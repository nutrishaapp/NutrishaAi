using System;
using System.Collections.Generic;

namespace NutrishaAI.API.Models
{
    public class ExtractedMemory
    {
        public string Summary { get; set; } = string.Empty;
        public bool ShouldSave { get; set; }
        public List<string> Topics { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    }

    public class MemoryVector
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid ConversationId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public List<string> Topics { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string MessageContent { get; set; } = string.Empty;
    }

    public class MemorySearchResult
    {
        public MemoryVector Memory { get; set; } = new();
        public float Score { get; set; }
        public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    }
}