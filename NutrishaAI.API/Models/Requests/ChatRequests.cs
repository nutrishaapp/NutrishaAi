using System.ComponentModel.DataAnnotations;

namespace NutrishaAI.API.Models.Requests
{
    public class SendMessageRequest
    {
        [Required]
        public Guid ConversationId { get; set; }
        
        public string? Content { get; set; }
        
        public string MessageType { get; set; } = "text";
    }

    public class SendMultimediaMessageRequest
    {
        [Required]
        public Guid ConversationId { get; set; }
        
        public string? Content { get; set; }
        
        [Required]
        public string MessageType { get; set; } // image, voice, document, video
        
        public string? BlobName { get; set; } // Azure blob name after upload
        
        public string? AdditionalContext { get; set; }
    }

    public class CreateConversationRequest
    {
        public string? Title { get; set; }
        public Guid? NutritionistId { get; set; }
    }

    public class ProcessMultimediaRequest
    {
        [Required]
        public Guid ConversationId { get; set; }
        
        [Required]
        public string BlobName { get; set; } = string.Empty;
        
        [Required]
        public string MessageType { get; set; } = string.Empty;
        
        public string? TextPrompt { get; set; }
        
        public string? AdditionalContext { get; set; }
    }

    public class DirectAiChatRequest
    {
        [Required]
        public string Message { get; set; } = string.Empty;
        
        public string? Context { get; set; }
        
        public bool ExtractHealthData { get; set; } = false;
    }
}