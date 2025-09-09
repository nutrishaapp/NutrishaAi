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
    public class ChatController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly IMessageService _messageService;
        private readonly IChatOrchestrationService _chatOrchestrationService;
        private readonly ISupabaseRealtimeService _realtimeService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IConversationService conversationService,
            IMessageService messageService,
            IChatOrchestrationService chatOrchestrationService,
            ISupabaseRealtimeService realtimeService,
            ILogger<ChatController> logger)
        {
            _conversationService = conversationService;
            _messageService = messageService;
            _chatOrchestrationService = chatOrchestrationService;
            _realtimeService = realtimeService;
            _logger = logger;
        }

        [HttpPost("conversations")]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized();

                var response = await _conversationService.CreateConversationAsync(userId.Value, request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation");
                return StatusCode(500, new { error = "Failed to create conversation" });
            }
        }

        [HttpPut("conversations/{conversationId}/mode")]
        public async Task<IActionResult> UpdateConversationMode(Guid conversationId, [FromBody] UpdateConversationModeRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized();

                var response = await _conversationService.UpdateConversationModeAsync(conversationId, userId.Value, request.Mode);
                return Ok(new { 
                    message = "Conversation mode updated successfully",
                    mode = request.Mode 
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating conversation mode");
                return StatusCode(500, new { error = "Failed to update conversation mode" });
            }
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized();

                var conversations = await _conversationService.GetUserConversationsAsync(userId.Value);
                return Ok(conversations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations");
                return StatusCode(500, new { error = "Failed to get conversations" });
            }
        }

        [HttpGet("messages/{conversationId}")]
        public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized();

                // Verify user has access to this conversation
                var hasAccess = await _conversationService.UserHasAccessToConversationAsync(conversationId, userId.Value);
                if (!hasAccess)
                    return NotFound(new { error = "Conversation not found" });

                var messages = await _messageService.GetMessagesAsync(conversationId, limit, offset);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages");
                return StatusCode(500, new { error = "Failed to get messages" });
            }
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = GetUserId();
                var userRole = GetUserRole();
                if (userId == null)
                    return Unauthorized();

                var response = await _chatOrchestrationService.SendUserMessageAsync(userId.Value, userRole, request);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { error = "Failed to send message" });
            }
        }

        [HttpPost("send-multimedia")]
        public async Task<IActionResult> SendMultimediaMessage([FromBody] SendMultimediaMessageRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized();

                // Verify user has access to conversation
                var hasAccess = await _conversationService.UserHasAccessToConversationAsync(request.ConversationId, userId.Value);
                if (!hasAccess)
                    return NotFound(new { error = "Conversation not found" });

                var messageResponse = await _messageService.SaveMultimediaMessageAsync(request.ConversationId, userId.Value, request);

                // Create media attachment record if blob name is provided
                if (!string.IsNullOrEmpty(request.BlobName))
                {
                    // This should be moved to a separate MediaAttachment service in future iterations
                    // For now, keeping it simple as it's just one operation
                }

                return Ok(messageResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending multimedia message");
                return StatusCode(500, new { error = "Failed to send multimedia message" });
            }
        }

        [HttpPost("process-multimedia")]
        public async Task<IActionResult> ProcessMultimedia([FromBody] ProcessMultimediaRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized();

                var response = await _chatOrchestrationService.ProcessMultimediaAsync(userId.Value, request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multimedia");
                return StatusCode(500, new { error = "Failed to process multimedia" });
            }
        }

        [HttpPost("join-channel/{conversationId}")]
        public async Task<IActionResult> JoinChannel(Guid conversationId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized();

                // Verify user has access to conversation
                var hasAccess = await _conversationService.UserHasAccessToConversationAsync(conversationId, userId.Value);
                if (!hasAccess)
                    return NotFound(new { error = "Conversation not found" });

                await _realtimeService.JoinChatChannelAsync(conversationId, userId.Value.ToString());
                return Ok(new { message = "Joined chat channel successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining chat channel");
                return StatusCode(500, new { error = "Failed to join chat channel" });
            }
        }

        [HttpPost("leave-channel/{conversationId}")]
        public async Task<IActionResult> LeaveChannel(Guid conversationId)
        {
            try
            {
                await _realtimeService.LeaveChannelAsync(conversationId);
                return Ok(new { message = "Left chat channel successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving chat channel");
                return StatusCode(500, new { error = "Failed to leave chat channel" });
            }
        }

        [HttpPost("ai-chat")]
        public async Task<IActionResult> DirectAiChat([FromBody] DirectAiChatRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized();

                var aiResponse = await _chatOrchestrationService.ProcessDirectAiChatAsync(request.Message, request.Context);
                
                return Ok(new DirectAiChatResponse
                {
                    AiResponse = aiResponse,
                    ProcessedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing direct AI chat");
                return StatusCode(500, new { error = "Failed to process AI chat" });
            }
        }

        private Guid? GetUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return string.IsNullOrEmpty(userIdString) ? null : Guid.Parse(userIdString);
        }

        private string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "user";
        }
    }
}