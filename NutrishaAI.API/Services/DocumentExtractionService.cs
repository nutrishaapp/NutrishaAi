using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NutrishaAI.API.Services
{
    public class DocumentExtractionService : IDocumentExtractionService
    {
        private readonly ISimpleGeminiService _geminiService;
        private readonly ILogger<DocumentExtractionService> _logger;

        public DocumentExtractionService(
            ISimpleGeminiService geminiService,
            ILogger<DocumentExtractionService> logger)
        {
            _geminiService = geminiService;
            _logger = logger;
        }

        public async Task<DocumentContent> ExtractContentFromPdfAsync(Stream fileStream)
        {
            try
            {
                // Convert PDF stream to base64
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var pdfBytes = memoryStream.ToArray();
                var base64Pdf = Convert.ToBase64String(pdfBytes);

                // Use Gemini to extract content from PDF
                var attachments = new List<AttachmentContent>
                {
                    new AttachmentContent
                    {
                        Base64Data = base64Pdf,
                        MimeType = "application/pdf",
                        Type = "document"
                    }
                };

                var extractionPrompt = @"Please extract and return ALL the text content from this PDF document. 
                    Include all sections, headings, paragraphs, lists, tables, and any other textual information.
                    Preserve the structure and formatting as much as possible.
                    Return ONLY the extracted text content, no additional commentary.";

                var extractedText = await _geminiService.ExtractContentAsync(
                    extractionPrompt, 
                    attachments
                );

                return new DocumentContent
                {
                    Text = extractedText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["extractionMethod"] = "Gemini AI",
                        ["extractedAt"] = DateTime.UtcNow
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting content from PDF using Gemini");
                throw new InvalidOperationException("Failed to extract PDF content", ex);
            }
        }

        public async Task<DocumentContent> ExtractContentFromWordAsync(Stream fileStream)
        {
            try
            {
                // Convert Word document stream to base64
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var docBytes = memoryStream.ToArray();
                var base64Doc = Convert.ToBase64String(docBytes);

                // Note: Gemini might not directly support Word documents
                // We'll try to send it as a document and see if it can process it
                var attachments = new List<AttachmentContent>
                {
                    new AttachmentContent
                    {
                        Base64Data = base64Doc,
                        MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        Type = "document"
                    }
                };

                var extractionPrompt = @"Please extract and return ALL the text content from this document. 
                    Include all sections, headings, paragraphs, lists, tables, and any other textual information.
                    Preserve the structure and formatting as much as possible.
                    Return ONLY the extracted text content, no additional commentary.";

                var extractedText = await _geminiService.ExtractContentAsync(
                    extractionPrompt,
                    attachments
                );

                return new DocumentContent
                {
                    Text = extractedText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["extractionMethod"] = "Gemini AI",
                        ["extractedAt"] = DateTime.UtcNow
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting content from Word document using Gemini");
                // Fallback message for Word documents if Gemini can't process them
                return new DocumentContent
                {
                    Text = "Word document uploaded successfully. Content extraction for Word documents may require manual review.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["extractionMethod"] = "Not extracted",
                        ["note"] = "Word documents may need to be converted to PDF for optimal extraction"
                    }
                };
            }
        }

        public async Task<DocumentContent> ExtractContentFromImageAsync(Stream fileStream, string mimeType)
        {
            try
            {
                // Convert image stream to base64
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);

                // Use Gemini to extract content from image
                var attachments = new List<AttachmentContent>
                {
                    new AttachmentContent
                    {
                        Base64Data = base64Image,
                        MimeType = mimeType,
                        Type = "image"
                    }
                };

                var extractionPrompt = @"Please analyze this image and extract ALL text content, information, and details you can see. 
                    If it's a workout plan, extract exercises, sets, reps, and instructions.
                    If it's a diet plan or food image, extract nutritional information, ingredients, calories, etc.
                    If it's a document or text, extract all readable text.
                    Provide a comprehensive description of what you see in the image.
                    Return ALL extracted information in a clear, organized format.";

                var extractedText = await _geminiService.ExtractContentAsync(
                    extractionPrompt,
                    attachments
                );

                return new DocumentContent
                {
                    Text = extractedText,
                    Metadata = new Dictionary<string, object>
                    {
                        ["extractionMethod"] = "Gemini AI Vision",
                        ["extractedAt"] = DateTime.UtcNow,
                        ["imageType"] = mimeType
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting content from image using Gemini");
                throw new InvalidOperationException("Failed to extract image content", ex);
            }
        }

        public async Task<string> GeneratePromptFromContentAsync(string content, string documentType)
        {
            // Removed - prompts will be created manually by users
            throw new NotImplementedException("Prompt generation has been removed. Prompts should be created manually.");
        }

        public async Task<List<string>> GenerateTagsFromContentAsync(string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new List<string>();
                }

                // Simplified tag generation - just extract key topics
                var prompt = @"Extract 5-10 relevant tags from this document content. 
                    Focus on key topics, themes, and categories.
                    Return only a comma-separated list of tags, nothing else.
                    
                    Document content:
                    " + content.Substring(0, Math.Min(content.Length, 2000));

                var response = await _geminiService.ExtractContentAsync(prompt, null);
                
                // Parse tags from raw response
                var cleanedResponse = response.Trim();
                
                var tags = cleanedResponse.Split(',')
                    .Select(t => t.Trim())
                    .Select(t => t.ToLower())
                    .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 2 && t.Length < 30)
                    .Distinct()
                    .Take(10)
                    .ToList();

                return tags;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tags from content");
                return new List<string>();
            }
        }
    }
}