# Medicare API — Complete Frontend Documentation

> **Generated from:** `medicare-backend` source code (Controllers, DTOs, Models, Enums)  
> **Base URL:** `http://localhost:5002` (dev) / VPS production URL  
> **Swagger UI:** `http://localhost:5002/`  
> **Swagger JSON:** `http://localhost:5002/swagger/v1/swagger.json`

---

## Table of Contents

1. [Response Format](#1-response-format)
2. [Authentication & Authorization](#2-authentication--authorization)
3. [Enums (send as integer values)](#3-enums-send-as-integer-values)
4. [TypeScript Interfaces](#4-typescript-interfaces)
5. [Endpoints by Controller](#5-endpoints-by-controller)
6. [File Uploads](#6-file-uploads)
7. [Rate Limits](#7-rate-limits)
8. [CORS & HTTPS](#8-cors--https)
9. [Quick Reference: Role-Based Access Matrix](#9-quick-reference-role-based-access-matrix)

---

## 1. Response Format

Every endpoint returns the same wrapper. **Always check `isSuccess` before accessing `data`.**

```typescript
interface ApiResponse<T> {
  isSuccess: boolean;
  message: string;           // Often Arabic
  data: T | null;
  errors: string[] | null;   // Validation errors
  statusCode: number;
}
```

**Error behavior:**
- `400` — Validation errors (check `errors` array)
- `401` — Unauthorized / token expired → try refresh
- `403` — Forbidden (wrong role)
- `429` — Rate limited
- `500` — Server error

---

## 2. Authentication & Authorization

### JWT Config

| Setting | Value |
|---|---|
| Access token lifetime | **1 hour** |
| Refresh token lifetime | **30 days** |
| Auth header | `Authorization: Bearer <token>` |

### Token Claims

| Claim | Value |
|---|---|
| `nameidentifier` | `user.Id` (int) |
| `mobilephone` | Phone number |
| `name` | Full name |
| `role` | `"Patient"`, `"Doctor"`, or `"ClinicAdmin"` |
| `userId` | `user.Id` (duplicate) |

### Auth Flow

```
1. Login → store token + refreshToken + role + profileId
2. Every API call → attach Authorization: Bearer <token>
3. On 401 → call /api/auth/refresh-token → get new token pair → retry original request
4. If refresh also 401 → redirect to login
```

**Important:** Refresh token rotation — every refresh returns a **new** access token AND a **new** refresh token. The old refresh token is revoked and cannot be reused.

### Auth Endpoints

| Method | Path | Auth | Request Body | Response `data` |
|---|---|---|---|---|
| `POST` | `/api/auth/login` | ❌ | `LoginDto` | `AuthResponseDto` |
| `POST` | `/api/auth/register/patient` | ❌ | `RegisterPatientDto` | `AuthResponseDto` |
| `POST` | `/api/auth/register/clinic` | ❌ | `RegisterClinicDto` | `AuthResponseDto` |
| `POST` | `/api/auth/register/doctor` | ❌ | `RegisterDoctorDto` | `AuthResponseDto` |
| `POST` | `/api/auth/refresh-token` | ❌ | `RefreshTokenRequestDto` | `AuthResponseDto` |
| `POST` | `/api/auth/logout` | ✅ | `LogoutRequestDto` | `null` |
| `POST` | `/api/auth/forgot-password` | ❌ | `ForgotPasswordDto` | `null` |
| `POST` | `/api/auth/verify-otp` | ❌ | `VerifyOtpDto` | `null` |
| `POST` | `/api/auth/reset-password` | ❌ | `ResetPasswordDto` | `null` |
| `POST` | `/api/auth/social-login` | ❌ | `SocialLoginDto` | **501 Not Implemented** |
| `POST` | `/api/auth/telegram-register` | ❌ | `RegisterTelegramDto` | `null` |

---

## 3. Enums (send as integer values)

**Backend expects integer values for enums in JSON, NOT strings.**

```typescript
enum UserRole {
  Patient = 0,
  Doctor = 1,
  ClinicAdmin = 2,
}

enum Gender {
  Male = 0,
  Female = 1,
}

enum AppointmentStatus {
  Pending = 0,
  Confirmed = 1,
  InProgress = 2,
  Completed = 3,
  Cancelled = 4,
  NoShow = 5,
}

enum QueueStatus {
  Waiting = 0,
  InConsultation = 1,
  Completed = 2,
  Refunded = 3,
}

enum RefundStatus {
  None = 0,
  Pending = 1,
  Processed = 2,
}

enum PaymentMethod {
  Cash = 0,
  Online = 1,
}

enum PaymentStatus {
  Pending = 0,
  Paid = 1,
  Refunded = 2,
}

enum RelationType {
  Parent = 0,
  Child = 1,
  Spouse = 2,
  Sibling = 3,
  Other = 4,
}

enum DayOfWeek {
  Sunday = 0,
  Monday = 1,
  Tuesday = 2,
  Wednesday = 3,
  Thursday = 4,
  Friday = 5,
  Saturday = 6,
}
```

---

## 4. TypeScript Interfaces

### 4.1 Auth DTOs

```typescript
interface LoginDto {
  phone: string;      // max 20
  password: string;
}

interface RegisterPatientDto {
  name: string;       // max 100
  phone: string;      // max 20
  age: number;
  password: string; // min 6
  confirmPassword: string;
}

interface RegisterClinicDto {
  clinicName: string;      // max 200
  linkMap?: string;        // max 500
  government: string;      // max 100
  area: string;            // max 100
  address?: string;        // max 300
  phone: string;           // max 20
  email?: string;          // max 100, email format
  latitude?: number;
  longitude?: number;
  password: string;        // min 6
  confirmPassword: string;
  openingTime?: string;     // "HH:mm:ss"
  closingTime?: string;     // "HH:mm:ss"
  licenseFileUrl: string;  // max 500
}

interface RegisterDoctorDto {
  name: string;            // max 100
  phone: string;           // max 20
  password: string;        // min 6
  confirmPassword: string;
  specialization: string;  // max 100
  licenseFileUrl: string;  // max 500
}

interface ForgotPasswordDto {
  phone: string; // max 20
}

interface VerifyOtpDto {
  phone: string;   // max 20
  otpCode: string; // max 6
}

interface ResetPasswordDto {
  phone: string;       // max 20
  otpCode: string;     // max 6
  newPassword: string; // min 6
  confirmPassword: string;
}

interface SocialLoginDto {
  provider: string;    // "Google", "Apple", "Facebook"
  accessToken: string;
}

interface RefreshTokenRequestDto {
  refreshToken: string;
}

interface LogoutRequestDto {
  refreshToken?: string; // max 2000
}

interface RegisterTelegramDto {
  phoneNumber: string;
  telegramChatId: string;
}

interface AuthResponseDto {
  userId: number;
  fullName: string;
  phone: string;
  role: UserRole; // integer 0/1/2
  token: string;
  tokenExpiration: string; // ISO 8601
  refreshToken: string;
  refreshTokenExpiration: string; // ISO 8601
  profileId: number | null;
}
```

### 4.2 Appointment DTOs

```typescript
interface CreateAppointmentDto {
  doctorId: number;
  appointmentDate: string; // ISO 8601 date
  startTime: string;       // "HH:mm:ss"
  notes?: string;          // max 500
  familyMemberId?: number;
}

interface ClinicCreateAppointmentDto {
  doctorId: number;
  appointmentDate: string; // ISO 8601
  startTime: string;       // "HH:mm:ss"
  patientId?: number;
  offlinePatientName?: string;  // max 100
  offlinePatientPhone?: string; // max 20
  notes?: string;          // max 500
  isEmergency: boolean;    // default false
  chiefComplaint?: string; // max 500
  isPaid: boolean;         // default false
  paymentMethod: PaymentMethod; // default Cash (0)
  offlinePatientAge?: number;
  offlinePatientGender?: Gender;
}

interface AppointmentDto {
  id: number;
  patientId: number | null;
  patientName: string;
  familyMemberId: number | null;
  familyMemberName: string | null;
  offlinePatientPhone: string | null;
  doctorId: number;
  doctorName: string;
  specialization: string;
  appointmentDate: string;      // ISO 8601
  startTime: string;              // "HH:mm:ss"
  endTime: string | null;         // "HH:mm:ss"
  status: number;                 // AppointmentStatus enum
  statusText: string;             // Arabic display
  queueNumber: number | null;
  queueStatus: number | null;   // QueueStatus enum
  refundStatus: number;           // RefundStatus enum
  refundStatusText: string;
  notes: string | null;
  cancellationReason: string | null;
  doctorProfileImageUrl: string | null;
  clinicId: number | null;
  clinicName: string | null;
  clinicAddress: string | null;
  currentServingNumber: number | null;
  isEmergency: boolean;
  chiefComplaint: string | null;
  isPaid: boolean;
  paymentStatus: number;          // PaymentStatus enum (0=Pending, 1=Paid, 2=Refunded)
  paymentStatusText: string;
  consultationFee: number;        // frozen at booking time
  paymentMethod: number;          // PaymentMethod enum
  paymentMethodText: string;
  offlinePatientAge: number | null;
  offlinePatientGender: number | null; // Gender enum
  createdAt: string;              // ISO 8601
}

interface CancelAppointmentDto {
  reason: string; // max 500, required
}

interface UpdateAppointmentStatusDto {
  status: number; // AppointmentStatus enum, required
}

interface RescheduleAppointmentDto {
  appointmentDate: string; // ISO 8601, required
  startTime: string;     // "HH:mm:ss", required
}

interface LiveQueueTrackerDto {
  appointmentId: number;
  myQueueNumber: number;
  currentServingNumber: number;
  patientsAheadOfMe: number;
  estimatedWaitTimeMinutes: number;
  myQueueStatus: number | null; // QueueStatus enum
  doctorName: string;
}

interface ClinicDashboardOverviewDto {
  paidCount: number;
  walkInCount: number;
  todayRevenueAmount: number;
}

interface PaymentsDashboardDto {
  totalRevenue: number;
  revenueGrowthText: string; // e.g. "+12% vs last month"
  cashAmount: number;
  cashPercentage: number;
  onlineAmount: number;
  onlinePercentage: number;
  refundsAmount: number;
  refundsPercentage: number;
  recentTransactions: TransactionDto[];
}

interface TransactionDto {
  appointmentId: number;
  patientName: string;
  dateTime: string; // ISO 8601
  amount: number;
  status: string; // "Paid", "Pending", "Refunded"
  paymentMethod: number; // PaymentMethod enum
  paymentMethodText: string;
}
```

### 4.3 Doctor DTOs

```typescript
interface DoctorProfileDto {
  id: number;
  userId: number;
  fullName: string;
  phoneNumber: string;
  email?: string;
  profileImageUrl?: string;
  specialization: string;
  licenseNumber?: string;
  licenseImageUrl?: string;
  yearsOfExperience: number;
  bio?: string;
  consultationFee: number;
  averageRating: number;
  totalReviews: number;
  isAvailable: boolean;
  clinicId?: number;
  clinicName?: string;
  clinicLatitude?: number;
  clinicLongitude?: number;
  degree?: string;
  university?: string;
  subSpecialty?: string;
  graduationYear?: number;
  boardCertification?: string;
  languages: string[];
  associatedClinics: string[];
  qrCodeKey?: string;
  totalPatients: number;
}

interface UpdateDoctorProfileDto {
  fullName?: string;
  phoneNumber?: string;
  email?: string;
  specialization?: string;
  yearsOfExperience?: number;
  bio?: string;
  consultationFee?: number;
  isAvailable?: boolean;
  profileImageUrl?: string;
  licenseImageUrl?: string;
  degree?: string;
  university?: string;
  subSpecialty?: string;
  graduationYear?: number;
  boardCertification?: string;
  languages?: string[];
}

interface DoctorListItemDto {
  id: number;
  fullName: string;
  specialization: string;
  profileImageUrl: string | null;
  consultationFee: number;
  averageRating: number;
  totalReviews: number;
  isAvailable: boolean;
  clinicName: string | null;
  clinicArea: string | null;
  isFavorited: boolean;
  latitude: number | null;
  longitude: number | null;
  distanceKm: number | null; // populated when ?userLat&userLng provided
}

interface NearbyDoctorDto extends DoctorListItemDto {
  distanceKm: number;              // always populated
  clinicIdForLocation: number | null;
}

interface ClinicDoctorDetailsDto {
  doctorId: number;
  fullName: string;
  specialization: string;
  profileImageUrl: string | null;
  consultationFee: number;
  isAvailable: boolean;
  internalNotes: string | null;
  schedules: DoctorScheduleDto[];
}

interface DoctorScheduleDto {
  id: number;
  doctorId: number;
  dayOfWeek: number; // DayOfWeek enum
  dayName: string;
  startTime: string; // "HH:mm:ss"
  endTime: string;   // "HH:mm:ss"
  breakStartTime?: string;
  breakEndTime?: string;
  slotDurationMinutes: number;
  maxPatients: number;
  isActive: boolean;
}

interface UpdateClinicDoctorDto {
  doctorId: number;
  qrCodeKey?: string;
  consultationFee: number;
  isAvailable: boolean; // default true
  internalNotes?: string;
  schedules: DoctorScheduleDto[];
}

interface ScannedDoctorDto {
  doctorId: number;
  fullName: string;
  specialization: string;
  profileImageUrl: string | null;
  yearsOfExperience: number;
  bio: string | null;
  defaultConsultationFee: number;
  isAlreadyRegisteredInClinic: boolean;
  qrCodeKey: string | null;
}

interface DoctorDashboardDto {
  totalAppointments: number;
  newPatientsCount: number;
  followUpsCount: number;
  walkInsCount: number;
  onlineCount: number;
  todayEarnings: number;
  waitingCount: number;
  withDoctorCount: number;
  completedCount: number;
}

interface PatientHistoryDto {
  patientId: number;
  fullName: string;
  profileImageUrl: string | null;
  age: number;
  gender: string | null;
  bloodType: string | null;
  chronicConditions: string[];
  currentMedications: string[];
  pastRecords: MedicalRecordDto[];
}
```

### 4.4 Patient DTOs

```typescript
interface PatientProfileDto {
  id: number;
  userId: number;
  fullName: string;
  phoneNumber: string;
  email?: string;
  gender?: number; // Gender enum
  age?: number;
  dateOfBirth?: string; // ISO 8601
  profileImageUrl?: string;
  address?: string;
  bloodType?: string;
  allergies?: string;
  chronicDiseases?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
}

interface UpdatePatientProfileDto {
  fullName?: string;
  phoneNumber?: string;
  email?: string;
  gender?: number; // Gender enum
  age?: number;
  dateOfBirth?: string; // ISO 8601
  address?: string;
  bloodType?: string;
  allergies?: string;
  chronicDiseases?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  profileImageUrl?: string;
}

interface CreateFamilyMemberDto {
  name: string;        // max 100, required
  relation: number;    // RelationType enum, required
  age: number;         // required
  gender: number;      // Gender enum, required
  bloodType?: string;  // max 50
  medicalHistory?: string; // max 500
  allergies?: string;  // max 200
  chronicDiseases?: string; // max 200
}

interface FamilyMemberDto {
  id: number;
  patientId: number;
  name: string;
  relation: number; // RelationType enum
  age: number;
  gender: number; // Gender enum
  bloodType: string | null;
  medicalHistory: string | null;
  allergies: string | null;
  chronicDiseases: string | null;
}
```

### 4.5 Clinic DTOs

```typescript
interface ClinicDto {
  id: number;
  name: string;
  facilityId?: string;
  description?: string;
  government?: string;
  area?: string;
  address?: string;
  linkMap?: string;
  phoneNumber?: string;
  email?: string;
  logoUrl?: string;
  licenseImageUrl?: string;
  latitude?: number;
  longitude?: number;
  openingTime?: string | null; // "HH:mm:ss" or null
  closingTime?: string | null; // "HH:mm:ss" or null
  isActive: boolean;
  doctorsCount: number;
}

interface CreateClinicDto {
  name: string;         // max 200, required
  facilityId?: string;  // max 100
  description?: string; // max 500
  government: string;   // max 100, required
  area: string;         // max 100, required
  address?: string;     // max 300
  linkMap?: string;     // max 500
  phoneNumber?: string; // max 20
  email?: string;       // max 100, email format
  latitude?: number;
  longitude?: number;
  openingTime?: string; // "HH:mm:ss"
  closingTime?: string; // "HH:mm:ss"
}

interface UpdateClinicDto {
  name?: string;        // max 200
  facilityId?: string;  // max 100
  description?: string; // max 500
  government?: string;  // max 100
  area?: string;        // max 100
  address?: string;     // max 300
  linkMap?: string;     // max 500
  phoneNumber?: string; // max 20
  email?: string;       // max 100, email format
  latitude?: number;
  longitude?: number;
  openingTime?: string; // "HH:mm:ss"
  closingTime?: string; // "HH:mm:ss"
}

interface NearbyClinicDto extends ClinicDto {
  distanceKm: number;
  matchingDoctorsCount: number;
}
```

### 4.6 Medical Record DTOs

```typescript
interface PrescribedMedicationDto {
  name: string;
  category: string;
  dosage: string;
  duration: string;
}

interface MedicalRecordDto {
  id: number;
  patientId: number;
  patientName: string;
  doctorId: number;
  doctorName: string;
  doctorSpecialization: string;
  doctorProfileImageUrl: string;
  appointmentId?: number;
  diagnosis: string;
  prescription?: string;
  instructions?: string;
  familyMemberId?: number;
  familyMemberName?: string;
  treatmentPlan?: string;
  notes?: string;
  symptoms?: string;
  subjective?: string;
  objective?: string;
  assessment?: string;
  plan?: string;
  bloodPressure?: string;
  heartRate?: string;
  weight?: string;
  medications?: PrescribedMedicationDto[];
  observations?: string;
  recommendedCare?: string[];
  visitDate: string; // ISO 8601
  createdAt: string; // ISO 8601
}

interface CreateMedicalRecordDto {
  patientId: number; // required
  appointmentId?: number;
  diagnosis: string; // max 500, required
  prescription?: string; // max 1000
  instructions?: string; // max 1000
  familyMemberId?: number;
  treatmentPlan?: string; // max 1000
  notes?: string; // max 1000
  symptoms?: string; // max 500
  subjective?: string; // max 1000
  objective?: string; // max 1000
  assessment?: string; // max 1000
  plan?: string; // max 1000
  bloodPressure?: string; // max 50
  heartRate?: string; // max 50
  weight?: string; // max 50
  medications?: PrescribedMedicationDto[];
  observations?: string;
  recommendedCare?: string[];
}

### Consultation DTOs

```typescript
interface CompleteConsultationDto {
  diagnosis: string;        // max 500, required
  medications?: PrescribedMedicationDto[];
  instructions?: string;    // max 1000
}

interface ConsultationPatientDto {
  patientId: number | null;
  familyMemberId: number | null;
  fullName: string;
  profileImageUrl: string | null;
  age: number;
  gender: string | null;
  bloodType: string | null;
  chronicConditions: string[];
  allergies: string[];
  isFamilyMember: boolean;
}

interface PreviousVisitDto {
  appointmentId: number;
  visitDate: string;        // ISO 8601
  doctorName: string;
  diagnosis: string | null;
  chiefComplaint: string | null;
}

interface ConsultationScreenDto {
  appointment: AppointmentDto;
  patient: ConsultationPatientDto;
  medicalHistory: MedicalRecordDto[];
  previousVisits: PreviousVisitDto[];
  previousDiagnoses: string[];
  previousPrescriptions: PrescribedMedicationDto[];
}
```

---

## 5. Endpoints by Controller

### 5.1 AuthController (`/api/auth`)

| Method | Path | Auth | Body | Response `data` |
|---|---|---|---|---|
| `POST` | `/api/auth/register/patient` | ❌ | `RegisterPatientDto` | `AuthResponseDto` |
| `POST` | `/api/auth/register/clinic` | ❌ | `RegisterClinicDto` | `AuthResponseDto` |
| `POST` | `/api/auth/register/doctor` | ❌ | `RegisterDoctorDto` | `AuthResponseDto` |
| `POST` | `/api/auth/login` | ❌ | `LoginDto` | `AuthResponseDto` |
| `POST` | `/api/auth/refresh-token` | ❌ | `RefreshTokenRequestDto` | `AuthResponseDto` |
| `POST` | `/api/auth/logout` | ✅ | `LogoutRequestDto` | `null` |
| `POST` | `/api/auth/forgot-password` | ❌ | `ForgotPasswordDto` | `null` |
| `POST` | `/api/auth/verify-otp` | ❌ | `VerifyOtpDto` | `null` |
| `POST` | `/api/auth/reset-password` | ❌ | `ResetPasswordDto` | `null` |
| `POST` | `/api/auth/social-login` | ❌ | `SocialLoginDto` | **501** |
| `POST` | `/api/auth/telegram-register` | ❌ | `RegisterTelegramDto` | `null` |

### 5.2 AppointmentController (`/api/appointment`)

| Method | Path | Auth | Body / Query | Response `data` |
|---|---|---|---|---|
| `POST` | `/api/appointment` | ✅ Patient | `CreateAppointmentDto` | `AppointmentDto` |
| `POST` | `/api/appointment/clinic-booking` | ✅ ClinicAdmin,Doctor | `ClinicCreateAppointmentDto` | `AppointmentDto` |
| `GET` | `/api/appointment/patient` | ✅ Patient | `?filter` (upcoming\|completed\|cancelled), `?status` | `AppointmentDto[]` |
| `GET` | `/api/appointment/doctor` | ✅ Doctor | `?date`, `?status` | `AppointmentDto[]` |
| `GET` | `/api/appointment/queue/today` | ✅ Doctor | — | `AppointmentDto[]` |
| `GET` | `/api/appointment/{id}` | ✅ | — | `AppointmentDto` |
| `PUT` | `/api/appointment/{id}/cancel` | ✅ | `CancelAppointmentDto` | `AppointmentDto` |
| `PUT` | `/api/appointment/{id}/reschedule` | ✅ | `RescheduleAppointmentDto` | `AppointmentDto` |
| `PUT` | `/api/appointment/{id}/status` | ✅ Doctor | `UpdateAppointmentStatusDto` | `AppointmentDto` |
| `POST` | `/api/appointment/queue/call-next` | ✅ Doctor | — | `AppointmentDto` |
| `GET` | `/api/appointment/queue/tracker/{id}` | ✅ Patient | — | `LiveQueueTrackerDto` |
| `GET` | `/api/appointment/clinic/dashboard` | ✅ ClinicAdmin | `?doctorId` | `ClinicDashboardOverviewDto` |
| `GET` | `/api/appointment/clinic/queue` | ✅ ClinicAdmin | `?doctorId` (required) | `AppointmentDto[]` |
| `POST` | `/api/appointment/{id}/start-checkup` | ✅ ClinicAdmin | — | `AppointmentDto` |
| `GET` | `/api/appointment/clinic/payments-dashboard` | ✅ ClinicAdmin | `?doctorId`, `?timeframe` (today\|week\|month) | `PaymentsDashboardDto` |

### 5.3 DoctorController (`/api/doctor`)

| Method | Path | Auth | Query | Response `data` |
|---|---|---|---|---|
| `GET` | `/api/doctor` | ❌ | `?specialization`, `?search`, `?government`, `?area`, `?appointmentDay`, `?gender`, `?minFee`, `?maxFee`, `?minRating`, `?userLat`, `?userLng` | `DoctorListItemDto[]` |
| `GET` | `/api/doctor/nearby` | ❌ | `?lat` (req), `?lng` (req), `?radiusKm` (default 5), `?specialization`, `?search` | `NearbyDoctorDto[]` |
| `GET` | `/api/doctor/specializations` | ❌ | — | `string[]` |
| `GET` | `/api/doctor/popular` | ❌ | `?userLat`, `?userLng` | `DoctorListItemDto[]` |
| `GET` | `/api/doctor/{id}` | ❌ | — | `DoctorProfileDto` |
| `GET` | `/api/doctor/profile` | ✅ Doctor | — | `DoctorProfileDto` |
| `PUT` | `/api/doctor/profile` | ✅ Doctor | `UpdateDoctorProfileDto` | `DoctorProfileDto` |
| `GET` | `/api/doctor/{id}/schedules` | ❌ | — | `DoctorScheduleDto[]` |
| `POST` | `/api/doctor/{doctorId}/schedules` | ✅ ClinicAdmin | `CreateScheduleDto` | `DoctorScheduleDto` |
| `DELETE` | `/api/doctor/schedules/{scheduleId}` | ✅ ClinicAdmin | — | `boolean` |
| `GET` | `/api/doctor/{id}/available-slots` | ❌ | `?date` (required) | `AvailableSlotDto[]` |
| `GET` | `/api/doctor/dashboard` | ✅ Doctor | — | `DoctorDashboardDto` |
| `GET` | `/api/doctor/live-queue` | ✅ Doctor | `?status` | `AppointmentDto[]` |
| `GET` | `/api/doctor/qr-code` | ✅ Doctor | — | `{ qrCodeKey: string }` |
| `GET` | `/api/doctor/patients/{patientId}/history` | ✅ Doctor | — | `PatientHistoryDto` |
| `POST` | `/api/doctor/session/{appointmentId}` | ✅ Doctor | `CreateMedicalRecordDto` | `MedicalRecordDto` |
| `GET` | `/api/doctor/consultation/{appointmentId}` | ✅ Doctor | — | `ConsultationScreenDto` |
| `GET` | `/api/doctor/active-consultations` | ✅ Doctor | — | `AppointmentDto[]` |
| `POST` | `/api/doctor/consultation/{appointmentId}/complete` | ✅ Doctor | `CompleteConsultationDto` | `MedicalRecordDto` |

### 5.4 PatientController (`/api/patient`)

| Method | Path | Auth | Body | Response `data` |
|---|---|---|---|---|
| `GET` | `/api/patient/profile` | ✅ Patient | — | `PatientProfileDto` |
| `PUT` | `/api/patient/profile` | ✅ Patient | `UpdatePatientProfileDto` | `PatientProfileDto` |
| `POST` | `/api/patient/favorite/{doctorId}` | ✅ Patient | — | `boolean` (is now favorited) |
| `GET` | `/api/patient/favorites` | ✅ Patient | — | `DoctorListItemDto[]` |
| `GET` | `/api/patient/family-members` | ✅ Patient | — | `FamilyMemberDto[]` |
| `POST` | `/api/patient/family-members` | ✅ Patient | `CreateFamilyMemberDto` | `FamilyMemberDto` |
| `DELETE` | `/api/patient/family-members/{memberId}` | ✅ Patient | — | `null` |
| `GET` | `/api/patient/search` | ✅ ClinicAdmin,Doctor | `?query` (required) | `PatientProfileDto[]` |

### 5.5 ClinicController (`/api/clinic`)

| Method | Path | Auth | Body | Response `data` |
|---|---|---|---|---|
| `GET` | `/api/clinic` | ❌ | `?search` | `ClinicDto[]` |
| `GET` | `/api/clinic/nearby` | ❌ | `?lat` (req), `?lng` (req), `?radiusKm` (default 5), `?specialization`, `?search` | `NearbyClinicDto[]` |
| `GET` | `/api/clinic/{id}` | ❌ | — | `ClinicDto` |
| `POST` | `/api/clinic` | ✅ ClinicAdmin | `CreateClinicDto` | `ClinicDto` |
| `PUT` | `/api/clinic/{id}` | ✅ ClinicAdmin | `UpdateClinicDto` | `ClinicDto` |
| `GET` | `/api/clinic/doctors` | ✅ ClinicAdmin | — | `ClinicDoctorDetailsDto[]` |
| `GET` | `/api/clinic/doctors/scan/{qrCodeKey}` | ✅ ClinicAdmin | — | `ScannedDoctorDto` |
| `POST` | `/api/clinic/doctors/register` | ✅ ClinicAdmin | `UpdateClinicDoctorDto` | `ClinicDoctorDetailsDto` |
| `GET` | `/api/clinic/doctors/{doctorId}` | ✅ ClinicAdmin | — | `ClinicDoctorDetailsDto` |
| `PUT` | `/api/clinic/doctors/{doctorId}` | ✅ ClinicAdmin | `UpdateClinicDoctorDto` | `ClinicDoctorDetailsDto` |
| `DELETE` | `/api/clinic/doctors/{doctorId}` | ✅ ClinicAdmin | — | `null` |
| `GET` | `/api/clinic/profile` | ✅ ClinicAdmin | — | `ClinicDto` |
| `PUT` | `/api/clinic/profile` | ✅ ClinicAdmin | `UpdateClinicDto` | `ClinicDto` |

### 5.6 CommunityController (`/api/community`)

| Method | Path | Auth | Body | Response `data` |
|---|---|---|---|---|
| `GET` | `/api/community/posts` | ✅ | `?specialization`, `?search` | `CommunityPostDto[]` |
| `POST` | `/api/community/posts` | ✅ | `CreatePostDto` | `CommunityPostDto` |
| `GET` | `/api/community/posts/{postId}/comments` | ✅ | — | `CommunityCommentDto[]` |
| `POST` | `/api/community/posts/{postId}/comments` | ✅ | `CreateCommentDto` | `CommunityCommentDto` |
| `DELETE` | `/api/community/posts/{id}` | ✅ | — | `null` |
| `DELETE` | `/api/community/comments/{id}` | ✅ | — | `null` |

### 5.7 MedicalRecordController (`/api/medicalrecord`)

| Method | Path | Auth | Body | Response `data` |
|---|---|---|---|---|
| `POST` | `/api/medicalrecord` | ✅ Doctor | `CreateMedicalRecordDto` | `MedicalRecordDto` |
| `GET` | `/api/medicalrecord/patient/{patientId}` | ✅ Doctor,Patient | — | `MedicalRecordDto[]` |
| `GET` | `/api/medicalrecord/{id}` | ✅ Doctor,Patient | — | `MedicalRecordDto` |

### 5.8 NotificationController (`/api/notification`)

| Method | Path | Auth | Response `data` |
|---|---|---|---|
| `GET` | `/api/notification` | ✅ | `NotificationDto[]` |
| `GET` | `/api/notification/unread-count` | ✅ | `NotificationUnreadCountDto` |
| `PUT` | `/api/notification/{id}/read` | ✅ | `NotificationDto` |
| `DELETE` | `/api/notification/{id}` | ✅ | `null` |

### 5.9 ReviewController (`/api/review`)

| Method | Path | Auth | Body | Response `data` |
|---|---|---|---|---|
| `POST` | `/api/review` | ✅ Patient | `CreateReviewDto` | `ReviewDto` |
| `GET` | `/api/review/doctor/{doctorId}` | ❌ | — | `ReviewDto[]` |

### 5.10 InfoController (`/api/info`)

| Method | Path | Auth | Response `data` |
|---|---|---|---|
| `GET` | `/api/info/version` | ❌ | `{ version: string }` |

### 5.11 UploadController (`/api/upload`)

| Method | Path | Auth | Body | Response `data` |
|---|---|---|---|---|
| `POST` | `/api/upload/license` | ❌ | `multipart/form-data`, field `file` | `{ url: string }` |
| `POST` | `/api/upload/profile-image` | ✅ | `multipart/form-data`, field `file` | `{ url: string }` |

---

## 6. File Uploads

### License Upload (`POST /api/upload/license`)
- **Field name:** `file`
- **Max size:** 5 MB
- **Allowed formats:** `.jpg`, `.jpeg`, `.png`, `.pdf`
- **Response:** `{ url: "/uploads/licenses/{guid}.ext" }`
- **Note:** `AllowAnonymous` — no auth required

### Profile Image Upload (`POST /api/upload/profile-image`)
- **Field name:** `file`
- **Max size:** 3 MB
- **Allowed formats:** `.jpg`, `.jpeg`, `.png`
- **Response:** `{ url: "/uploads/profiles/{guid}.ext" }`
- **Note:** Requires authentication

**Usage:** Pass the returned `url` string into DTOs like `licenseFileUrl`, `profileImageUrl`, etc.

---

## 7. Rate Limits

| Policy | Limit | Window | Applies To |
|---|---|---|---|
| `AuthRateLimit` | 5 req | 1 minute | `/api/auth/*` |
| `GeneralRateLimit` | 100 req | 1 minute | Everything else |

Exceeding returns HTTP **429**.

---

## 8. CORS & HTTPS

- **CORS:** Fully open — `AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`. No CORS issues expected.
- **HTTPS:** `UseHttpsRedirection()` is enabled. Plain HTTP requests to root redirect to HTTPS with 301. For development, hit specific endpoints directly or configure `ASPNETCORE_URLS` with HTTPS.

---

## 9. Quick Reference: Role-Based Access Matrix

### Public (no auth)
- `POST /api/auth/*` (except logout)
- `GET /api/info/version`
- `GET /api/doctor`
- `GET /api/doctor/nearby`
- `GET /api/doctor/specializations`
- `GET /api/doctor/popular`
- `GET /api/doctor/{id}`
- `GET /api/doctor/{id}/schedules`
- `GET /api/doctor/{id}/available-slots`
- `GET /api/clinic`
- `GET /api/clinic/nearby`
- `GET /api/clinic/{id}`
- `GET /api/review/doctor/{doctorId}`
- `POST /api/upload/license`

### Patient only
- `POST /api/auth/logout`
- `GET|PUT /api/patient/profile`
- `POST /api/patient/favorite/{doctorId}`
- `GET /api/patient/favorites`
- `GET|POST /api/patient/family-members`
- `DELETE /api/patient/family-members/{id}`
- `POST /api/appointment`
- `GET /api/appointment/patient`
- `PUT /api/appointment/{id}/cancel`
- `PUT /api/appointment/{id}/reschedule`
- `GET /api/appointment/queue/tracker/{id}`
- `POST /api/review`
- `GET /api/medicalrecord/patient/{patientId}`
- `GET /api/medicalrecord/{id}`

### Doctor only
- `POST /api/auth/logout`
- `GET|PUT /api/doctor/profile`
- `GET /api/doctor/dashboard`
- `GET /api/doctor/live-queue`
- `GET /api/doctor/qr-code`
- `GET /api/doctor/patients/{patientId}/history`
- `POST /api/doctor/session/{appointmentId}`
- `GET /api/doctor/consultation/{appointmentId}`
- `GET /api/doctor/active-consultations`
- `POST /api/doctor/consultation/{appointmentId}/complete`
- `GET /api/appointment/doctor`
- `GET /api/appointment/queue/today`
- `PUT /api/appointment/{id}/status`
- `POST /api/appointment/queue/call-next`
- `POST /api/appointment/clinic-booking`
- `GET /api/patient/search`
- `GET /api/medicalrecord/patient/{patientId}`
- `GET /api/medicalrecord/{id}`
- `POST /api/medicalrecord`

### ClinicAdmin only
- `POST /api/auth/logout`
- `GET|PUT /api/clinic/profile`
- `POST /api/clinic`
- `PUT /api/clinic/{id}`
- `GET /api/clinic/doctors`
- `GET /api/clinic/doctors/scan/{qrCodeKey}`
- `POST /api/clinic/doctors/register`
- `GET /api/clinic/doctors/{doctorId}`
- `PUT /api/clinic/doctors/{doctorId}`
- `DELETE /api/clinic/doctors/{doctorId}`
- `POST /api/doctor/{doctorId}/schedules`
- `POST /api/appointment/clinic-booking`
- `GET /api/appointment/clinic/dashboard`
- `GET /api/appointment/clinic/queue`
- `POST /api/appointment/{id}/start-checkup`
- `GET /api/appointment/clinic/payments-dashboard`
- `GET /api/patient/search`

### All authenticated roles
- `GET /api/community/posts`
- `POST /api/community/posts`
- `GET /api/community/posts/{id}/comments`
- `POST /api/community/posts/{id}/comments`
- `DELETE /api/community/posts/{id}` (own posts only)
- `DELETE /api/community/comments/{id}` (own comments only)
- `GET /api/notification`
- `GET /api/notification/unread-count`
- `PUT /api/notification/{id}/read`
- `DELETE /api/notification/{id}`
- `POST /api/upload/profile-image`

---

## Appendix: Type-Safety Notes for Frontend

1. **Enums are integers** — backend serializes enums as `int`. Send `0` for `Patient`, not `"Patient"`.
2. **TimeSpan fields** — sent/received as `"HH:mm:ss"` strings in JSON.
3. **DateTime fields** — ISO 8601 strings (e.g., `"2026-05-26T14:00:00Z"`).
4. **Nullable fields** — marked with `?` in C# become `T | null` in TypeScript.
5. **Response wrapper** — always unwrap `response.data` from the `ApiResponse<T>` envelope.
6. **Auth header** — include `Authorization: Bearer <token>` on every authenticated request.
7. **Token refresh** — on 401, call `/api/auth/refresh-token` with stored `refreshToken`, then retry.
8. **Multipart uploads** — use `FormData`, field name is exactly `"file"`.

---

*End of documentation — derived directly from source code for 100% accuracy.*
