using System;
using System.Collections.Generic;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.API.Models
{
    [Table("documents")]
    public class Document : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }
        
        [Column("user_id")]
        public Guid UserId { get; set; }
        
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        
        [Column("description")]
        public string? Description { get; set; }
        
        [Column("document_type")]
        public string DocumentType { get; set; } = string.Empty; // workout_plan, diet_plan, other
        
        [Column("original_filename")]
        public string OriginalFilename { get; set; } = string.Empty;
        
        [Column("blob_name")]
        public string BlobName { get; set; } = string.Empty;
        
        [Column("file_size")]
        public long? FileSize { get; set; }
        
        [Column("mime_type")]
        public string? MimeType { get; set; }
        
        [Column("content")]
        public string? Content { get; set; }
        
        [Column("prompt")]
        public string? Prompt { get; set; }
        
        [Column("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
        
        [Column("status")]
        public string Status { get; set; } = "active";
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        
        [Column("processed_at")]
        public DateTime? ProcessedAt { get; set; }
        
        [Column("is_public")]
        public bool IsPublic { get; set; }
        
        [Column("tags")]
        public List<string>? Tags { get; set; }
    }
}