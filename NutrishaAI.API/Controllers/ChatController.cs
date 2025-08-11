using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.Core.Entities;
using Supabase;
using System.Security.Claims;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<ChatController> _logger;
        // Note: We'll inject these services later when implemented
        // private readonly IAzureBlobService _blobService;
        // private readonly IGeminiService _geminiService;
        // private readonly IQdrantService _qdrantService;

        public ChatController(
            Client supabaseClient,
            ILogger<ChatController> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        [HttpPost("conversations")]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(userId),
                    NutritionistId = request.NutritionistId,
                    Title = request.Title ?? $"Conversation {DateTime.UtcNow:yyyy-MM-dd}",
                    Status = "active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<Conversation>()
                    .Insert(conversation);

                var response = new ConversationResponse
                {
                    Id = conversation.Id,
                    UserId = conversation.UserId,
                    NutritionistId = conversation.NutritionistId,
                    Title = conversation.Title,
                    Status = conversation.Status,
                    CreatedAt = conversation.CreatedAt,
                    UpdatedAt = conversation.UpdatedAt,
                    MessageCount = 0
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation");
                return StatusCode(500, new { error = "Failed to create conversation" });
            }
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var conversations = await _supabaseClient
                    .From<Conversation>()
                    .Where(c => c.UserId == Guid.Parse(userId))
                    .Order(c => c.UpdatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var response = conversations.Models.Select(c => new ConversationResponse
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    NutritionistId = c.NutritionistId,
                    Title = c.Title,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                });

                return Ok(response);
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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Verify user has access to this conversation
                var conversation = await _supabaseClient
                    .From<Conversation>()
                    .Where(c => c.Id == conversationId)
                    .Where(c => c.UserId == Guid.Parse(userId))
                    .Single();

                if (conversation == null)
                    return NotFound(new { error = "Conversation not found" });

                var messages = await _supabaseClient
                    .From<Message>()
                    .Where(m => m.ConversationId == conversationId)
                    .Order(m => m.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
                    .Offset(offset)
                    .Get();

                var response = messages.Models.Select(m => new MessageResponse
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId = m.SenderId,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    IsAiGenerated = m.IsAiGenerated,
                    CreatedAt = m.CreatedAt
                });

                return Ok(response);
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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Verify user has access to this conversation
                var conversation = await _supabaseClient
                    .From<Conversation>()
                    .Where(c => c.Id == request.ConversationId)
                    .Where(c => c.UserId == Guid.Parse(userId))
                    .Single();

                if (conversation == null)
                    return NotFound(new { error = "Conversation not found" });

                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = request.ConversationId,
                    SenderId = Guid.Parse(userId),
                    Content = request.Content,
                    MessageType = request.MessageType,
                    IsAiGenerated = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<Message>()
                    .Insert(message);

                // Update conversation's updated_at
                await _supabaseClient
                    .From<Conversation>()
                    .Where(c => c.Id == request.ConversationId)
                    .Set(c => c.UpdatedAt, DateTime.UtcNow)
                    .Update();

                // TODO: Process message with Gemini AI
                // TODO: Extract health data and store in Qdrant
                // TODO: Generate AI response if enabled

                var response = new MessageResponse
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    SenderId = message.SenderId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    IsAiGenerated = message.IsAiGenerated,
                    CreatedAt = message.CreatedAt
                };

                return Ok(response);
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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Verify user has access to this conversation
                var conversation = await _supabaseClient
                    .From<Conversation>()
                    .Where(c => c.Id == request.ConversationId)
                    .Where(c => c.UserId == Guid.Parse(userId))
                    .Single();

                if (conversation == null)
                    return NotFound(new { error = "Conversation not found" });

                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = request.ConversationId,
                    SenderId = Guid.Parse(userId),
                    Content = request.Content,
                    MessageType = request.MessageType,
                    IsAiGenerated = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<Message>()
                    .Insert(message);

                // Create media attachment record
                if (!string.IsNullOrEmpty(request.BlobName))
                {
                    var attachment = new MediaAttachment
                    {
                        Id = Guid.NewGuid(),
                        MessageId = message.Id,
                        FileUrl = request.BlobName, // Will be converted to full URL by blob service
                        FileType = request.MessageType,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _supabaseClient
                        .From<MediaAttachment>()
                        .Insert(attachment);
                }

                // TODO: Process multimedia with Gemini AI
                // TODO: Extract information and store in Qdrant

                var response = new MessageResponse
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    SenderId = message.SenderId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    IsAiGenerated = message.IsAiGenerated,
                    CreatedAt = message.CreatedAt
                };

                return Ok(response);
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
                // TODO: Implement when services are ready
                // 1. Get blob from Azure
                // 2. Send to Gemini for processing
                // 3. Extract health data
                // 4. Store in Qdrant
                // 5. Return processed data

                return Ok(new ProcessedMessageResponse
                {
                    MessageId = Guid.NewGuid(),
                    AiResponse = "This will be implemented with Gemini service",
                    BlobUrl = request.BlobName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multimedia");
                return StatusCode(500, new { error = "Failed to process multimedia" });
            }
        }
    }
}