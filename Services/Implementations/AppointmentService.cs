using System.Data;
using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Appointment;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Models.Enums;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AppointmentService> _logger;

        public AppointmentService(IUnitOfWork unitOfWork, ILogger<AppointmentService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ===== Create Patient Appointment (Online Booking via App) =====
        public async Task<ApiResponse<AppointmentDto>> CreateAppointmentAsync(int userId, CreateAppointmentDto dto)
        {
            // Find Patient by UserId
            var patient = await _unitOfWork.Patients.Query()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return ApiResponse<AppointmentDto>.Failure("حساب المريض غير موجود", 404);

            // Find Doctor
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == dto.DoctorId);

            if (doctor == null)
                return ApiResponse<AppointmentDto>.Failure("الطبيب المحدد غير موجود", 404);

            // Check if slot falls on active doctor schedule day
            var dayOfWeek = dto.AppointmentDate.DayOfWeek;
            var schedule = await _unitOfWork.DoctorSchedules.Query()
                .FirstOrDefaultAsync(s => s.DoctorId == dto.DoctorId && s.DayOfWeek == dayOfWeek && s.IsActive);

            if (schedule == null)
                return ApiResponse<AppointmentDto>.Failure("الطبيب ليس لديه مواعيد عمل متاحة في هذا اليوم", 400);

            // Validate that the slot is inside operating hours
            if (dto.StartTime < schedule.StartTime || dto.StartTime >= schedule.EndTime)
                return ApiResponse<AppointmentDto>.Failure("وقت الحجز يقع خارج ساعات عمل الطبيب الرسمية", 400);

            using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Prevent double booking (Check for active appointments in the same slot)
                var doubleBookingExists = await _unitOfWork.Appointments.Query()
                    .AnyAsync(a => a.DoctorId == dto.DoctorId 
                        && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                        && a.StartTime == dto.StartTime 
                        && a.Status != AppointmentStatus.Cancelled);

                if (doubleBookingExists)
                    return ApiResponse<AppointmentDto>.Failure("عذراً، هذا الموعد محجوز بالفعل! يرجى اختيار موعد آخر", 409);

                // Prevent a patient or family member from booking overlapping appointments
                var hasOverlapForPerson = await _unitOfWork.Appointments.Query()
                    .AnyAsync(a => a.AppointmentDate.Date == dto.AppointmentDate.Date
                        && a.StartTime == dto.StartTime
                        && a.Status != AppointmentStatus.Cancelled
                        && (
                            a.PatientId == patient.Id
                            || (dto.FamilyMemberId.HasValue && a.FamilyMemberId == dto.FamilyMemberId)
                        ));

                if (hasOverlapForPerson)
                    return ApiResponse<AppointmentDto>.Failure("لا يمكنك حجز أكثر من موعد لنفس الشخص في نفس الوقت", 409);

                // Calculate Queue Number for the day
                var todayAppointmentsCount = await _unitOfWork.Appointments.Query()
                    .CountAsync(a => a.DoctorId == dto.DoctorId 
                        && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                        && a.Status != AppointmentStatus.Cancelled);

            var queueNumber = todayAppointmentsCount + 1;

            // Validate Family Member if provided
            Models.Entities.FamilyMember? familyMember = null;
            if (dto.FamilyMemberId.HasValue)
            {
                familyMember = await _unitOfWork.FamilyMembers.Query()
                    .FirstOrDefaultAsync(fm => fm.Id == dto.FamilyMemberId.Value && fm.PatientId == patient.Id);

                if (familyMember == null)
                    return ApiResponse<AppointmentDto>.Failure("فرد العائلة غير موجود أو لا ينتمي لهذا الحساب", 400);
            }

            // Create appointment
            var appointment = new Appointment
            {
                PatientId = patient.Id,
                FamilyMemberId = dto.FamilyMemberId,
                DoctorId = dto.DoctorId,
                AppointmentDate = dto.AppointmentDate.Date,
                StartTime = dto.StartTime,
                EndTime = dto.StartTime.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)),
                Status = AppointmentStatus.Confirmed,
                QueueNumber = queueNumber,
                QueueStatus = QueueStatus.Waiting,
                Notes = dto.Notes
            };

            await _unitOfWork.Appointments.AddAsync(appointment);
            
            // Create notification for patient
            var notification = new Notification
            {
                UserId = userId,
                Title = "تأكيد الحجز",
                Message = $"تم تأكيد حجزك بنجاح مع د. {doctor.User.FullName} يوم {appointment.AppointmentDate:yyyy-MM-dd} الساعة {appointment.StartTime:hh\\:mm}."
            };
            await _unitOfWork.Notifications.AddAsync(notification);
            
            await _unitOfWork.CompleteAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Appointment created successfully. Queue: {Queue}", queueNumber);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "تم حجز الموعد بنجاح", 201);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

        // ===== Create Clinic Walk-in Booking (Offline/Online via Admin Panel) =====
        public async Task<ApiResponse<AppointmentDto>> CreateClinicAppointmentAsync(int userId, ClinicCreateAppointmentDto dto)
        {
            // Verify User Authorization
            var requestingUser = await _unitOfWork.Users.GetByIdAsync(userId);
            if (requestingUser != null)
            {
                if (requestingUser.Role == UserRole.ClinicAdmin)
                {
                    var admin = await _unitOfWork.ClinicAdmins.Query().FirstOrDefaultAsync(ca => ca.UserId == userId);
                    if (admin == null || !await _unitOfWork.DoctorClinics.AnyAsync(dc => dc.ClinicId == admin.ClinicId && dc.DoctorId == dto.DoctorId && dc.IsActive))
                        return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بالحجز لهذا الطبيب", 403);
                }
                else if (requestingUser.Role == UserRole.Doctor)
                {
                    var requestingDoctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                    if (requestingDoctor == null || requestingDoctor.Id != dto.DoctorId)
                        return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بالحجز كطبيب آخر", 403);
                }
            }

            // Verify Doctor exists
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == dto.DoctorId);

            if (doctor == null)
                return ApiResponse<AppointmentDto>.Failure("الطبيب المحدد غير موجود", 404);

            // Check if slot falls on active doctor schedule day
            var dayOfWeek = dto.AppointmentDate.DayOfWeek;
            var schedule = await _unitOfWork.DoctorSchedules.Query()
                .FirstOrDefaultAsync(s => s.DoctorId == dto.DoctorId && s.DayOfWeek == dayOfWeek && s.IsActive);

            if (schedule == null)
                return ApiResponse<AppointmentDto>.Failure("الطبيب ليس لديه مواعيد عمل متاحة في هذا اليوم", 400);

            // Validate slot inside operating hours
            if (dto.StartTime < schedule.StartTime || dto.StartTime >= schedule.EndTime)
                return ApiResponse<AppointmentDto>.Failure("وقت الحجز يقع خارج ساعات عمل الطبيب الرسمية", 400);

            using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // Prevent double booking
            var doubleBookingExists = await _unitOfWork.Appointments.Query()
                .AnyAsync(a => a.DoctorId == dto.DoctorId 
                    && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                    && a.StartTime == dto.StartTime 
                    && a.Status != AppointmentStatus.Cancelled);

            if (doubleBookingExists)
                return ApiResponse<AppointmentDto>.Failure("عذراً، هذا الموعد محجوز بالفعل! يرجى اختيار موعد آخر", 409);

            if (dto.PatientId.HasValue)
            {
                var patientOverlapExists = await _unitOfWork.Appointments.Query()
                    .AnyAsync(a => a.AppointmentDate.Date == dto.AppointmentDate.Date
                        && a.StartTime == dto.StartTime
                        && a.Status != AppointmentStatus.Cancelled
                        && a.PatientId == dto.PatientId.Value);

                if (patientOverlapExists)
                    return ApiResponse<AppointmentDto>.Failure("لا يمكنك حجز أكثر من موعد لنفس المريض في نفس الوقت", 409);
            }

            // Determine if booking is for a registered patient or offline walk-in
            Patient? registeredPatient = null;
            if (dto.PatientId.HasValue)
            {
                registeredPatient = await _unitOfWork.Patients.Query()
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == dto.PatientId.Value);

                if (registeredPatient == null)
                    return ApiResponse<AppointmentDto>.Failure("المريض المسجل غير موجود", 404);
            }
            else if (string.IsNullOrWhiteSpace(dto.OfflinePatientName))
            {
                return ApiResponse<AppointmentDto>.Failure("يجب إدخال اسم المريض الخارجي (Offline Patient Name) أو تحديد معرف مريض مسجل", 400);
            }

            // Calculate Queue Number for the day
            var todayAppointmentsCount = await _unitOfWork.Appointments.Query()
                .CountAsync(a => a.DoctorId == dto.DoctorId 
                    && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                    && a.Status != AppointmentStatus.Cancelled);

            var queueNumber = todayAppointmentsCount + 1;

            // Create appointment
            var appointment = new Appointment
            {
                PatientId = dto.PatientId,
                DoctorId = dto.DoctorId,
                AppointmentDate = dto.AppointmentDate.Date,
                StartTime = dto.StartTime,
                EndTime = dto.StartTime.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)),
                Status = AppointmentStatus.Confirmed,
                QueueNumber = queueNumber,
                QueueStatus = QueueStatus.Waiting,
                Notes = dto.Notes,
                OfflinePatientName = dto.OfflinePatientName,
                OfflinePatientPhone = dto.OfflinePatientPhone,
                IsEmergency = dto.IsEmergency,
                ChiefComplaint = dto.ChiefComplaint,
                IsPaid = dto.IsPaid,
                PaymentMethod = dto.PaymentMethod,
                OfflinePatientAge = dto.OfflinePatientAge,
                OfflinePatientGender = dto.OfflinePatientGender
            };

            await _unitOfWork.Appointments.AddAsync(appointment);

            // Create notification for registered patient if applicable
            if (dto.PatientId.HasValue && registeredPatient != null)
            {
                var notification = new Notification
                {
                    UserId = registeredPatient.UserId,
                    Title = "تأكيد الحجز",
                    Message = $"تم تأكيد حجزك بنجاح مع د. {doctor.User.FullName} يوم {appointment.AppointmentDate:yyyy-MM-dd} الساعة {appointment.StartTime:hh\\:mm}."
                };
                await _unitOfWork.Notifications.AddAsync(notification);
            }

            await _unitOfWork.CompleteAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Walk-in clinic booking created successfully. Queue: {Queue}", queueNumber);

            // Load navigations if patient registered
            if (dto.PatientId.HasValue && appointment.Patient == null)
            {
                appointment.Patient = registeredPatient;
            }
            appointment.Doctor = doctor;

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "تم تسجيل موعد العيادة بنجاح", 201);
        }

        // ===== Get Patient's Bookings list =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetPatientAppointmentsAsync(int userId, string? filter = null, AppointmentStatus? status = null)
        {
            var patient = await _unitOfWork.Patients.Query()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return ApiResponse<List<AppointmentDto>>.Failure("حساب المريض غير موجود", 404);

            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.DoctorClinics)
                        .ThenInclude(dc => dc.Clinic)
                .Include(a => a.FamilyMember)
                .Where(a => a.PatientId == patient.Id);

            if (!string.IsNullOrWhiteSpace(filter))
            {
                if (filter.Equals("upcoming", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => a.Status == AppointmentStatus.Confirmed 
                                          || a.Status == AppointmentStatus.InProgress 
                                          || a.Status == AppointmentStatus.Pending);
                    
                    var appointmentsList = await query
                        .OrderBy(a => a.AppointmentDate)
                        .ThenBy(a => a.StartTime)
                        .ToListAsync();
                    
                    var dtosList = appointmentsList.Select(MapToDto).ToList();
                    return ApiResponse<List<AppointmentDto>>.Success(dtosList, "تم استرجاع مواعيد المريض القادمة بنجاح");
                }
                else if (filter.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => a.Status == AppointmentStatus.Completed);
                }
                else if (filter.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => a.Status == AppointmentStatus.Cancelled 
                                          || a.Status == AppointmentStatus.NoShow);
                }
            }
            else if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            var dtos = appointments.Select(MapToDto).ToList();
            return ApiResponse<List<AppointmentDto>>.Success(dtos, "تم استرجاع مواعيد المريض بنجاح");
        }

        // ===== Get Doctor's Appointments list =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetDoctorAppointmentsAsync(int userId, DateTime? date = null, AppointmentStatus? status = null)
        {
            var doctor = await _unitOfWork.Doctors.Query()
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (doctor == null)
                return ApiResponse<List<AppointmentDto>>.Failure("حساب الطبيب غير موجود", 404);

            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Where(a => a.DoctorId == doctor.Id);

            if (date.HasValue)
            {
                query = query.Where(a => a.AppointmentDate.Date == date.Value.Date);
            }

            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            var appointments = await query
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            var dtos = appointments.Select(MapToDto).ToList();
            return ApiResponse<List<AppointmentDto>>.Success(dtos, "تم استرجاع مواعيد الطبيب بنجاح");
        }

        // ===== Get Appointment By ID =====
        public async Task<ApiResponse<AppointmentDto>> GetAppointmentByIdAsync(int appointmentId, int userId)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.DoctorClinics)
                        .ThenInclude(dc => dc.Clinic)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("الموعد غير موجود", 404);

            // Authorization check
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<AppointmentDto>.Failure("المستخدم غير موجود", 404);

            if (user.Role == UserRole.Patient && appointment.PatientId != null)
            {
                var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
                if (patient == null || appointment.PatientId != patient.Id)
                    return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بعرض هذا الموعد", 403);
            }
            else if (user.Role == UserRole.Doctor)
            {
                var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor == null || appointment.DoctorId != doctor.Id)
                    return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بعرض هذا الموعد", 403);
            }
            else if (user.Role == UserRole.ClinicAdmin)
            {
                // Check if admin belongs to the clinic that the doctor operates in
                var admin = await _unitOfWork.ClinicAdmins.Query().FirstOrDefaultAsync(a => a.UserId == userId);
                var schedule = await _unitOfWork.DoctorSchedules.Query()
                    .FirstOrDefaultAsync(s => s.DoctorId == appointment.DoctorId);
                
                if (admin == null || schedule == null || schedule.ClinicId != admin.ClinicId)
                    return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بعرض هذا الموعد", 403);
            }

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "تم استرجاع الموعد بنجاح");
        }

        // ===== Cancel Appointment =====
        public async Task<ApiResponse<AppointmentDto>> CancelAppointmentAsync(int appointmentId, int userId, CancelAppointmentDto dto)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("الموعد غير موجود", 404);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<AppointmentDto>.Failure("المستخدم غير موجود", 404);

            // Authorization check for cancel
            if (user.Role == UserRole.Patient)
            {
                var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
                if (patient == null || appointment.PatientId != patient.Id)
                    return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بإلغاء هذا الموعد", 403);
            }
            else if (user.Role == UserRole.Doctor)
            {
                var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor == null || appointment.DoctorId != doctor.Id)
                    return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بإلغاء هذا الموعد", 403);
            }

            // Perform cancellation
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancellationReason = dto.Reason;
            appointment.QueueStatus = QueueStatus.Refunded;
            appointment.RefundStatus = RefundStatus.Pending;

            _unitOfWork.Appointments.Update(appointment);

            // Create notification for patient if registered
            if (appointment.PatientId.HasValue && appointment.Patient != null)
            {
                string title = "إلغاء الحجز";
                string message = "";

                if (user.Role == UserRole.Patient)
                {
                    message = $"تم إلغاء حجزك بنجاح بناءً على طلبك مع د. {appointment.Doctor.User.FullName} يوم {appointment.AppointmentDate:yyyy-MM-dd} الساعة {appointment.StartTime:hh\\:mm}.";
                }
                else
                {
                    title = "إلغاء الحجز من قبل العيادة";
                    string reasonText = string.IsNullOrWhiteSpace(dto.Reason) ? "غير محدد" : dto.Reason;
                    message = $"نود إفادتكم بأنه تم إلغاء حجزكم مع د. {appointment.Doctor.User.FullName} يوم {appointment.AppointmentDate:yyyy-MM-dd} الساعة {appointment.StartTime:hh\\:mm} من قبل العيادة. السبب: {reasonText}.";
                }

                var notification = new Notification
                {
                    UserId = appointment.Patient.UserId,
                    Title = title,
                    Message = message
                };
                await _unitOfWork.Notifications.AddAsync(notification);
            }

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Appointment {Id} has been Cancelled and Refunded", appointmentId);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "تم إلغاء الموعد واسترداد قيمته بنجاح");
        }

        // ===== Reschedule Appointment =====
        public async Task<ApiResponse<AppointmentDto>> RescheduleAppointmentAsync(int appointmentId, int userId, RescheduleAppointmentDto dto)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("الموعد غير موجود", 404);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<AppointmentDto>.Failure("المستخدم غير موجود", 404);

            // Authorization check
            if (user.Role == UserRole.Patient)
            {
                var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
                if (patient == null || appointment.PatientId != patient.Id)
                    return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بتعديل موعد هذا الحجز", 403);
            }
            else if (user.Role == UserRole.Doctor)
            {
                var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor == null || appointment.DoctorId != doctor.Id)
                    return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بتعديل موعد هذا الحجز", 403);
            }

            // Validate schedule
            var dayOfWeek = dto.AppointmentDate.DayOfWeek;
            var schedule = await _unitOfWork.DoctorSchedules.Query()
                .FirstOrDefaultAsync(s => s.DoctorId == appointment.DoctorId && s.DayOfWeek == dayOfWeek && s.IsActive);

            if (schedule == null)
                return ApiResponse<AppointmentDto>.Failure("الطبيب ليس لديه مواعيد عمل متاحة في هذا اليوم الجديد", 400);

            if (dto.StartTime < schedule.StartTime || dto.StartTime >= schedule.EndTime)
                return ApiResponse<AppointmentDto>.Failure("وقت الحجز الجديد يقع خارج ساعات عمل الطبيب الرسمية", 400);

            using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // Prevent double booking
            var doubleBookingExists = await _unitOfWork.Appointments.Query()
                .AnyAsync(a => a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                    && a.StartTime == dto.StartTime 
                    && a.Id != appointment.Id
                    && a.Status != AppointmentStatus.Cancelled);

            if (doubleBookingExists)
                return ApiResponse<AppointmentDto>.Failure("عذراً، هذا الموعد الجديد محجوز بالفعل! يرجى اختيار موعد آخر", 409);

            // Prevent overlapping appointments for the same patient or family member
            var overlapExists = await _unitOfWork.Appointments.Query()
                .AnyAsync(a => a.AppointmentDate.Date == dto.AppointmentDate.Date
                    && a.StartTime == dto.StartTime
                    && a.Id != appointment.Id
                    && a.Status != AppointmentStatus.Cancelled
                    && (
                        (appointment.PatientId.HasValue && a.PatientId == appointment.PatientId)
                        || (appointment.FamilyMemberId.HasValue && a.FamilyMemberId == appointment.FamilyMemberId)
                    ));

            if (overlapExists)
                return ApiResponse<AppointmentDto>.Failure("لا يمكنك إعادة جدولة هذا الموعد إلى وقت متداخل مع موعد آخر لنفس الشخص", 409);

            var oldDateStr = appointment.AppointmentDate.ToString("yyyy-MM-dd");
            var oldTimeStr = appointment.StartTime.ToString(@"hh\:mm");
            var newDateStr = dto.AppointmentDate.ToString("yyyy-MM-dd");
            var newTimeStr = dto.StartTime.ToString(@"hh\:mm");

            // Calculate new Queue Number for the new day
            var newDayAppointmentsCount = await _unitOfWork.Appointments.Query()
                .CountAsync(a => a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                    && a.Status != AppointmentStatus.Cancelled);

            var queueNumber = newDayAppointmentsCount + 1;

            // Perform Rescheduling
            appointment.AppointmentDate = dto.AppointmentDate.Date;
            appointment.StartTime = dto.StartTime;
            appointment.EndTime = dto.StartTime.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));
            appointment.QueueNumber = queueNumber;
            appointment.QueueStatus = QueueStatus.Waiting;
            appointment.Status = AppointmentStatus.Confirmed;

            _unitOfWork.Appointments.Update(appointment);

            // Send notification to Patient
            if (appointment.PatientId.HasValue && appointment.Patient != null)
            {
                var patientNotification = new Notification
                {
                    UserId = appointment.Patient.UserId,
                    Title = "تعديل موعد الحجز",
                    Message = $"تم تعديل موعد حجزك بنجاح مع د. {appointment.Doctor.User.FullName} ليصبح يوم {newDateStr} الساعة {newTimeStr} بدلاً من يوم {oldDateStr} الساعة {oldTimeStr}."
                };
                await _unitOfWork.Notifications.AddAsync(patientNotification);
            }

            // Send notification to Doctor
            var doctorNotification = new Notification
            {
                UserId = appointment.Doctor.UserId,
                Title = "تعديل موعد حجز مريض",
                Message = $"قام المريض {appointment.Patient?.User?.FullName ?? "خارجي"} بتعديل موعد حجزه ليصبح يوم {newDateStr} الساعة {newTimeStr} بدلاً من يوم {oldDateStr} الساعة {oldTimeStr}."
            };
            await _unitOfWork.Notifications.AddAsync(doctorNotification);

            // Send notification to Clinic Admins
            var clinicAdmins = await _unitOfWork.ClinicAdmins.Query()
                .Where(ca => ca.ClinicId == schedule.ClinicId)
                .Select(ca => ca.UserId)
                .ToListAsync();

            foreach (var adminUserId in clinicAdmins)
            {
                var adminNotification = new Notification
                {
                    UserId = adminUserId,
                    Title = "تعديل موعد حجز مريض",
                    Message = $"تم تعديل موعد حجز للمريض {appointment.Patient?.User?.FullName ?? "خارجي"} مع د. {appointment.Doctor.User.FullName} ليصبح يوم {newDateStr} الساعة {newTimeStr} بدلاً من يوم {oldDateStr} الساعة {oldTimeStr}."
                };
                await _unitOfWork.Notifications.AddAsync(adminNotification);
            }

            await _unitOfWork.CompleteAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Appointment {Id} Rescheduled successfully to {Date} {Time}", appointmentId, newDateStr, newTimeStr);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "تم تعديل موعد الحجز بنجاح وإرسال الإشعارات اللازمة");
        }

        // ===== Update Appointment Status =====
        public async Task<ApiResponse<AppointmentDto>> UpdateStatusAsync(int appointmentId, int userId, UpdateAppointmentStatusDto dto)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("الموعد غير موجود", 404);

            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null || appointment.DoctorId != doctor.Id)
                return ApiResponse<AppointmentDto>.Failure("غير مصرح لك بتغيير حالة هذا الموعد", 403);

            // ===== Enforce valid status transitions =====
            var validTransitions = new Dictionary<AppointmentStatus, List<AppointmentStatus>>
            {
                { AppointmentStatus.Pending,     new() { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled } },
                { AppointmentStatus.Confirmed,   new() { AppointmentStatus.InProgress, AppointmentStatus.Cancelled, AppointmentStatus.NoShow } },
                { AppointmentStatus.InProgress,  new() { AppointmentStatus.Completed, AppointmentStatus.Cancelled } },
                { AppointmentStatus.Completed,   new() { } }, // Terminal state
                { AppointmentStatus.Cancelled,   new() { } }, // Terminal state
                { AppointmentStatus.NoShow,      new() { AppointmentStatus.Confirmed } }  // Allow rebook
            };

            if (!validTransitions.ContainsKey(appointment.Status) 
                || !validTransitions[appointment.Status].Contains(dto.Status))
            {
                return ApiResponse<AppointmentDto>.Failure(
                    $"لا يمكن تغيير حالة الموعد من '{appointment.Status}' إلى '{dto.Status}'", 400);
            }

            // Update statuses
            appointment.Status = dto.Status;
            appointment.QueueStatus = dto.Status switch
            {
                AppointmentStatus.Confirmed => QueueStatus.Waiting,
                AppointmentStatus.InProgress => QueueStatus.InConsultation,
                AppointmentStatus.Completed => QueueStatus.Completed,
                AppointmentStatus.Cancelled => QueueStatus.Refunded,
                AppointmentStatus.NoShow => null, // Remove from active queue
                _ => appointment.QueueStatus
            };

            if (dto.Status == AppointmentStatus.Cancelled)
            {
                appointment.RefundStatus = RefundStatus.Pending;
            }

            _unitOfWork.Appointments.Update(appointment);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Appointment status updated to {Status}", dto.Status);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "تم تحديث حالة الموعد بنجاح");
        }

        // ===== Get Today's Queue =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetTodayQueueAsync(int userId)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null)
                return ApiResponse<List<AppointmentDto>>.Failure("حساب الطبيب غير موجود", 404);

            var todayQueue = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Where(a => a.DoctorId == doctor.Id 
                    && a.AppointmentDate.Date == DateTime.Today 
                    && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.QueueNumber)
                .ToListAsync();

            var dtos = todayQueue.Select(MapToDto).ToList();
            return ApiResponse<List<AppointmentDto>>.Success(dtos, "تم استرجاع قائمة الطابور المباشر لليوم");
        }

        // ===== Get Patient Live Queue Tracker =====
        public async Task<ApiResponse<LiveQueueTrackerDto>> GetLiveQueueTrackerAsync(int appointmentId, int userId)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<LiveQueueTrackerDto>.Failure("الموعد غير موجود", 404);

            // Patient Auth check
            var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null || appointment.PatientId != patient.Id)
                return ApiResponse<LiveQueueTrackerDto>.Failure("غير مصرح لك بتعقب هذا الموعد", 403);

            if (appointment.Status == AppointmentStatus.Cancelled)
                return ApiResponse<LiveQueueTrackerDto>.Failure("هذا الموعد قد تم إلغاؤه بالفعل", 400);

            // Fetch currently serving patient in consultation
            var activeServing = await _unitOfWork.Appointments.Query()
                .Where(a => a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate.Date == appointment.AppointmentDate.Date 
                    && a.QueueStatus == QueueStatus.InConsultation)
                .FirstOrDefaultAsync();

            int currentServingNumber = 0;
            if (activeServing != null)
            {
                currentServingNumber = activeServing.QueueNumber ?? 0;
            }
            else
            {
                // Find last completed patient for today
                var lastCompleted = await _unitOfWork.Appointments.Query()
                    .Where(a => a.DoctorId == appointment.DoctorId 
                        && a.AppointmentDate.Date == appointment.AppointmentDate.Date 
                        && a.QueueStatus == QueueStatus.Completed)
                    .OrderByDescending(a => a.QueueNumber)
                    .FirstOrDefaultAsync();

                currentServingNumber = lastCompleted?.QueueNumber ?? 0;
            }

            // Calculate patients ahead in the queue
            int myQueueNumber = appointment.QueueNumber ?? 0;
            int patientsAhead = 0;
            if (myQueueNumber > 0)
            {
                patientsAhead = await _unitOfWork.Appointments.Query()
                    .CountAsync(a => a.DoctorId == appointment.DoctorId 
                        && a.AppointmentDate.Date == appointment.AppointmentDate.Date 
                        && a.QueueStatus == QueueStatus.Waiting 
                        && a.QueueNumber < myQueueNumber);
            }

            // Estimating 15 minutes wait time per waiting patient
            int estimatedWaitTimeMinutes = patientsAhead * 15;

            var trackerDto = new LiveQueueTrackerDto
            {
                AppointmentId = appointment.Id,
                MyQueueNumber = myQueueNumber,
                CurrentServingNumber = currentServingNumber,
                PatientsAheadOfMe = patientsAhead,
                EstimatedWaitTimeMinutes = estimatedWaitTimeMinutes,
                MyQueueStatus = appointment.QueueStatus,
                DoctorName = appointment.Doctor.User.FullName
            };

            return ApiResponse<LiveQueueTrackerDto>.Success(trackerDto, "تم تحديث معلومات طابور الحجز المباشر");
        }

        // ===== Call Next Patient in Queue =====
        public async Task<ApiResponse<AppointmentDto>> CallNextPatientInQueueAsync(int doctorUserId)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<AppointmentDto>.Failure("حساب الطبيب غير موجود", 404);

            var today = DateTime.Today;

            // 1. Mark currently serving patient (if any) as Completed
            var activeServing = await _unitOfWork.Appointments.Query()
                .FirstOrDefaultAsync(a => a.DoctorId == doctor.Id 
                    && a.AppointmentDate.Date == today 
                    && a.QueueStatus == QueueStatus.InConsultation);

            if (activeServing != null)
            {
                activeServing.Status = AppointmentStatus.Completed;
                activeServing.QueueStatus = QueueStatus.Completed;
                _unitOfWork.Appointments.Update(activeServing);
            }

            // 2. Find next patient in Waiting state
            var nextPatient = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Where(a => a.DoctorId == doctor.Id 
                    && a.AppointmentDate.Date == today 
                    && a.QueueStatus == QueueStatus.Waiting 
                    && a.Status == AppointmentStatus.Confirmed)
                .OrderBy(a => a.QueueNumber)
                .FirstOrDefaultAsync();

            if (nextPatient == null)
            {
                await _unitOfWork.CompleteAsync(); // Save currently serving completion
                return ApiResponse<AppointmentDto>.Failure("لا يوجد مرضى في الانتظار اليوم", 404);
            }

            // 3. Mark next patient as InConsultation / InProgress
            nextPatient.Status = AppointmentStatus.InProgress;
            nextPatient.QueueStatus = QueueStatus.InConsultation;

            _unitOfWork.Appointments.Update(nextPatient);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Doctor {Doc} called next patient Queue {Queue}", doctor.Id, nextPatient.QueueNumber);

            return ApiResponse<AppointmentDto>.Success(MapToDto(nextPatient), $"تم مناداة المريض رقم {nextPatient.QueueNumber} للدخول");
        }

        // ===== Map Entity to DTO Helper =====
        private AppointmentDto MapToDto(Appointment app)
        {
            string patientName = "مريض خارجي";
            string phone = string.Empty;

            if (app.Patient != null && app.Patient.User != null)
            {
                patientName = app.Patient.User.FullName;
                phone = app.Patient.User.PhoneNumber;
            }
            else if (!string.IsNullOrWhiteSpace(app.OfflinePatientName))
            {
                patientName = app.OfflinePatientName;
                phone = app.OfflinePatientPhone ?? string.Empty;
            }

            string? familyMemberName = null;
            if (app.FamilyMember != null)
            {
                familyMemberName = app.FamilyMember.Name;
            }
            else if (app.FamilyMemberId.HasValue)
            {
                // In case it wasn't loaded via Include, fetch it synchronously or just leave as null
                // To avoid sync-over-async in MapToDto, we should rely on Includes in the queries.
                // Assuming it's Included.
            }

            int? clinicId = null;
            string? clinicName = null;
            string? clinicAddress = null;

            var activeClinic = app.Doctor?.DoctorClinics?.FirstOrDefault(dc => dc.IsActive)?.Clinic;
            if (activeClinic != null)
            {
                clinicId = activeClinic.Id;
                clinicName = activeClinic.Name;
                clinicAddress = $"{activeClinic.Area}، {activeClinic.Government}";
            }

            int? currentServingNumber = null;
            if (app.AppointmentDate.Date == DateTime.Today 
                && app.Status == AppointmentStatus.Confirmed 
                && app.QueueStatus == QueueStatus.Waiting)
            {
                var activeServing = _unitOfWork.Appointments.Query()
                    .FirstOrDefault(a => a.DoctorId == app.DoctorId 
                        && a.AppointmentDate.Date == DateTime.Today 
                        && a.QueueStatus == QueueStatus.InConsultation);

                if (activeServing != null)
                {
                    currentServingNumber = activeServing.QueueNumber ?? 0;
                }
                else
                {
                    var lastCompleted = _unitOfWork.Appointments.Query()
                        .Where(a => a.DoctorId == app.DoctorId 
                            && a.AppointmentDate.Date == DateTime.Today 
                            && a.QueueStatus == QueueStatus.Completed)
                        .OrderByDescending(a => a.QueueNumber)
                        .FirstOrDefault();

                    currentServingNumber = lastCompleted?.QueueNumber ?? 0;
                }
            }

            return new AppointmentDto
            {
                Id = app.Id,
                PatientId = app.PatientId,
                PatientName = patientName,
                FamilyMemberId = app.FamilyMemberId,
                FamilyMemberName = familyMemberName,
                OfflinePatientPhone = phone,
                DoctorId = app.DoctorId,
                DoctorName = app.Doctor?.User?.FullName ?? string.Empty,
                Specialization = app.Doctor?.Specialization ?? string.Empty,
                DoctorProfileImageUrl = app.Doctor?.User?.ProfileImageUrl,
                ClinicId = clinicId,
                ClinicName = clinicName,
                ClinicAddress = clinicAddress,
                CurrentServingNumber = currentServingNumber,
                AppointmentDate = app.AppointmentDate,
                StartTime = app.StartTime,
                EndTime = app.EndTime,
                Status = app.Status,
                StatusText = app.Status.ToString(),
                QueueNumber = app.QueueNumber,
                QueueStatus = app.QueueStatus,
                RefundStatus = app.RefundStatus,
                RefundStatusText = app.RefundStatus == RefundStatus.Pending ? "Refund Pending" : 
                                   app.RefundStatus == RefundStatus.Processed ? "Refund Processed" : "",
                Notes = app.Notes,
                CancellationReason = app.CancellationReason,
                IsEmergency = app.IsEmergency,
                ChiefComplaint = app.ChiefComplaint,
                IsPaid = app.IsPaid,
                PaymentMethod = app.PaymentMethod,
                PaymentMethodText = app.PaymentMethod.ToString(),
                OfflinePatientAge = app.OfflinePatientAge,
                OfflinePatientGender = app.OfflinePatientGender,
                CreatedAt = app.CreatedAt
            };
        }

        // ===== Get Clinic Dashboard Overview =====
        public async Task<ApiResponse<ClinicDashboardOverviewDto>> GetClinicDashboardOverviewAsync(int clinicAdminUserId, int? doctorId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(a => a.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<ClinicDashboardOverviewDto>.Failure("غير مصرح لك بنظرة عامة على العيادة كمسؤول", 403);

            int clinicId = admin.ClinicId;
            var today = DateTime.Today;

            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.DoctorClinics)
                .Where(a => a.AppointmentDate.Date == today && a.Status != AppointmentStatus.Cancelled);

            // Filter doctors who belong to the admin's clinic
            query = query.Where(a => a.Doctor.DoctorClinics.Any(dc => dc.ClinicId == clinicId && dc.IsActive));

            if (doctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == doctorId.Value);
            }

            var appointments = await query.ToListAsync();

            int paidCount = appointments.Count(a => a.IsPaid);
            int walkInCount = appointments.Count(a => a.PatientId == null || !string.IsNullOrEmpty(a.OfflinePatientName));
            decimal totalRevenue = appointments.Where(a => a.IsPaid).Sum(a => a.Doctor.ConsultationFee);

            var overview = new ClinicDashboardOverviewDto
            {
                PaidCount = paidCount,
                WalkInCount = walkInCount,
                TodayRevenueAmount = totalRevenue
            };

            return ApiResponse<ClinicDashboardOverviewDto>.Success(overview, "تم استرجاع الإحصائيات بنجاح");
        }

        // ===== Get Clinic Today Queue =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetClinicTodayQueueAsync(int clinicAdminUserId, int doctorId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(a => a.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<List<AppointmentDto>>.Failure("غير مصرح لك باستدعاء طابور العيادة", 403);

            int clinicId = admin.ClinicId;

            var isLinked = await _unitOfWork.DoctorClinics.Query()
                .AnyAsync(dc => dc.ClinicId == clinicId && dc.DoctorId == doctorId && dc.IsActive);
            if (!isLinked)
                return ApiResponse<List<AppointmentDto>>.Failure("الطبيب المحدد لا يعمل في هذه العيادة", 400);

            var today = DateTime.Today;

            var appointments = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics)
                    .ThenInclude(dc => dc.Clinic)
                .Where(a => a.DoctorId == doctorId 
                    && a.AppointmentDate.Date == today 
                    && a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            // Custom sorting:
            // 1. QueueStatus.InConsultation (order weight 0)
            // 2. IsEmergency && Waiting (order weight 1)
            // 3. !IsEmergency && Waiting (order weight 2)
            // 4. Completed/Others (order weight 3)
            var sortedAppointments = appointments
                .OrderBy(a => a.QueueStatus == QueueStatus.InConsultation ? 0 :
                              a.QueueStatus == QueueStatus.Waiting && a.IsEmergency ? 1 :
                              a.QueueStatus == QueueStatus.Waiting && !a.IsEmergency ? 2 : 3)
                .ThenBy(a => a.QueueNumber)
                .Select(MapToDto)
                .ToList();

            return ApiResponse<List<AppointmentDto>>.Success(sortedAppointments, "تم استرجاع قائمة الطابور بنجاح");
        }

        // ===== Start Checkup =====
        public async Task<ApiResponse<AppointmentDto>> StartCheckupAsync(int clinicAdminUserId, int appointmentId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(a => a.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<AppointmentDto>.Failure("غير مصرح لك ببدء كشف المرضى", 403);

            int clinicId = admin.ClinicId;

            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("الحجز غير موجود", 404);

            var isLinked = appointment.Doctor.DoctorClinics.Any(dc => dc.ClinicId == clinicId && dc.IsActive);
            if (!isLinked)
                return ApiResponse<AppointmentDto>.Failure("لا يملك مدير هذه العيادة صلاحية إدارة هذا الحجز", 403);

            if (appointment.Status == AppointmentStatus.Cancelled)
                return ApiResponse<AppointmentDto>.Failure("هذا الحجز ملغى بالفعل ولا يمكن بدء الكشف له", 400);

            if (appointment.Status == AppointmentStatus.Completed)
                return ApiResponse<AppointmentDto>.Failure("هذا الحجز مكتمل بالفعل", 400);

            var today = DateTime.Today;

            // 1. Mark any other active patient in consultation for this doctor today as Completed
            var activeServing = await _unitOfWork.Appointments.Query()
                .FirstOrDefaultAsync(a => a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate.Date == today 
                    && a.QueueStatus == QueueStatus.InConsultation
                    && a.Id != appointment.Id);

            if (activeServing != null)
            {
                activeServing.Status = AppointmentStatus.Completed;
                activeServing.QueueStatus = QueueStatus.Completed;
                _unitOfWork.Appointments.Update(activeServing);
            }

            // 2. Start checkup for selected appointment
            appointment.Status = AppointmentStatus.InProgress;
            appointment.QueueStatus = QueueStatus.InConsultation;

            _unitOfWork.Appointments.Update(appointment);
            await _unitOfWork.CompleteAsync();

            // Create notification for registered patient
            if (appointment.PatientId.HasValue && appointment.Patient != null)
            {
                var notification = new Notification
                {
                    UserId = appointment.Patient.UserId,
                    Title = "بدء الكشف",
                    Message = $"لقد تم استدعاؤك الآن للدخول لعيادة د. {appointment.Doctor.User.FullName}."
                };
                await _unitOfWork.Notifications.AddAsync(notification);
                await _unitOfWork.CompleteAsync();
            }

            _logger.LogInformation("Clinic Admin started checkup for appointment {Id}", appointmentId);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "تم بدء الكشف واستدعاء المريض بنجاح");
        }

        // ===== Get Payments Dashboard =====
        public async Task<ApiResponse<PaymentsDashboardDto>> GetPaymentsDashboardAsync(int clinicAdminUserId, int? doctorId, string timeframe)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(a => a.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<PaymentsDashboardDto>.Failure("غير مصرح لك بنظرة عامة على لوحة المدفوعات كمسؤول", 403);

            int clinicId = admin.ClinicId;

            // Determine date filter based on timeframe
            var today = DateTime.Today;
            DateTime startDate = today;

            timeframe = timeframe.ToLower();
            if (timeframe == "week")
            {
                // Last 7 days
                startDate = today.AddDays(-7);
            }
            else if (timeframe == "month")
            {
                // Last 30 days
                startDate = today.AddDays(-30);
            }
            else
            {
                // Default to today
                startDate = today;
            }

            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics)
                .Where(a => a.AppointmentDate.Date >= startDate.Date && a.AppointmentDate.Date <= today.Date);

            // Filter by clinic
            query = query.Where(a => a.Doctor.DoctorClinics.Any(dc => dc.ClinicId == clinicId && dc.IsActive));

            if (doctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == doctorId.Value);
            }

            var appointments = await query.ToListAsync();

            // Calculate stats
            // Revenue is calculated from active paid appointments
            var activePaid = appointments.Where(a => a.IsPaid && a.Status != AppointmentStatus.Cancelled).ToList();
            var refunded = appointments.Where(a => a.Status == AppointmentStatus.Cancelled && a.RefundStatus == RefundStatus.Processed).ToList();

            decimal totalRevenue = activePaid.Sum(a => a.Doctor.ConsultationFee);
            decimal cashAmount = activePaid.Where(a => a.PaymentMethod == PaymentMethod.Cash).Sum(a => a.Doctor.ConsultationFee);
            decimal onlineAmount = activePaid.Where(a => a.PaymentMethod == PaymentMethod.Online).Sum(a => a.Doctor.ConsultationFee);
            decimal refundsAmount = refunded.Sum(a => a.Doctor.ConsultationFee);

            double cashPercentage = totalRevenue > 0 ? (double)(cashAmount / totalRevenue) * 100.0 : 0.0;
            double onlinePercentage = totalRevenue > 0 ? (double)(onlineAmount / totalRevenue) * 100.0 : 0.0;
            double refundsPercentage = totalRevenue > 0 ? (double)(refundsAmount / totalRevenue) * 100.0 : 0.0;

            // Map Recent Transactions
            var recentTransactions = appointments
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .Select(a => {
                    string name = "مريض خارجي";
                    if (a.Patient?.User != null)
                        name = a.Patient.User.FullName;
                    else if (!string.IsNullOrEmpty(a.OfflinePatientName))
                        name = a.OfflinePatientName;

                    string status = "Paid";
                    if (a.Status == AppointmentStatus.Cancelled)
                        status = "Refunded";
                    else if (!a.IsPaid)
                        status = "Pending";

                    return new TransactionDto
                    {
                        AppointmentId = a.Id,
                        PatientName = name,
                        DateTime = a.AppointmentDate.Date.Add(a.StartTime),
                        Amount = a.Doctor.ConsultationFee,
                        Status = status,
                        PaymentMethod = a.PaymentMethod,
                        PaymentMethodText = a.PaymentMethod.ToString()
                    };
                })
                .Take(50) // Cap recent transactions at 50
                .ToList();

            var dto = new PaymentsDashboardDto
            {
                TotalRevenue = totalRevenue,
                RevenueGrowthText = timeframe == "month" ? "+12% vs last month" : timeframe == "week" ? "+3% vs last week" : "+5% vs yesterday",
                CashAmount = cashAmount,
                CashPercentage = Math.Round(cashPercentage, 1),
                OnlineAmount = onlineAmount,
                OnlinePercentage = Math.Round(onlinePercentage, 1),
                RefundsAmount = refundsAmount,
                RefundsPercentage = Math.Round(refundsPercentage, 1),
                RecentTransactions = recentTransactions
            };

            return ApiResponse<PaymentsDashboardDto>.Success(dto, "تم استرجاع بيانات لوحة المدفوعات بنجاح");
        }
    }
}
