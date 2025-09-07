namespace NutrishaAI.API.Models.Responses
{
    public class AppConfigResponse
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}