using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("users")]
    public class User : BaseModel
    {
        [Column("id")]
        public Guid Id { get; set; }
        
        [Column("email")]
        public string Email { get; set; } = string.Empty;
        
        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;
        
        [Column("role")]
        public string Role { get; set; } = "patient"; // patient, nutritionist, admin
        
        [Column("phone_number")]
        public string? PhoneNumber { get; set; }
        
        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }
        
        [Column("gender")]
        public string? Gender { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}