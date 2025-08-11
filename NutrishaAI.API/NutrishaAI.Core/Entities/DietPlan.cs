using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("diet_plans")]
    public class DietPlan : BaseModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid NutritionistId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Dictionary<string, object> PlanData { get; set; } = new();
        public string Status { get; set; } = "draft"; // draft, active, completed, archived
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public User? Patient { get; set; }
        public User? Nutritionist { get; set; }
    }
}