using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.API.Services;
using NutrishaAI.Core.Entities;
using Supabase;
using System.Security.Claims;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MediaController : ControllerBase
    {
        private readonly ILogger<MediaController> _logger;
        private readonly IAzureBlobService _blobService;
        private readonly Client _supabaseClient;
        private readonly IConfiguration _configuration;

        public MediaController(
            ILogger<MediaController> logger,
            IAzureBlobService blobService,
            Client supabaseClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _blobService = blobService;
            _supabaseClient = supabaseClient;
            _configuration = configuration;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string fileType)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file provided" });

                // Validate file type
                var allowedTypes = new[] { "image", "voice", "document", "video" };
                if (!allowedTypes.Contains(fileType.ToLowerInvariant()))
                    return BadRequest(new { error = "Invalid file type" });

                // Get container name from configuration
                var containerName = _configuration[$"AzureStorage:ContainerNames:UserUploads"] ?? "user-uploads";

                // Upload file to Azure Blob Storage
                using var stream = file.OpenReadStream();
                var blobName = await _blobService.UploadFileAsync(
                    stream, 
                    file.FileName, 
                    containerName, 
                    file.ContentType);

                // Get blob info
                var blobInfo = await _blobService.GetBlobInfoAsync(blobName, containerName);

                var response = new UploadResponse
                {
                    BlobName = blobName,
                    OriginalFileName = file.FileName,
                    Size = file.Length,
                    ContentType = file.ContentType,
                    FileType = fileType,
                    Url = blobInfo.Url,
                    UploadedAt = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new { error = "Failed to upload file" });
            }
        }

        [HttpGet("download/{blobName}")]
        public async Task<IActionResult> DownloadFile(string blobName, [FromQuery] string container = "user-uploads")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // TODO: Add authorization check - ensure user can access this blob

                if (!await _blobService.BlobExistsAsync(blobName, container))
                    return NotFound(new { error = "File not found" });

                var blobInfo = await _blobService.GetBlobInfoAsync(blobName, container);
                var stream = await _blobService.DownloadFileAsync(blobName, container);

                return File(stream, blobInfo.ContentType, blobInfo.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {BlobName}", blobName);
                return StatusCode(500, new { error = "Failed to download file" });
            }
        }

        [HttpDelete("{blobName}")]
        public async Task<IActionResult> DeleteFile(string blobName, [FromQuery] string container = "user-uploads")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // TODO: Add authorization check - ensure user can delete this blob

                var deleted = await _blobService.DeleteFileAsync(blobName, container);
                if (!deleted)
                    return NotFound(new { error = "File not found" });

                return Ok(new { message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {BlobName}", blobName);
                return StatusCode(500, new { error = "Failed to delete file" });
            }
        }

        [HttpGet("{messageId}/attachments")]
        public async Task<IActionResult> GetMessageAttachments(Guid messageId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Get message attachments from database
                var attachments = await _supabaseClient
                    .From<MediaAttachment>()
                    .Where(a => a.MessageId == messageId)
                    .Get();

                var response = attachments.Models.Select(attachment => new MediaAttachmentResponse
                {
                    Id = attachment.Id,
                    MessageId = attachment.MessageId,
                    FileUrl = attachment.FileUrl,
                    FileType = attachment.FileType,
                    CreatedAt = attachment.CreatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message attachments");
                return StatusCode(500, new { error = "Failed to get message attachments" });
            }
        }
    }

    public class GetUploadUrlRequest
    {
        public string FileType { get; set; } = string.Empty; // voice, image, document
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class UploadResponse
    {
        public string BlobName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}