using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("pinned_plans")]
    public class PinnedPlan : BaseModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string PlanType { get; set; } = "diet"; // diet, workout, medication, supplement
        public Guid? PlanId { get; set; }
        public Dictionary<string, object>? PlanContent { get; set; }
        public string[]? Goals { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public Dictionary<string, object>? ReminderSettings { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation property
        public User? User { get; set; }
    }
}