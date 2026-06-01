using System.ComponentModel.DataAnnotations;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.DTOs.Auth
{
    // ===== Login DTO (by Phone) =====
    public class LoginDto
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        public string Password { get; set; } = string.Empty;
    }

    // ===== Register Patient =====
    public class RegisterPatientDto
    {
        [Required(ErrorMessage = "الاسم مطلوب")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "العمر مطلوب")]
        public int Age { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare(nameof(Password), ErrorMessage = "كلمة المرور غير متطابقة")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // ===== Register Clinic =====
    public class RegisterClinicDto
    {
        [Required(ErrorMessage = "اسم العيادة مطلوب")]
        [MaxLength(200)]
        public string ClinicName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? LinkMap { get; set; }

        [Required(ErrorMessage = "المحافظة مطلوبة")]
        [MaxLength(100)]
        public string Government { get; set; } = string.Empty;

        [Required(ErrorMessage = "المنطقة مطلوبة")]
        [MaxLength(100)]
        public string Area { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare(nameof(Password), ErrorMessage = "كلمة المرور غير متطابقة")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "رخصة العيادة مطلوبة")]
        [MaxLength(500)]
        public string LicenseFileUrl { get; set; } = string.Empty;
    }

    // ===== Register Doctor =====
    public class RegisterDoctorDto
    {
        [Required(ErrorMessage = "الاسم مطلوب")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare(nameof(Password), ErrorMessage = "كلمة المرور غير متطابقة")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "التخصص مطلوب")]
        [MaxLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Required(ErrorMessage = "رخصة الطبيب مطلوبة")]
        [MaxLength(500)]
        public string LicenseFileUrl { get; set; } = string.Empty;
    }

    // ===== Forgot Password =====
    public class ForgotPasswordDto
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;
    }

    // ===== Verify OTP =====
    public class VerifyOtpDto
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز التحقق مطلوب")]
        [MaxLength(6)]
        public string OtpCode { get; set; } = string.Empty;
    }

    // ===== Reset Password =====
    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز التحقق مطلوب")]
        [MaxLength(6)]
        public string OtpCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
        [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare(nameof(NewPassword), ErrorMessage = "كلمة المرور غير متطابقة")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // ===== Social Login =====
    public class SocialLoginDto
    {
        [Required]
        public string Provider { get; set; } = string.Empty; // "Google", "Apple", "Facebook"

        [Required]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class RefreshTokenRequestDto
    {
        [Required(ErrorMessage = "رمز التحديث مطلوب")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutRequestDto
    {
        [MaxLength(2000)]
        public string? RefreshToken { get; set; }
    }

    // ===== Auth Response =====
    public class AuthResponseDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime TokenExpiration { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiration { get; set; }
        public int? ProfileId { get; set; }
    }

    public class RegisterTelegramDto
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز التليجرام (Chat ID) مطلوب")]
        public string TelegramChatId { get; set; } = string.Empty;
    }
}
