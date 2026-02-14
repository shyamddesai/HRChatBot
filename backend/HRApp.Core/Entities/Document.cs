using System;
using Pgvector;

namespace HRApp.Core.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Vector? Embedding { get; set; }
        public string Type { get; set; } = "Policy";
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}