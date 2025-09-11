namespace NutrishaAI.API.Models.Responses
{
    public class DietPlanResponse
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid NutritionistId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Dictionary<string, object> PlanData { get; set; } = new();
        public string Status { get; set; } = "draft";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserResponse? Patient { get; set; }
        public UserResponse? Nutritionist { get; set; }
    }

    public class PinnedPlanResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string PlanType { get; set; } = "diet";
        public Guid? PlanId { get; set; }
        public Dictionary<string, object>? PlanContent { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}