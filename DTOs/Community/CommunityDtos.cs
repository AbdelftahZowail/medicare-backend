using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.DTOs.Community
{
    public class CreatePostDto
    {
        [Required(ErrorMessage = "محتوى المنشور مطلوب")]
        [MaxLength(2000, ErrorMessage = "لا يمكن أن يتجاوز المنشور 2000 حرف")]
        public string Content { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "اسم التخصص لا يتجاوز 100 حرف")]
        public string? Specialization { get; set; } // The specialization chip selected e.g. Neurology, Dentistry, or null/empty for All/General
    }

    public class CreateCommentDto
    {
        [Required(ErrorMessage = "محتوى التعليق مطلوب")]
        [MaxLength(1000, ErrorMessage = "لا يمكن أن يتجاوز التعليق 1000 حرف")]
        public string Content { get; set; } = string.Empty;
    }

    public class CommunityCommentDto
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorProfileImageUrl { get; set; }
        public string AuthorRoleText { get; set; } = string.Empty; // "Patient" or "Doctor" or "Admin"
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CommunityPostDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorProfileImageUrl { get; set; }
        public string AuthorRoleText { get; set; } = string.Empty; // "Patient" or "Doctor" or "Admin"
        public string? AuthorSpecialization { get; set; } // If the author is a doctor
        public string Content { get; set; } = string.Empty;
        public string? Specialization { get; set; } // Specialization category chip e.g. Neurology
        public DateTime CreatedAt { get; set; }
        public int CommentsCount { get; set; }
        public List<CommunityCommentDto> Comments { get; set; } = new List<CommunityCommentDto>();
    }
}
