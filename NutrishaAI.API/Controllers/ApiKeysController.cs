using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.API.Services;
using System.Security.Claims;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requires JWT auth for management
    public class ApiKeysController : ControllerBase
    {
        private readonly IApiKeyService _apiKeyService;
        private readonly ILogger<ApiKeysController> _logger;

        public ApiKeysController(
            IApiKeyService apiKeyService,
            ILogger<ApiKeysController> logger)
        {
            _apiKeyService = apiKeyService;
            _logger = logger;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateApiKey([FromBody] GenerateApiKeyRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }
                //test
                var apiKey = await _apiKeyService.GenerateApiKey(
                    userId,
                    request.Name,
                    request.Permissions
                );

                var response = new ApiKeyResponse
                {
                    ApiKey = apiKey.FullKey ?? "",
                    ApiKeyId = apiKey.Id,
                    Name = apiKey.Name,
                    Warning = "Save this key securely. It won't be shown again."
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating API key");
                return StatusCode(500, new { error = "Failed to generate API key" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetApiKeys()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var keys = await _apiKeyService.GetUserApiKeys(userId);

                // Don't return the actual keys, just metadata
                var response = keys.Select(k => new ApiKeyListResponse
                {
                    Id = k.Id,
                    Name = k.Name,
                    Prefix = k.KeyPrefix,
                    LastUsed = k.LastUsedAt,
                    Created = k.CreatedAt,
                    IsActive = k.IsActive
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API keys");
                return StatusCode(500, new { error = "Failed to retrieve API keys" });
            }
        }

        [HttpDelete("{keyId}")]
        public async Task<IActionResult> RevokeApiKey(string keyId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Verify the key belongs to the user before revoking
                var keys = await _apiKeyService.GetUserApiKeys(userId);
                if (!keys.Any(k => k.Id.ToString() == keyId))
                {
                    return NotFound(new { error = "API key not found" });
                }

                await _apiKeyService.RevokeApiKey(keyId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking API key");
                return StatusCode(500, new { error = "Failed to revoke API key" });
            }
        }
    }
}