using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("conversations")]
    public class Conversation : BaseModel
    {
        [Column("id")]
        [PrimaryKey]
        public Guid Id { get; set; }
        
        [Column("user_id")]
        public Guid UserId { get; set; }
        
        [Column("nutritionist_id")]
        public Guid? NutritionistId { get; set; }
        
        [Column("title")]
        public string? Title { get; set; }
        
        [Column("status")]
        public string Status { get; set; } = "active"; // active, closed, archived
        
        [Column("conversation_mode")]
        public string ConversationMode { get; set; } = "ai"; // ai, human
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}