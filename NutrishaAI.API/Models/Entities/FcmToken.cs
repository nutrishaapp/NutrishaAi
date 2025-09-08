using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.API.Models.Entities
{
    [Table("fcm_tokens")]
    public class FcmToken : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Column("device_id")]
        public string? DeviceId { get; set; }

        [Column("platform")]
        public string? Platform { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}