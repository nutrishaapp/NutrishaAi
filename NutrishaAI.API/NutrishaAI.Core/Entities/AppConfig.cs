using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("app_configs")]
    public class AppConfig : BaseModel
    {
        [PrimaryKey("key")]
        [Column("key")]
        public string Key { get; set; } = string.Empty;
        
        [Column("value")]
        public string Value { get; set; } = string.Empty;
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        
        [Column("updated_by")]
        public Guid? UpdatedBy { get; set; }
    }
}