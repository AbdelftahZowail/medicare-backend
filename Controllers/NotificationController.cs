using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Notification;
using MedicalApp.API.Helpers;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    [Authorize]
    public class NotificationController : BaseApiController
    {
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;

        public NotificationController(INotificationService notificationService, IUnitOfWork unitOfWork)
        {
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Get all notifications for the current logged-in user.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var result = await _notificationService.GetNotificationsAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get unread notifications count for the badge of the bell icon on the home screen.
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var result = await _notificationService.GetUnreadCountAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var result = await _notificationService.MarkAsReadAsync(GetUserId(), id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _notificationService.DeleteNotificationAsync(GetUserId(), id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("fcm-token")]
        public async Task<IActionResult> GetFcmToken()
        {
            var userId = GetUserId();
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return Unauthorized();

            return Ok(ApiResponse<object>.Success(new { token = user.FcmToken }));
        }

        [HttpPost("fcm-token")]
        public async Task<IActionResult> RegisterFcmToken([FromBody] FcmTokenDto dto)
        {
            var userId = GetUserId();
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return Unauthorized();

            user.FcmToken = dto.Token;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();

            return Ok(ApiResponse.Success("FCM token registered"));
        }

        [HttpDelete("fcm-token")]
        public async Task<IActionResult> DeleteFcmToken()
        {
            var userId = GetUserId();
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user != null)
            {
                user.FcmToken = null;
                _unitOfWork.Users.Update(user);
                await _unitOfWork.CompleteAsync();
            }
            return Ok(ApiResponse.Success("FCM token removed"));
        }
    }
}
