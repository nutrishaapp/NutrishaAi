namespace NutrishaAI.API.Models.Requests
{
    public class MemoryExtractionRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? ConversationContext { get; set; }
        public Guid UserId { get; set; }
        public Guid ConversationId { get; set; }
        public Dictionary<string, string>? AdditionalContext { get; set; }
    }
}