using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;

namespace NutrishaAI.API.Services
{
    public interface IFirebaseNotificationService
    {
        Task<NotificationResponse> SendNotificationToTokenAsync(string token, SendNotificationRequest request);
        Task<NotificationResponse> SendNotificationToUserAsync(string userId, SendNotificationRequest request);
        Task<NotificationResponse> SendNotificationToTopicAsync(string topic, SendNotificationRequest request);
        Task<NotificationResponse> SendNotificationToMultipleTokensAsync(string[] tokens, SendNotificationRequest request);
        Task<TopicSubscriptionResponse> SubscribeToTopicAsync(string[] tokens, string topic);
        Task<TopicSubscriptionResponse> UnsubscribeFromTopicAsync(string[] tokens, string topic);
        Task<TokenRegistrationResponse> RegisterTokenAsync(RegisterTokenRequest request);
        Task<bool> ValidateTokenAsync(string token);
        Task<NotificationResponse> SendNotificationByEmailAsync(SendNotificationByEmailRequest request);
        Task<DeviceDeactivationResponse> DeactivateDeviceAsync(string userId, string deviceId);
    }
}