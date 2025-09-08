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
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly IFirebaseNotificationService _notificationService;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(
            IFirebaseNotificationService notificationService,
            ILogger<NotificationController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpPost("register-token")]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterTokenRequest request)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized("User not authenticated");
                }

                // Override the userId from the request with the authenticated user's ID for security
                request.UserId = currentUserId;

                var result = await _notificationService.RegisterTokenAsync(request);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering FCM token");
                return StatusCode(500, new TokenRegistrationResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("send-to-token")]
        [Authorize(Roles = "admin,nutritionist")]
        public async Task<IActionResult> SendToToken([FromBody] SendNotificationToTokenRequest request)
        {
            try
            {
                var result = await _notificationService.SendNotificationToTokenAsync(request.Token, request);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to token");
                return StatusCode(500, new NotificationResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("send-to-user")]
        [Authorize(Roles = "admin,nutritionist")]
        public async Task<IActionResult> SendToUser([FromBody] SendNotificationToUserRequest request)
        {
            try
            {
                var result = await _notificationService.SendNotificationToUserAsync(request.UserId, request);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user");
                return StatusCode(500, new NotificationResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("send-to-topic")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SendToTopic([FromBody] SendNotificationToTopicRequest request)
        {
            try
            {
                var result = await _notificationService.SendNotificationToTopicAsync(request.Topic, request);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to topic");
                return StatusCode(500, new NotificationResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("send-to-all-users")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SendToAllUsers([FromBody] SendNotificationRequest request)
        {
            try
            {
                // Send to a general topic that all users should be subscribed to
                var result = await _notificationService.SendNotificationToTopicAsync("all_users", request);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to all users");
                return StatusCode(500, new NotificationResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("subscribe-to-topic")]
        public async Task<IActionResult> SubscribeToTopic([FromBody] SubscribeToTopicRequest request)
        {
            try
            {
                var result = await _notificationService.SubscribeToTopicAsync(request.Tokens, request.Topic);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to topic");
                return StatusCode(500, new TopicSubscriptionResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("unsubscribe-from-topic")]
        public async Task<IActionResult> UnsubscribeFromTopic([FromBody] SubscribeToTopicRequest request)
        {
            try
            {
                var result = await _notificationService.UnsubscribeFromTopicAsync(request.Tokens, request.Topic);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from topic");
                return StatusCode(500, new TopicSubscriptionResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("validate-token")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ValidateToken([FromBody] string token)
        {
            try
            {
                var isValid = await _notificationService.ValidateTokenAsync(token);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        [HttpPost("send-test-notification")]
        [Authorize]
        public async Task<IActionResult> SendTestNotification()
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized("User not authenticated");
                }

                var testRequest = new SendNotificationRequest
                {
                    Title = "Test Notification",
                    Body = "This is a test notification from NutrishaAI",
                    Data = new Dictionary<string, string>
                    {
                        { "type", "test" },
                        { "timestamp", DateTime.UtcNow.ToString("O") }
                    }
                };

                var result = await _notificationService.SendNotificationToUserAsync(currentUserId, testRequest);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test notification");
                return StatusCode(500, new NotificationResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("send-by-email")]
        [Authorize(Roles = "admin,nutritionist")]
        public async Task<IActionResult> SendByEmail([FromBody] SendNotificationByEmailRequest request)
        {
            try
            {
                var result = await _notificationService.SendNotificationByEmailAsync(request);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification by email");
                return StatusCode(500, new NotificationResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }

        [HttpPost("deactivate-device")]
        public async Task<IActionResult> DeactivateDevice([FromBody] DeactivateDeviceRequest request)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized("User not authenticated");
                }

                var result = await _notificationService.DeactivateDeviceAsync(currentUserId, request.DeviceId);
                
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating device");
                return StatusCode(500, new DeviceDeactivationResponse 
                { 
                    Success = false, 
                    Error = "Internal server error" 
                });
            }
        }
    }
}