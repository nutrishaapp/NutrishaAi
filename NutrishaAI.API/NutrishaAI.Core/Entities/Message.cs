using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("messages")]
    public class Message : BaseModel
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string? Content { get; set; }
        public string MessageType { get; set; } = "text"; // text, image, voice, document, video
        public bool IsAiGenerated { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public Conversation? Conversation { get; set; }
        public User? Sender { get; set; }
        public List<MediaAttachment>? Attachments { get; set; }
    }
}