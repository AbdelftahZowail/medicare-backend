using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.DTOs.Review
{
    public class ReviewDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public int? AppointmentId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateReviewDto
    {
        [Required(ErrorMessage = "معرف الطبيب مطلوب")]
        public int DoctorId { get; set; }

        public int? AppointmentId { get; set; }

        [Required(ErrorMessage = "التقييم مطلوب")]
        [Range(1, 5, ErrorMessage = "التقييم يجب أن يكون من 1 إلى 5")]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }
}
