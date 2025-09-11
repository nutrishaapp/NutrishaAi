using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("pinned_plans")]
    public class PinnedPlan : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }
        
        [Column("user_id")]
        public Guid UserId { get; set; }
        
        [Column("plan_type")]
        public string PlanType { get; set; } = "diet"; // diet, workout, medication, supplement
        
        [Column("plan_id")]
        public Guid? PlanId { get; set; }
        
        [Column("plan_content")]
        public Dictionary<string, object>? PlanContent { get; set; }
        
        [Column("start_date")]
        public DateTime? StartDate { get; set; }
        
        [Column("end_date")]
        public DateTime? EndDate { get; set; }
        
        [Column("is_active")]
        public bool IsActive { get; set; } = true;
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        
    }
}