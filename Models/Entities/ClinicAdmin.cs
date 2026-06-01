using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Clinic administrator linked to a User and a Clinic.
    /// </summary>
    public class ClinicAdmin : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        public int ClinicId { get; set; }

        [ForeignKey(nameof(ClinicId))]
        public Clinic Clinic { get; set; } = null!;

        [MaxLength(100)]
        public string? Position { get; set; }
    }
}
