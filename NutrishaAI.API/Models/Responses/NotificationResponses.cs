namespace NutrishaAI.API.Models.Responses
{
    public class NotificationResponse
    {
        public bool Success { get; set; }
        public string? MessageId { get; set; }
        public string? Error { get; set; }
        public int? SuccessCount { get; set; }
        public int? FailureCount { get; set; }
    }

    public class TopicSubscriptionResponse
    {
        public bool Success { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public string[] FailedTokens { get; set; } = Array.Empty<string>();
        public string? Error { get; set; }
    }

    public class TokenRegistrationResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? TokenId { get; set; }
    }

    public class DeviceDeactivationResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }
}