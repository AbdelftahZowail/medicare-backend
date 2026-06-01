using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MedicalApp.API.Data;
using MedicalApp.API.DTOs.Community;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Models.Enums;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class CommunityService : ICommunityService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CommunityService> _logger;

        public CommunityService(ApplicationDbContext context, ILogger<CommunityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===== Create Post =====
        public async Task<ApiResponse<CommunityPostDto>> CreatePostAsync(int userId, CreatePostDto dto)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Doctor)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return ApiResponse<CommunityPostDto>.Failure("المستخدم غير موجود", 404);

                var post = new CommunityPost
                {
                    UserId = userId,
                    Content = System.Web.HttpUtility.HtmlEncode(dto.Content),
                    Specialization = string.IsNullOrWhiteSpace(dto.Specialization) ? null : dto.Specialization.Trim()
                };

                _context.CommunityPosts.Add(post);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} created a new community post {PostId}", userId, post.Id);

                // Reload for DTO mapping
                post.User = user;
                return ApiResponse<CommunityPostDto>.Success(MapToPostDto(post), "تم نشر مشاركتك بنجاح", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating community post for user {UserId}", userId);
                return ApiResponse<CommunityPostDto>.Failure("حدث خطأ أثناء معالجة طلبك، يرجى المحاولة لاحقاً", 500);
            }
        }

        // ===== Get Posts (Filtered by Specialization and Search) =====
        public async Task<ApiResponse<List<CommunityPostDto>>> GetPostsAsync(string? specialization = null, string? search = null)
        {
            try
            {
                var query = _context.CommunityPosts
                    .Include(cp => cp.User)
                        .ThenInclude(u => u.Doctor)
                    .Include(cp => cp.Comments)
                        .ThenInclude(cc => cc.User)
                    .AsQueryable();

                // 1. Filter by Specialization Chip
                if (!string.IsNullOrWhiteSpace(specialization) && !specialization.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(cp => cp.Specialization == specialization);
                }

                // 2. Filter by Search Query (Content or Author Name)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(cp => cp.Content.Contains(search) || cp.User.FullName.Contains(search));
                }

                // 3. Order by Recency
                var posts = await query
                    .OrderByDescending(cp => cp.CreatedAt)
                    .ToListAsync();

                var dtos = posts.Select(MapToPostDto).ToList();
                return ApiResponse<List<CommunityPostDto>>.Success(dtos, "تم استرجاع منشورات المجتمع بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving community posts");
                return ApiResponse<List<CommunityPostDto>>.Failure("حدث خطأ أثناء استرجاع المنشورات", 500);
            }
        }

        // ===== Create Comment =====
        public async Task<ApiResponse<CommunityCommentDto>> CreateCommentAsync(int postId, int userId, CreateCommentDto dto)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return ApiResponse<CommunityCommentDto>.Failure("المستخدم غير موجود", 404);

                var post = await _context.CommunityPosts.FindAsync(postId);
                if (post == null)
                    return ApiResponse<CommunityCommentDto>.Failure("المنشور غير موجود", 404);

                var comment = new CommunityComment
                {
                    PostId = postId,
                    UserId = userId,
                    Content = System.Web.HttpUtility.HtmlEncode(dto.Content)
                };

                _context.CommunityComments.Add(comment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} added comment {CommentId} to post {PostId}", userId, comment.Id, postId);

                comment.User = user;
                return ApiResponse<CommunityCommentDto>.Success(MapToCommentDto(comment), "تم إضافة تعليقك بنجاح", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating comment for post {PostId} by user {UserId}", postId, userId);
                return ApiResponse<CommunityCommentDto>.Failure("حدث خطأ أثناء إضافة التعليق", 500);
            }
        }

        // ===== Get Post Comments =====
        public async Task<ApiResponse<List<CommunityCommentDto>>> GetPostCommentsAsync(int postId)
        {
            try
            {
                var postExists = await _context.CommunityPosts.AnyAsync(cp => cp.Id == postId);
                if (!postExists)
                    return ApiResponse<List<CommunityCommentDto>>.Failure("المنشور غير موجود", 404);

                var comments = await _context.CommunityComments
                    .Include(cc => cc.User)
                    .Where(cc => cc.PostId == postId)
                    .OrderBy(cc => cc.CreatedAt)
                    .ToListAsync();

                var dtos = comments.Select(MapToCommentDto).ToList();
                return ApiResponse<List<CommunityCommentDto>>.Success(dtos, "تم استرجاع تعليقات المنشور بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comments for post {PostId}", postId);
                return ApiResponse<List<CommunityCommentDto>>.Failure("حدث خطأ أثناء استرجاع التعليقات", 500);
            }
        }

        // ===== Delete Post =====
        public async Task<ApiResponse<bool>> DeletePostAsync(int postId, int userId)
        {
            try
            {
                var post = await _context.CommunityPosts.FindAsync(postId);
                if (post == null)
                    return ApiResponse<bool>.Failure("المنشور غير موجود", 404);

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return ApiResponse<bool>.Failure("المستخدم غير موجود", 404);

                // Authorization check: Only author or admins can delete
                if (post.UserId != userId && user.Role != UserRole.ClinicAdmin)
                    return ApiResponse<bool>.Failure("غير مصرح لك بحذف هذا المنشور", 403);

                post.IsDeleted = true;
                _context.CommunityPosts.Update(post);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Community post {PostId} deleted by user {UserId}", postId, userId);
                return ApiResponse<bool>.Success(true, "تم حذف المنشور بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting community post {PostId}", postId);
                return ApiResponse<bool>.Failure("حدث خطأ أثناء حذف المنشور", 500);
            }
        }

        // ===== Delete Comment =====
        public async Task<ApiResponse<bool>> DeleteCommentAsync(int commentId, int userId)
        {
            try
            {
                var comment = await _context.CommunityComments
                    .Include(cc => cc.Post)
                    .FirstOrDefaultAsync(cc => cc.Id == commentId);

                if (comment == null)
                    return ApiResponse<bool>.Failure("التعليق غير موجود", 404);

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return ApiResponse<bool>.Failure("المستخدم غير موجود", 404);

                // Authorization check: Only comment author, post author, or admin can delete
                if (comment.UserId != userId && comment.Post.UserId != userId && user.Role != UserRole.ClinicAdmin)
                    return ApiResponse<bool>.Failure("غير مصرح لك بحذف هذا التعليق", 403);

                comment.IsDeleted = true;
                _context.CommunityComments.Update(comment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Comment {CommentId} deleted by user {UserId}", commentId, userId);
                return ApiResponse<bool>.Success(true, "تم حذف التعليق بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment {CommentId}", commentId);
                return ApiResponse<bool>.Failure("حدث خطأ أثناء حذف التعليق", 500);
            }
        }

        // ===== Map Helpers =====
        private CommunityPostDto MapToPostDto(CommunityPost post)
        {
            return new CommunityPostDto
            {
                Id = post.Id,
                UserId = post.UserId,
                AuthorName = post.User?.FullName ?? "مستخدم مجهول",
                AuthorProfileImageUrl = post.User?.ProfileImageUrl,
                AuthorRoleText = post.User?.Role.ToString() ?? "Patient",
                AuthorSpecialization = post.User?.Doctor?.Specialization,
                Content = post.Content,
                Specialization = post.Specialization,
                CreatedAt = post.CreatedAt,
                CommentsCount = post.Comments?.Count(c => !c.IsDeleted) ?? 0,
                Comments = post.Comments?
                    .Where(c => !c.IsDeleted)
                    .OrderBy(c => c.CreatedAt)
                    .Select(MapToCommentDto)
                    .ToList() ?? new List<CommunityCommentDto>()
            };
        }

        private CommunityCommentDto MapToCommentDto(CommunityComment comment)
        {
            return new CommunityCommentDto
            {
                Id = comment.Id,
                PostId = comment.PostId,
                UserId = comment.UserId,
                AuthorName = comment.User?.FullName ?? "مستخدم مجهول",
                AuthorProfileImageUrl = comment.User?.ProfileImageUrl,
                AuthorRoleText = comment.User?.Role.ToString() ?? "Patient",
                Content = comment.Content,
                CreatedAt = comment.CreatedAt
            };
        }
    }
}
