using System;
using System.Collections.Generic;

namespace NutrishaAI.API.Models.Responses
{
    public class DocumentResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OriginalFilename { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public bool IsPublic { get; set; }
        public List<string>? Tags { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class DocumentListResponse
    {
        public List<DocumentResponse> Documents { get; set; } = new();
        public int TotalCount { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
    }

    public class DocumentUploadResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class DocumentProcessingStatusResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ProcessedAt { get; set; }
        public bool HasContent { get; set; }
        public List<string>? Tags { get; set; }
    }
}