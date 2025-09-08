using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NutrishaAI.API.Models;
using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using Supabase;

namespace NutrishaAI.API.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly IAzureBlobService _blobService;
        private readonly IDocumentExtractionService _extractionService;
        private readonly ILogger<DocumentService> _logger;
        private readonly ISimpleGeminiService _geminiService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public DocumentService(
            Supabase.Client supabaseClient,
            IAzureBlobService blobService,
            IDocumentExtractionService extractionService,
            ISimpleGeminiService geminiService,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DocumentService> logger)
        {
            _supabaseClient = supabaseClient;
            _blobService = blobService;
            _extractionService = extractionService;
            _geminiService = geminiService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task<Document> UploadDocumentAsync(Guid userId, IFormFile file, DocumentUploadDto dto)
        {
            try
            {
                // Validate file type
                var allowedMimeTypes = new[] { 
                    "application/pdf", 
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "image/jpeg",
                    "image/jpg",
                    "image/png",
                    "image/gif",
                    "image/webp"
                };
                if (!allowedMimeTypes.Contains(file.ContentType))
                {
                    throw new InvalidOperationException("Only PDF, Word documents, and images (JPEG, PNG, GIF, WebP) are allowed");
                }

                // Upload to Azure Blob Storage
                string actualBlobName;
                using (var stream = file.OpenReadStream())
                {
                    actualBlobName = await _blobService.UploadFileAsync(stream, file.FileName, "nutrisha-documents", file.ContentType);
                }

                // Create document record
                var document = new Document
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = dto.Name ?? Path.GetFileNameWithoutExtension(file.FileName),
                    Description = dto.Description,
                    DocumentType = dto.DocumentType ?? "other",
                    OriginalFilename = file.FileName,
                    BlobName = actualBlobName,
                    FileSize = file.Length,
                    MimeType = file.ContentType,
                    Status = "active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsPublic = dto.IsPublic,
                    Tags = dto.Tags
                };

                // Save to database
                var response = await _supabaseClient
                    .From<Document>()
                    .Insert(document);

                _logger.LogInformation("Document uploaded successfully: {DocumentId}", document.Id);

                // Queue for content extraction (async)
                _ = Task.Run(async () => 
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();
                    await documentService.ProcessDocumentContentAsync(document.Id);
                });

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                throw;
            }
        }

        public async Task<Document?> GetDocumentAsync(Guid documentId, Guid userId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<Document>()
                    .Select("*")
                    .Where(d => d.Id == documentId && d.UserId == userId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", documentId);
                return null;
            }
        }

        public async Task<List<Document>> GetUserDocumentsAsync(Guid userId, DocumentFilterDto? filter = null)
        {
            try
            {
                var query = _supabaseClient
                    .From<Document>()
                    .Select("*")
                    .Where(d => d.UserId == userId && d.Status != "deleted");

                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.DocumentType))
                    {
                        query = query.Where(d => d.DocumentType == filter.DocumentType);
                    }

                    if (!string.IsNullOrEmpty(filter.Status))
                    {
                        query = query.Where(d => d.Status == filter.Status);
                    }

                    if (filter.IsPublic.HasValue)
                    {
                        query = query.Where(d => d.IsPublic == filter.IsPublic.Value);
                    }
                }

                query = query.Order(d => d.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending);

                var response = await query.Get();
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user documents");
                return new List<Document>();
            }
        }

        public async Task<bool> DeleteDocumentAsync(Guid documentId, Guid userId)
        {
            try
            {
                // Get document first to check ownership
                var document = await GetDocumentAsync(documentId, userId);
                if (document == null)
                {
                    return false;
                }

                // Soft delete: Update status to "deleted"
                document.Status = "deleted";
                document.UpdatedAt = DateTime.UtcNow;

                await _supabaseClient
                    .From<Document>()
                    .Where(d => d.Id == documentId && d.UserId == userId)
                    .Update(document);

                _logger.LogInformation("Document soft deleted successfully: {DocumentId}", documentId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting document {DocumentId}", documentId);
                return false;
            }
        }

        public async Task<Document?> UpdateDocumentAsync(Guid documentId, Guid userId, DocumentUpdateDto dto)
        {
            try
            {
                var document = await GetDocumentAsync(documentId, userId);
                if (document == null)
                {
                    return null;
                }

                // Update fields
                if (!string.IsNullOrEmpty(dto.Name))
                    document.Name = dto.Name;
                
                if (dto.Description != null)
                    document.Description = dto.Description;
                
                if (dto.Content != null)
                    document.Content = dto.Content;
                
                if (!string.IsNullOrEmpty(dto.DocumentType))
                    document.DocumentType = dto.DocumentType;
                
                if (dto.Tags != null)
                    document.Tags = dto.Tags;
                
                if (dto.IsPublic.HasValue)
                    document.IsPublic = dto.IsPublic.Value;

                document.UpdatedAt = DateTime.UtcNow;

                // Update in database
                var response = await _supabaseClient
                    .From<Document>()
                    .Where(d => d.Id == documentId && d.UserId == userId)
                    .Update(document);

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId}", documentId);
                return null;
            }
        }

        public async Task<(Stream? stream, string? contentType, string? fileName)> DownloadDocumentAsync(Guid documentId, Guid userId)
        {
            try
            {
                var document = await GetDocumentAsync(documentId, userId);
                if (document == null)
                {
                    return (null, null, null);
                }

                var stream = await _blobService.DownloadFileAsync(document.BlobName, "nutrisha-documents");
                return (stream, document.MimeType, document.OriginalFilename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", documentId);
                return (null, null, null);
            }
        }

        public async Task<Document?> ProcessDocumentContentAsync(Guid documentId)
        {
            try
            {
                var document = await _supabaseClient
                    .From<Document>()
                    .Select("*")
                    .Where(d => d.Id == documentId)
                    .Single();

                if (document == null)
                {
                    return null;
                }

                // Download file from blob storage
                var stream = await _blobService.DownloadFileAsync(document.BlobName, "nutrisha-documents");
                if (stream == null)
                {
                    _logger.LogError("Could not download document from blob storage: {DocumentId}", documentId);
                    return null;
                }

                // Extract content based on file type
                DocumentContent extractedContent;
                if (document.MimeType == "application/pdf")
                {
                    extractedContent = await _extractionService.ExtractContentFromPdfAsync(stream);
                }
                else if (document.MimeType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
                {
                    extractedContent = await _extractionService.ExtractContentFromWordAsync(stream);
                }
                else if (document.MimeType != null && (document.MimeType.StartsWith("image/") || document.MimeType == "image/jpg"))
                {
                    extractedContent = await _extractionService.ExtractContentFromImageAsync(stream, document.MimeType);
                }
                else
                {
                    _logger.LogWarning("Unsupported document type for extraction: {MimeType}", document.MimeType);
                    return null;
                }

                // Generate tags using AI
                var tags = await _extractionService.GenerateTagsFromContentAsync(extractedContent.Text);

                // Update document with extracted content
                document.Content = extractedContent.Text;
                document.Metadata = extractedContent.Metadata;
                if (tags?.Any() == true)
                {
                    document.Tags = (document.Tags ?? new List<string>()).Union(tags).ToList();
                }
                document.ProcessedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;

                // Update in database
                await _supabaseClient
                    .From<Document>()
                    .Where(d => d.Id == documentId)
                    .Update(document);

                _logger.LogInformation("Document content processed successfully: {DocumentId}", documentId);
                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document content for {DocumentId}", documentId);
                
                // Update document status to indicate processing error
                try
                {
                    var errorDoc = new Document { Status = "processing_error", UpdatedAt = DateTime.UtcNow };
                    await _supabaseClient
                        .From<Document>()
                        .Where(d => d.Id == documentId)
                        .Update(errorDoc);
                }
                catch { }
                
                return null;
            }
        }
    }
}