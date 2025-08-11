using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("conversations")]
    public class Conversation : BaseModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? NutritionistId { get; set; }
        public string? Title { get; set; }
        public string Status { get; set; } = "active"; // active, closed, archived
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public User? User { get; set; }
        public User? Nutritionist { get; set; }
        public List<Message>? Messages { get; set; }
    }
}