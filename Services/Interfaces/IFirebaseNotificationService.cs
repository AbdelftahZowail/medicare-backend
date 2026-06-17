namespace MedicalApp.API.Services.Interfaces
{
    public interface IFirebaseNotificationService
    {
        Task SendToUserAsync(int userId, string title, string body, string? type = null, int? relatedId = null);

        Task SendToMultipleUsersAsync(IEnumerable<int> userIds, string title, string body, string? type = null, int? relatedId = null);
    }
}
