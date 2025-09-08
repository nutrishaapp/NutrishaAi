namespace NutrishaAI.API.Models.Responses
{
    public class MessageResponse
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid? SenderId { get; set; }
        public string? Content { get; set; }
        public string MessageType { get; set; } = "text";
        public bool IsAiGenerated { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<MediaAttachmentResponse>? Attachments { get; set; }
        public UserResponse? Sender { get; set; }
    }

    public class MediaAttachmentResponse
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public int? FileSize { get; set; }
        public string? FileName { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Transcription { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ConversationResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? NutritionistId { get; set; }
        public string? Title { get; set; }
        public string Status { get; set; } = "active";
        public string ConversationMode { get; set; } = "ai";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserResponse? User { get; set; }
        public UserResponse? Nutritionist { get; set; }
        public int MessageCount { get; set; }
        public MessageResponse? LastMessage { get; set; }
    }

    public class ProcessedMessageResponse
    {
        public Guid MessageId { get; set; }
        public string? Transcription { get; set; }
        public string? AiResponse { get; set; }
        public string BlobUrl { get; set; } = string.Empty;
    }

    public class UploadUrlResponse
    {
        public string UploadUrl { get; set; } = string.Empty;
        public string BlobName { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    public class DirectAiChatResponse
    {
        public string AiResponse { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }
}