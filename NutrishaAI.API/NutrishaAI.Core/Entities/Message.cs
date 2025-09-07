using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("messages")]
    public class Message : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }
        
        [Column("conversation_id")]
        public Guid ConversationId { get; set; }
        
        [Column("sender_id")]
        public Guid? SenderId { get; set; }
        
        [Column("content")]
        public string? Content { get; set; }
        
        [Column("message_type")]
        public string MessageType { get; set; } = "text"; // text, image, voice, document, video
        
        [Column("is_ai_generated")]
        public bool IsAiGenerated { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("attachments")]
        public object? Attachments { get; set; } // JSONB column
    }
}