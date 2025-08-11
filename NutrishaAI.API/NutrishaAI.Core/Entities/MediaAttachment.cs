using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NutrishaAI.Core.Entities
{
    [Table("media_attachments")]
    public class MediaAttachment : BaseModel
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public int? FileSize { get; set; }
        public string? FileName { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? Transcription { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation property
        public Message? Message { get; set; }
    }
}