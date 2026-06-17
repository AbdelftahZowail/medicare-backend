using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class FirebaseNotificationService : IFirebaseNotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<FirebaseNotificationService> _logger;

        public FirebaseNotificationService(IUnitOfWork unitOfWork, ILogger<FirebaseNotificationService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task SendToUserAsync(int userId, string title, string body, string? type = null, int? relatedId = null)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user?.FcmToken == null) return;

                var message = new Message
                {
                    Token = user.FcmToken,
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = new Dictionary<string, string>
                    {
                        ["type"] = type ?? "",
                        ["relatedId"] = relatedId?.ToString() ?? ""
                    }
                };

                await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogDebug("FCM push sent to user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send Firebase push to user {UserId}", userId);
            }
        }

        public async Task SendToMultipleUsersAsync(IEnumerable<int> userIds, string title, string body, string? type = null, int? relatedId = null)
        {
            var tokens = await _unitOfWork.Users.Query()
                .Where(u => userIds.Contains(u.Id) && u.FcmToken != null)
                .Select(u => u.FcmToken!)
                .ToListAsync();

            if (tokens.Count == 0) return;

            foreach (var chunk in tokens.Chunk(500))
            {
                try
                {
                    var message = new MulticastMessage
                    {
                        Tokens = chunk,
                        Notification = new Notification
                        {
                            Title = title,
                            Body = body
                        },
                        Data = new Dictionary<string, string>
                        {
                            ["type"] = type ?? "",
                            ["relatedId"] = relatedId?.ToString() ?? ""
                        }
                    };

                    var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
                    _logger.LogInformation("FCM multicast: {Success}/{Total}", response.SuccessCount, response.FailureCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send Firebase multicast push");
                }
            }
        }
    }
}
