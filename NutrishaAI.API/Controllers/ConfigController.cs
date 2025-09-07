using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.API.Services;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.Core.Entities;
using System.Security.Claims;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class ConfigController : ControllerBase
    {
        private readonly IAppConfigService _configService;
        private readonly ILogger<ConfigController> _logger;

        public ConfigController(
            IAppConfigService configService,
            ILogger<ConfigController> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllConfigs()
        {
            try
            {
                var configs = await _configService.GetAllConfigsAsync();
                
                // Convert to DTO to avoid serialization issues
                var configDtos = configs.Select(c => new AppConfigResponse
                {
                    Key = c.Key,
                    Value = c.Value,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    UpdatedBy = c.UpdatedBy
                }).ToList();
                
                return Ok(configDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all configs");
                return StatusCode(500, new { error = "Failed to retrieve configs" });
            }
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> GetConfig(string key)
        {
            try
            {
                var config = await _configService.GetFullConfigAsync(key);
                
                if (config == null)
                {
                    return NotFound(new { error = $"Config with key '{key}' not found" });
                }

                // Convert to DTO to avoid serialization issues
                var configDto = new AppConfigResponse
                {
                    Key = config.Key,
                    Value = config.Value,
                    CreatedAt = config.CreatedAt,
                    UpdatedAt = config.UpdatedAt,
                    UpdatedBy = config.UpdatedBy
                };

                return Ok(configDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving config: {Key}", key);
                return StatusCode(500, new { error = "Failed to retrieve config" });
            }
        }

        [HttpPut("{key}")]
        public async Task<IActionResult> UpdateConfig(string key, [FromBody] UpdateConfigRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var success = await _configService.UpdateConfigAsync(key, request.Value, Guid.Parse(userId));
                
                if (!success)
                {
                    return StatusCode(500, new { error = "Failed to update config" });
                }

                return Ok(new { message = "Config updated successfully", key, value = request.Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating config: {Key}", key);
                return StatusCode(500, new { error = "Failed to update config" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateConfig([FromBody] CreateConfigRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Check if config already exists
                var existingConfig = await _configService.GetFullConfigAsync(request.Key);
                if (existingConfig != null)
                {
                    return Conflict(new { error = $"Config with key '{request.Key}' already exists" });
                }

                var success = await _configService.CreateConfigAsync(request.Key, request.Value, Guid.Parse(userId));
                
                if (!success)
                {
                    return StatusCode(500, new { error = "Failed to create config" });
                }

                return CreatedAtAction(nameof(GetConfig), new { key = request.Key }, 
                    new { message = "Config created successfully", key = request.Key, value = request.Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating config: {Key}", request.Key);
                return StatusCode(500, new { error = "Failed to create config" });
            }
        }

        [HttpDelete("{key}")]
        public async Task<IActionResult> DeleteConfig(string key)
        {
            try
            {
                var config = await _configService.GetFullConfigAsync(key);
                if (config == null)
                {
                    return NotFound(new { error = $"Config with key '{key}' not found" });
                }

                var success = await _configService.DeleteConfigAsync(key);
                
                if (!success)
                {
                    return StatusCode(500, new { error = "Failed to delete config" });
                }

                return Ok(new { message = "Config deleted successfully", key });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting config: {Key}", key);
                return StatusCode(500, new { error = "Failed to delete config" });
            }
        }

        [HttpPost("cache/clear")]
        public IActionResult ClearCache()
        {
            try
            {
                _configService.ClearCache();
                return Ok(new { message = "Cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                return StatusCode(500, new { error = "Failed to clear cache" });
            }
        }

        [HttpPost("cache/clear/{key}")]
        public IActionResult ClearCache(string key)
        {
            try
            {
                _configService.ClearCache(key);
                return Ok(new { message = $"Cache cleared for key: {key}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache for key: {Key}", key);
                return StatusCode(500, new { error = "Failed to clear cache" });
            }
        }
    }

    public class UpdateConfigRequest
    {
        public string Value { get; set; } = string.Empty;
    }

    public class CreateConfigRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}