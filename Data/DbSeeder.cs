using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Only seed if the database is empty
            if (await context.Users.AnyAsync())
            {
                return;
            }

            var random = new Random();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");

            // Arrays for generating realistic Egyptian Data
            var specializations = new[] { "باطنة", "أسنان", "عظام", "أطفال", "نساء وتوليد", "جلدية", "نفسية", "عيون", "أنف وأذن", "قلب وأوعية دموية" };
            var governments = new[] { "القاهرة", "الجيزة", "الإسكندرية", "الدقهلية", "الشرقية" };
            var areas = new[] { "مدينة نصر", "المهندسين", "سموحة", "المنصورة", "الزقازيق", "المعادي", "الدقي", "التجمع الخامس" };
            
            var firstNames = new[] { "أحمد", "محمد", "محمود", "عمر", "كريم", "خالد", "علي", "طارق", "فاطمة", "نور", "سارة", "مريم", "ياسمين", "هند", "ندى", "آية" };
            var lastNames = new[] { "إبراهيم", "حسن", "يوسف", "الدسوقي", "النجار", "الحداد", "توفيق", "سليمان", "محمود", "فاروق" };
            
            var clinicNames = new[] { "مستشفى الشفاء", "عيادة الأمل", "مجمع النور الطبي", "دار الفؤاد", "مركز الحياة", "عيادات الصفوة", "مستشفى السلام", "عيادة المستقبل", "مركز الرعاية", "مستشفى النسيم" };

            // ================== Seed Clinics ==================
            var clinics = new List<Clinic>();
            for (int i = 0; i < clinicNames.Length; i++)
            {
                clinics.Add(new Clinic
                {
                    Name = clinicNames[i],
                    Description = "مركز طبي متكامل يقدم أحدث الخدمات الطبية على يد نخبة من الاستشاريين.",
                    Government = governments[random.Next(governments.Length)],
                    Area = areas[random.Next(areas.Length)],
                    Address = "شارع رئيسي متفرع من الميدان الرئيسي",
                    PhoneNumber = $"01{random.Next(100000000, 999999999)}",
                    IsActive = true
                });
            }
            await context.Clinics.AddRangeAsync(clinics);
            await context.SaveChangesAsync();

            // ================== Seed Clinic Admins ==================
            for (int i = 0; i < clinics.Count; i++)
            {
                var adminUser = new User
                {
                    FullName = $"أدمن {clinics[i].Name}",
                    PhoneNumber = $"012000000{i:D2}",
                    PasswordHash = passwordHash,
                    Role = UserRole.ClinicAdmin,
                    CreatedAt = DateTime.UtcNow
                };
                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();

                await context.ClinicAdmins.AddAsync(new ClinicAdmin
                {
                    UserId = adminUser.Id,
                    ClinicId = clinics[i].Id
                });
            }
            await context.SaveChangesAsync();

            // ================== Seed Doctors ==================
            var doctors = new List<Doctor>();
            for (int i = 0; i < 30; i++)
            {
                var fName = firstNames[random.Next(firstNames.Length)];
                var lName = lastNames[random.Next(lastNames.Length)];
                
                var doctorUser = new User
                {
                    FullName = $"د. {fName} {lName}",
                    PhoneNumber = $"011000000{i:D2}",
                    PasswordHash = passwordHash,
                    Role = UserRole.Doctor,
                    Gender = (fName == "فاطمة" || fName == "نور" || fName == "سارة" || fName == "مريم" || fName == "ياسمين" || fName == "هند" || fName == "ندى" || fName == "آية") ? Gender.Female : Gender.Male,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 100))
                };
                await context.Users.AddAsync(doctorUser);
                await context.SaveChangesAsync();

                var doctor = new Doctor
                {
                    UserId = doctorUser.Id,
                    Specialization = specializations[random.Next(specializations.Length)],
                    YearsOfExperience = random.Next(3, 25),
                    Bio = "طبيب متخصص يمتلك خبرة واسعة في تشخيص وعلاج الحالات المستعصية.",
                    ConsultationFee = random.Next(150, 600),
                    QrCodeKey = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
                };
                doctors.Add(doctor);
                await context.Doctors.AddAsync(doctor);
            }
            await context.SaveChangesAsync();

            // ================== Link Doctors to Clinics & Add Schedules ==================
            foreach (var doc in doctors)
            {
                int numberOfClinics = random.Next(1, 3);
                var assignedClinics = clinics.OrderBy(x => random.Next()).Take(numberOfClinics).ToList();

                foreach (var clinic in assignedClinics)
                {
                    await context.DoctorClinics.AddAsync(new DoctorClinic
                    {
                        DoctorId = doc.Id,
                        ClinicId = clinic.Id,
                        ConsultationFee = doc.ConsultationFee + random.Next(-50, 50),
                        IsAvailable = true,
                        IsActive = true
                    });

                    // Add dummy schedules
                    await context.DoctorSchedules.AddAsync(new DoctorSchedule
                    {
                        DoctorId = doc.Id,
                        ClinicId = clinic.Id,
                        DayOfWeek = (DayOfWeek)random.Next(0, 7),
                        StartTime = new TimeSpan(random.Next(10, 14), 0, 0),
                        EndTime = new TimeSpan(random.Next(16, 22), 0, 0),
                        SlotDurationMinutes = 30,
                        IsActive = true
                    });
                }
            }
            await context.SaveChangesAsync();

            // ================== Seed Patients ==================
            var patients = new List<Patient>();
            for (int i = 0; i < 50; i++)
            {
                var fName = firstNames[random.Next(firstNames.Length)];
                var lName = lastNames[random.Next(lastNames.Length)];
                
                var patientUser = new User
                {
                    FullName = $"{fName} {lName}",
                    PhoneNumber = $"010000000{i:D2}",
                    PasswordHash = passwordHash,
                    Role = UserRole.Patient,
                    Age = random.Next(18, 70),
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 200))
                };
                await context.Users.AddAsync(patientUser);
                await context.SaveChangesAsync();

                var patient = new Patient
                {
                    UserId = patientUser.Id,
                    Address = areas[random.Next(areas.Length)] + "، " + governments[random.Next(governments.Length)],
                    BloodType = new[] { "A+", "B+", "O+", "AB+", "O-" }[random.Next(5)]
                };
                patients.Add(patient);
                await context.Patients.AddAsync(patient);
            }
            await context.SaveChangesAsync();

            // ================== Seed Appointments & Reviews ==================
            for (int i = 0; i < 150; i++)
            {
                var patient = patients[random.Next(patients.Count)];
                var doctor = doctors[random.Next(doctors.Count)];

                var status = new[] { AppointmentStatus.Completed, AppointmentStatus.Completed, AppointmentStatus.Completed, AppointmentStatus.Confirmed, AppointmentStatus.Cancelled }[random.Next(5)];
                var pastDate = DateTime.UtcNow.AddDays(-random.Next(1, 60));

                var appointment = new Appointment
                {
                    PatientId = patient.Id,
                    DoctorId = doctor.Id,
                    AppointmentDate = status == AppointmentStatus.Confirmed ? DateTime.UtcNow.AddDays(random.Next(1, 10)) : pastDate,
                    Status = status,
                    CreatedAt = pastDate.AddDays(-random.Next(1, 5))
                };
                await context.Appointments.AddAsync(appointment);
                await context.SaveChangesAsync();

                // Add Review if completed
                if (status == AppointmentStatus.Completed && random.Next(2) == 0) // 50% chance to leave a review
                {
                    await context.Reviews.AddAsync(new Review
                    {
                        AppointmentId = appointment.Id,
                        PatientId = patient.Id,
                        DoctorId = doctor.Id,
                        Rating = random.Next(3, 6),
                        Comment = "دكتور ممتاز ومحترم جداً والعيادة نظيفة",
                        CreatedAt = pastDate.AddDays(1)
                    });
                }
            }
            await context.SaveChangesAsync();

            // ================== Seed Community Posts & Comments ==================
            for (int i = 0; i < 30; i++)
            {
                var isDoctor = random.Next(2) == 0;
                var posterId = isDoctor ? doctors[random.Next(doctors.Count)].UserId : patients[random.Next(patients.Count)].UserId;

                var post = new CommunityPost
                {
                    UserId = posterId,
                    Content = isDoctor ? "نصيحة طبية: شرب الماء بكميات كافية يحسن من وظائف الكلى." : "عندي ألم في المفاصل، هل هناك دكتور متخصص يقدر يفيدني؟",
                    Specialization = isDoctor ? "نصائح عامة" : "استشارة طبية",
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
                };
                await context.CommunityPosts.AddAsync(post);
                await context.SaveChangesAsync();

                // Add Comments
                int numComments = random.Next(0, 4);
                for (int j = 0; j < numComments; j++)
                {
                    var commenterId = doctors[random.Next(doctors.Count)].UserId; // mostly doctors answering
                    await context.CommunityComments.AddAsync(new CommunityComment
                    {
                        PostId = post.Id,
                        UserId = commenterId,
                        Content = "أنصحك بزيارة العيادة للفحص الدقيق.",
                        CreatedAt = post.CreatedAt.AddHours(random.Next(1, 48))
                    });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
