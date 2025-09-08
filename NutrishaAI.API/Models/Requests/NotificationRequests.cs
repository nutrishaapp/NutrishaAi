using System.ComponentModel.DataAnnotations;

namespace NutrishaAI.API.Models.Requests
{
    public class SendNotificationRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public Dictionary<string, string>? Data { get; set; }

        public string? ClickAction { get; set; }
    }

    public class SendNotificationToUserRequest : SendNotificationRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }

    public class SendNotificationToTokenRequest : SendNotificationRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    public class SendNotificationToTopicRequest : SendNotificationRequest
    {
        [Required]
        public string Topic { get; set; } = string.Empty;
    }

    public class RegisterTokenRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        public string? DeviceId { get; set; }
        public string? Platform { get; set; }
    }

    public class SubscribeToTopicRequest
    {
        [Required]
        public string[] Tokens { get; set; } = Array.Empty<string>();

        [Required]
        public string Topic { get; set; } = string.Empty;
    }

    public class SendNotificationByEmailRequest : SendNotificationRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class DeactivateDeviceRequest
    {
        [Required]
        public string DeviceId { get; set; } = string.Empty;
    }
}