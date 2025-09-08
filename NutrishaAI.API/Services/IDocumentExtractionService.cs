using System;
using System.IO;
using System.Threading.Tasks;

namespace NutrishaAI.API.Services
{
    public class DocumentContent
    {
        public string Text { get; set; } = string.Empty;
        public Dictionary<string, object>? Metadata { get; set; }
        public List<string>? ExtractedTags { get; set; }
    }

    public interface IDocumentExtractionService
    {
        Task<DocumentContent> ExtractContentFromPdfAsync(Stream fileStream);
        Task<DocumentContent> ExtractContentFromWordAsync(Stream fileStream);
        Task<DocumentContent> ExtractContentFromImageAsync(Stream fileStream, string mimeType);
        Task<string> GeneratePromptFromContentAsync(string content, string documentType);
        Task<List<string>> GenerateTagsFromContentAsync(string content);
    }
}