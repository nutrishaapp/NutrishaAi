using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutrishaAI.API.Models;
using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.API.Services;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(
            IDocumentService documentService,
            ILogger<DocumentController> logger)
        {
            _documentService = documentService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a new document (PDF, Word, or Image)
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(10_485_760)] // 10MB limit
        [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadDocument([FromForm] DocumentUploadRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User ID not found in token");
                }

                // Validate file
                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest("File is required");
                }

                // Check file extension
                var allowedExtensions = new[] { ".pdf", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = System.IO.Path.GetExtension(request.File.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Only PDF, Word documents, and images (JPEG, PNG, GIF, WebP) are allowed");
                }

                // Map request to DTO
                var dto = new DocumentUploadDto
                {
                    Name = request.Name,
                    Description = request.Description,
                    DocumentType = request.DocumentType,
                    Tags = request.Tags,
                    IsPublic = request.IsPublic
                };

                // Upload document
                var document = await _documentService.UploadDocumentAsync(userId, request.File, dto);

                var response = new DocumentUploadResponse
                {
                    Id = document.Id,
                    Name = document.Name,
                    Status = document.Status,
                    Message = "Document uploaded successfully. Content extraction is in progress.",
                    CreatedAt = document.CreatedAt
                };

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation during document upload");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, "An error occurred while uploading the document");
            }
        }

        /// <summary>
        /// Get a specific document by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDocument(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var document = await _documentService.GetDocumentAsync(id, userId);
                
                if (document == null)
                {
                    return NotFound("Document not found");
                }

                var response = MapToDocumentResponse(document);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
                return StatusCode(500, "An error occurred while retrieving the document");
            }
        }

        /// <summary>
        /// Get all documents for the current user with optional filtering
        /// </summary>
        [HttpGet("list")]
        [ProducesResponseType(typeof(DocumentListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserDocuments([FromQuery] DocumentFilterDto? filter)
        {
            try
            {
                var userId = GetUserId();
                var documents = await _documentService.GetUserDocumentsAsync(userId, filter);
                
                var response = new DocumentListResponse
                {
                    Documents = documents.Select(MapToDocumentResponse).ToList(),
                    TotalCount = documents.Count,
                    Offset = filter?.Offset ?? 0,
                    Limit = filter?.Limit ?? 50
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user documents");
                return StatusCode(500, "An error occurred while retrieving documents");
            }
        }

        /// <summary>
        /// Update document metadata
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDocument(Guid id, [FromBody] DocumentUpdateDto request)
        {
            try
            {
                var userId = GetUserId();
                var document = await _documentService.UpdateDocumentAsync(id, userId, request);
                
                if (document == null)
                {
                    return NotFound("Document not found");
                }

                var response = MapToDocumentResponse(document);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId}", id);
                return StatusCode(500, "An error occurred while updating the document");
            }
        }

        /// <summary>
        /// Delete a document
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var result = await _documentService.DeleteDocumentAsync(id, userId);
                
                if (!result)
                {
                    return NotFound("Document not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return StatusCode(500, "An error occurred while deleting the document");
            }
        }

        /// <summary>
        /// Download a document
        /// </summary>
        [HttpGet("{id}/download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadDocument(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var (stream, contentType, fileName) = await _documentService.DownloadDocumentAsync(id, userId);
                
                if (stream == null)
                {
                    return NotFound("Document not found");
                }

                return File(stream, contentType ?? "application/octet-stream", fileName ?? "document");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", id);
                return StatusCode(500, "An error occurred while downloading the document");
            }
        }

        /// <summary>
        /// Reprocess document content extraction
        /// </summary>
        [HttpPost("{id}/reprocess")]
        [ProducesResponseType(typeof(DocumentProcessingStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReprocessDocument(Guid id)
        {
            try
            {
                var userId = GetUserId();
                
                // Verify ownership
                var document = await _documentService.GetDocumentAsync(id, userId);
                if (document == null)
                {
                    return NotFound("Document not found");
                }

                // Trigger reprocessing
                var processedDocument = await _documentService.ProcessDocumentContentAsync(id);
                
                if (processedDocument == null)
                {
                    return StatusCode(500, "Failed to process document content");
                }

                var response = new DocumentProcessingStatusResponse
                {
                    Id = processedDocument.Id,
                    Status = processedDocument.Status,
                    ProcessedAt = processedDocument.ProcessedAt,
                    HasContent = !string.IsNullOrEmpty(processedDocument.Content),
                    Tags = processedDocument.Tags
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reprocessing document {DocumentId}", id);
                return StatusCode(500, "An error occurred while reprocessing the document");
            }
        }

        /// <summary>
        /// Get document processing status
        /// </summary>
        [HttpGet("{id}/status")]
        [ProducesResponseType(typeof(DocumentProcessingStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDocumentStatus(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var document = await _documentService.GetDocumentAsync(id, userId);
                
                if (document == null)
                {
                    return NotFound("Document not found");
                }

                var response = new DocumentProcessingStatusResponse
                {
                    Id = document.Id,
                    Status = document.Status,
                    ProcessedAt = document.ProcessedAt,
                    HasContent = !string.IsNullOrEmpty(document.Content),
                    Tags = document.Tags
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document status {DocumentId}", id);
                return StatusCode(500, "An error occurred while retrieving document status");
            }
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value 
                ?? User.FindFirst("sub")?.Value 
                ?? User.FindFirst("nameid")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private DocumentResponse MapToDocumentResponse(Document document)
        {
            return new DocumentResponse
            {
                Id = document.Id,
                Name = document.Name,
                Description = document.Description,
                DocumentType = document.DocumentType,
                Status = document.Status,
                OriginalFilename = document.OriginalFilename,
                FileSize = document.FileSize,
                MimeType = document.MimeType,
                Content = document.Content,
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt,
                ProcessedAt = document.ProcessedAt,
                IsPublic = document.IsPublic,
                Tags = document.Tags,
                Metadata = document.Metadata
            };
        }
    }
}