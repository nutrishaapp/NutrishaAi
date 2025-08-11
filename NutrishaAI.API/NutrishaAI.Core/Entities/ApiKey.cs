using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("api_keys")]
    public class ApiKey : BaseModel
    {
        public Guid Id { get; set; }
        public string KeyHash { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string[]? Permissions { get; set; }
        public int RateLimit { get; set; } = 1000;
        public bool IsActive { get; set; } = true;
        public DateTime? LastUsedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Not stored in DB, only used when returning a new key
        public string? FullKey { get; set; }
    }
}