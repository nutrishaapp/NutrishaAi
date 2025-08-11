namespace NutrishaAI.API.Models.Responses
{
    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public UserResponse User { get; set; } = new();
    }

    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class ApiKeyResponse
    {
        public string ApiKey { get; set; } = string.Empty;
        public Guid ApiKeyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Warning { get; set; } = "Save this key securely. It won't be shown again.";
    }

    public class ApiKeyListResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public DateTime? LastUsed { get; set; }
        public DateTime Created { get; set; }
        public bool IsActive { get; set; }
    }
}