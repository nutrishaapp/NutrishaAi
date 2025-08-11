using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.API.Models.Responses;
using System.Security.Claims;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MediaController : ControllerBase
    {
        private readonly ILogger<MediaController> _logger;
        // TODO: Inject Azure Blob Service when implemented
        // private readonly IAzureBlobService _blobService;

        public MediaController(ILogger<MediaController> logger)
        {
            _logger = logger;
        }

        [HttpPost("get-upload-url")]
        public async Task<IActionResult> GetUploadUrl([FromBody] GetUploadUrlRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // TODO: Implement when Azure Blob Service is ready
                // 1. Validate file type and size
                // 2. Generate unique blob name
                // 3. Create presigned upload URL
                // 4. Return URL with expiration

                var blobName = $"{userId}/{Guid.NewGuid()}_{request.FileName}";
                
                var response = new UploadUrlResponse
                {
                    UploadUrl = "https://placeholder-upload-url.com", // TODO: Replace with actual Azure blob URL
                    BlobName = blobName,
                    ExpiresIn = 3600 // 1 hour
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating upload URL");
                return StatusCode(500, new { error = "Failed to generate upload URL" });
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

                // TODO: Implement when database queries are ready
                // 1. Verify user has access to the message
                // 2. Get all attachments for the message
                // 3. Generate download URLs if needed

                return Ok(new List<MediaAttachmentResponse>());
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
}