# Changelog

All notable changes to the API that the frontend should be aware of.

## 2026-06-13

### Added

#### New endpoints

**`GET /api/info/version`** — returns the current backend version. Public, no auth required.

```jsonc
{
  "isSuccess": true,
  "data": { "version": "1.0.0" },
  "statusCode": 200
}
```

**`GET /api/doctor/consultation/{appointmentId}`** — load the full consultation screen for a specific appointment. Returns `ConsultationScreenDto`. Requires `Doctor` role.

**`GET /api/doctor/active-consultations`** — list the doctor's active (in-progress) consultations for today. Returns `AppointmentDto[]`. Requires `Doctor` role.

**`POST /api/doctor/consultation/{appointmentId}/complete`** — complete a consultation with diagnosis, medications, and instructions. Returns `MedicalRecordDto`. Requires `Doctor` role.

```jsonc
// CompleteConsultationDto
{
  "diagnosis": "Upper respiratory infection",
  "medications": [
    { "name": "Amoxicillin", "category": "Antibiotic", "dosage": "500mg twice daily", "duration": "7 days" }
  ],
  "instructions": "Rest and drink plenty of fluids"
}
```

#### New DTOs

**`ConsultationScreenDto`**:

```jsonc
{
  "appointment": { /* AppointmentDto */ },
  "patient": {
    "patientId": 1,          // null for walk-in/offline patients
    "familyMemberId": null,
    "fullName": "...",
    "profileImageUrl": "...",
    "age": 30,
    "gender": "Male",
    "bloodType": "O+",
    "chronicConditions": [],
    "allergies": [],
    "isFamilyMember": false
  },
  "medicalHistory": [ /* MedicalRecordDto[] */ ],
  "previousVisits": [
    { "appointmentId": 5, "visitDate": "2026-05-01T00:00:00Z", "doctorName": "...", "diagnosis": "...", "chiefComplaint": "..." }
  ],
  "previousDiagnoses": ["Asthma", "Hypertension"],
  "previousPrescriptions": [ /* PrescribedMedicationDto[] */ ]
}
```

#### New enum

**`PaymentStatus`** (send/receive as integer):

| Value | Name |
|---|---|
| 0 | Pending |
| 1 | Paid |
| 2 | Refunded |

#### New fields on existing responses

**`AppointmentDto`** now includes:

| Field | Type | Notes |
|---|---|---|
| `paymentStatus` | number | `PaymentStatus` enum (0/1/2) |
| `paymentStatusText` | string | e.g. `"Pending"`, `"Paid"`, `"Refunded"` |
| `consultationFee` | number | Fee frozen at booking time. Use this for revenue display, not `doctor.consultationFee`. |

**`MedicalRecordDto`** and **`CreateMedicalRecordDto`** now include:

| Field | Type | Notes |
|---|---|---|
| `instructions` | string? | Free-text instructions |
| `familyMemberId` | number? | Set when the record is for a family member |
| `familyMemberName` | string? | Display name (response only) |

### Changed

- **Appointment cancellation is now only allowed while the appointment is waiting.**
  - `PUT /api/appointment/{id}/cancel` returns `400` if the appointment is already `InProgress` / `Completed` / `InConsultation`.
  - On cancellation, refund status is immediately set to `Processed` and `paymentStatus` becomes `Refunded`.
- **`PUT /api/appointment/{id}/status` can no longer set status to `Completed`.** Use `POST /api/doctor/consultation/{appointmentId}/complete` instead. Attempting `status: Completed` returns `400`.
- **Queue ordering changed from `queueNumber` to `startTime`**. This affects live queue, call-next, clinic queue, and patient tracker calculations.
- **`POST /api/appointment/{id}/start-checkup` no longer auto-completes the previously in-consultation patient.** It only starts the selected appointment. It also now rejects already-started or non-today appointments.

### Fixed

- **Clinic registration** now persists `address`, `email`, `latitude`, and `longitude` (not just `government`, `area`, and `linkMap`).
- **Updating clinic doctor details** no longer wipes the doctor's schedule. `UpdateClinicDoctorAsync` is now decoupled from `RegisterClinicDoctorAsync`.

### Not changed

- Database migrations are included in this release. The production database has already been updated as part of the deployment.
- All previously-existing endpoints, query parameters, and response fields are unchanged unless noted above. New fields are additive.

## 2026-06-03

### Added

#### New endpoints

**`DELETE /api/notification/{id}`** — delete a notification. Only the notification owner can delete it. Available to all authenticated roles (Patient, Doctor, ClinicAdmin).

**`GET /api/patient/favorites`** — get the current patient's favorited doctors. Returns `DoctorListItemDto[]`. Requires `Patient` role.

#### New fields on existing responses

**`ClinicDto` / `CreateClinicDto` / `UpdateClinicDto`**:

| Field | Type | Notes |
|---|---|---|
| `openingTime` | string? (TimeSpan) | `"HH:mm:ss"` format or `null` |
| `closingTime` | string? (TimeSpan) | `"HH:mm:ss"` format or `null` |

