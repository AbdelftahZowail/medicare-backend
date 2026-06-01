using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Review/Rating given by a Patient to a Doctor after an appointment.
    /// </summary>
    public class Review : BaseEntity
    {
        [Required]
        public int PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient Patient { get; set; } = null!;

        [Required]
        public int DoctorId { get; set; }

        [ForeignKey(nameof(DoctorId))]
        public Doctor Doctor { get; set; } = null!;

        public int? AppointmentId { get; set; }

        [ForeignKey(nameof(AppointmentId))]
        public Appointment? Appointment { get; set; }

        /// <summary>
        /// Rating from 1 to 5.
        /// </summary>
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }
}
