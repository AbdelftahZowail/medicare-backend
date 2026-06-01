using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Notification entity to store in-app notifications for patients, doctors, or clinic admins.
    /// </summary>
    public class Notification : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;
    }
}
