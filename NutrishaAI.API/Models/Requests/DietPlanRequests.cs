using System.ComponentModel.DataAnnotations;

namespace NutrishaAI.API.Models.Requests
{
    public class CreateDietPlanRequest
    {
        [Required]
        public Guid PatientId { get; set; }
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        [Required]
        public Dictionary<string, object> PlanData { get; set; } = new();
        
        public string Status { get; set; } = "draft";
    }

    public class UpdateDietPlanRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, object>? PlanData { get; set; }
        public string? Status { get; set; }
    }

    public class PinPlanRequest
    {
        [Required]
        public string PlanType { get; set; } = "diet";
        
        public Guid? PlanId { get; set; }
        
        public Dictionary<string, object>? PlanContent { get; set; }
        
        public string[]? Goals { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        public Dictionary<string, object>? ReminderSettings { get; set; }
    }
}