Both fields are now exposed on `GET /api/clinic`, `GET /api/clinic/{id}`, `POST /api/clinic`, `PUT /api/clinic/{id}`, and `PUT /api/clinic/profile`. They map directly to the entity's `OpeningTime` / `ClosingTime` columns.

### Not changed

- No database schema changes. No migration needed — the entity already had these columns.
- All previously-existing endpoints, query parameters, and response fields are unchanged. New fields are additive.

## 2026-06-01

### Added

#### New endpoints

**`GET /api/clinic/nearby`** — geospatial clinic search for the map screen.

Query parameters (all optional unless noted):

| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `lat` | double | yes | — | Caller's latitude (-90 to 90) |
| `lng` | double | yes | — | Caller's longitude (-180 to 180) |
| `radiusKm` | double | no | 5 | Search radius in km |
| `specialization` | string | no | — | Filter to clinics with at least one doctor of this specialty |
| `search` | string | no | — | Name / government / area contains match |

Response is `NearbyClinicDto[]`, sorted ascending by `distanceKm`. Public (no auth required).

**`GET /api/doctor/nearby`** — geospatial doctor search for the map screen. Same query parameters as the clinic version. Returns `NearbyDoctorDto[]`, sorted ascending by `distanceKm`. Public.

Notes:
- Both endpoints only return items that have `latitude` AND `longitude` set on at least one active clinic. Items without coordinates are silently excluded.
- Invalid coordinates (e.g. `lat=999`) return an empty list with `200`, not an error.
- A doctor's location is taken from the first active clinic with coordinates. If a doctor works at multiple clinics, `clinicIdForLocation` tells you which one was used.

#### New fields on existing responses

**`GET /api/doctor/{id}`** (response: `DoctorProfileDto`):

| Field | Type | Notes |
|---|---|---|
| `clinicLatitude` | double? | Active clinic's latitude (null if no active clinic or clinic has no coords) |
| `clinicLongitude` | double? | Active clinic's longitude |
| `totalPatients` | int | Distinct registered patients with non-cancelled appointments. Excludes walk-ins. |

**`GET /api/doctor`** and **`GET /api/doctor/popular`** (response: `DoctorListItemDto`):

| Field | Type | Notes |
|---|---|---|
| `distanceKm` | double? | Haversine distance from caller to the doctor's active clinic. Null unless `userLat` + `userLng` were provided AND the doctor has a clinic with coordinates. |

#### New optional query parameters

**`GET /api/doctor`** now accepts:

| Name | Type | Description |
|---|---|---|
| `userLat` | double? | If both `userLat` and `userLng` are provided, results are sorted ascending by `distanceKm` and `distanceKm` is populated per item. |
| `userLng` | double? | Same. |

**`GET /api/doctor/popular`** now accepts the same `userLat` / `userLng` pair. Results stay sorted by rating (popular order is preserved) but `distanceKm` is populated per item.

All existing query parameters on both endpoints are unchanged.

#### New DTO shapes (for reference)

```jsonc
// NearbyClinicDto (extends ClinicDto)
{
  "id": 1,
  "name": "Cairo Medical Center",
  "government": "Cairo",
  "area": "Downtown",
  "address": "...",
  "latitude": 30.0444,
  "longitude": 31.2357,
  "isActive": true,
  "doctorsCount": 12,
  "distanceKm": 0.842,           // NEW
  "matchingDoctorsCount": 3      // NEW — doctors matching the specialization filter
}

// NearbyDoctorDto (extends DoctorListItemDto)
{
  "id": 1,
  "fullName": "Dr. ...",
  "specialization": "Cardiology",
  "profileImageUrl": "...",
  "consultationFee": 250.00,
  "averageRating": 4.6,
  "totalReviews": 48,
  "isAvailable": true,
  "clinicName": "...",
  "clinicArea": "...",
  "isFavorited": false,
  "latitude": 30.0444,
  "longitude": 31.2357,
  "distanceKm": 1.205,           // inherited from DoctorListItemDto
  "clinicIdForLocation": 1       // NEW — the clinic whose coords were used
}
```

### Fixed

- **`GET /api/doctor` was returning HTTP 500.** Caused by a missing AutoMapper mapping (`Doctor -> DoctorListItemDto`) on the backend. The endpoint now returns `200` with a `DoctorListItemDto[]` as it was always supposed to. If your app was silently catching this as a generic server error, you can now remove any error-toast logic that fired on the doctor browse screen.

### Not changed

- No database schema changes. No migration needed on the backend, and no client-side data migration needed.
- All previously-existing endpoints, query parameters, and response fields are unchanged. New fields are additive — existing JSON parsers will ignore them.

### Out of scope (still not in this release)

- AI Chatbot
- Social login (Google / Apple / Facebook)
- Structured prescription storage (current JSON-in-text storage continues to work)
