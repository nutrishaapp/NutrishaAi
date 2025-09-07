using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.API.Models.Requests;
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
    public class ChatController : ControllerBase
    {
        private readonly Client _supabaseClient;
        private readonly ISupabaseRealtimeService _realtimeService;
        private readonly IAzureBlobService _blobService;
        private readonly IGeminiService _geminiService;
        private readonly ISimpleGeminiService _simpleGeminiService;
        // private readonly IQdrantService _qdrantService;
        private readonly ILogger<ChatController> _logger;
        private readonly IConfiguration _configuration;

        public ChatController(
            Client supabaseClient,
            ISupabaseRealtimeService realtimeService,
            IAzureBlobService blobService,
            IGeminiService geminiService,
            ISimpleGeminiService simpleGeminiService,
            // IQdrantService qdrantService,
            ILogger<ChatController> logger,
            IConfiguration configuration)
        {
            _supabaseClient = supabaseClient;
            _realtimeService = realtimeService;
            _blobService = blobService;
            _geminiService = geminiService;
            _simpleGeminiService = simpleGeminiService;
            // _qdrantService = qdrantService;
            _logger = logger;
            _configuration = configuration;
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
                    ConversationMode = request.ConversationMode ?? "ai",
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
                    ConversationMode = conversation.ConversationMode,
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

        [HttpPut("conversations/{conversationId}/mode")]
        public async Task<IActionResult> UpdateConversationMode(Guid conversationId, [FromBody] UpdateConversationModeRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Get the conversation
                var conversationResult = await _supabaseClient
                    .From<Conversation>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, conversationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Get();

                var conversation = conversationResult.Models.FirstOrDefault();
                if (conversation == null)
                    return NotFound(new { error = "Conversation not found" });

                // Update the mode
                conversation.ConversationMode = request.Mode;
                conversation.UpdatedAt = DateTime.UtcNow;

                await _supabaseClient
                    .From<Conversation>()
                    .Where(c => c.Id == conversationId)
                    .Update(conversation);

                return Ok(new { 
                    message = "Conversation mode updated successfully",
                    mode = request.Mode 
                });
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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var conversations = await _supabaseClient
                    .From<Conversation>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Order("updated_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var response = conversations.Models.Select(c => new ConversationResponse
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    NutritionistId = c.NutritionistId,
                    Title = c.Title,
                    Status = c.Status,
                    ConversationMode = c.ConversationMode,
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
                var conversationResult = await _supabaseClient
                    .From<Conversation>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, conversationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Get();
                
                var conversation = conversationResult.Models.FirstOrDefault();

                if (conversation == null)
                    return NotFound(new { error = "Conversation not found" });

                var messages = await _supabaseClient
                    .From<Message>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId.ToString())
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Ascending)
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
                    Attachments = ConvertAttachmentsFromJson(m.Attachments),
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
                var conversationResult = await _supabaseClient
                    .From<Conversation>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, request.ConversationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Get();
                
                var conversation = conversationResult.Models.FirstOrDefault();

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
                    Attachments = request.Attachments, // Store attachments in JSONB column
                    CreatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<Message>()
                    .Insert(message);

                // Update conversation's updated_at using direct SQL update to bypass RLS
                try
                {
                    var updateResult = await _supabaseClient
                        .From<Conversation>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, request.ConversationId.ToString())
                        .Set(c => c.UpdatedAt!, DateTime.UtcNow)
                        .Update();
                }
                catch (Exception updateEx)
                {
                    _logger.LogWarning(updateEx, "Failed to update conversation timestamp for {ConversationId}, but message was saved", request.ConversationId);
                }

                // Send message via Realtime
                try
                {
                    await _realtimeService.SendMessageAsync(request.ConversationId, message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send realtime message for conversation {ConversationId}", request.ConversationId);
                }

                // Generate AI response only if conversation mode is "ai"
                if (conversation.ConversationMode == "ai")
                {
                    try
                    {
                        // Get recent conversation context
                    var recentMessages = await _supabaseClient
                        .From<Message>()
                        .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, request.ConversationId.ToString())
                        .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                        .Limit(5)
                        .Get();

                    var conversationContext = string.Join("\n", 
                        recentMessages.Models.Select(m => $"{(m.IsAiGenerated ? "AI" : "User")}: {m.Content}"));
                    
                    // Build prompt with attachment context and prepare attachments
                    string combinedPrompt = request.Content ?? "";
                    string attachmentPrompt = "";
                    var attachments = new List<AttachmentContent>();
                    
                    if (request.Attachments != null && request.Attachments.Any())
                    {
                        var imageAttachments = request.Attachments.Where(a => a.Type == "image").ToList();
                        
                        if (imageAttachments.Any())
                        {
                            var containerName = _configuration["AzureStorage:ContainerNames:UserUploads"] ?? "user-uploads";
                            
                            foreach (var imageAttachment in imageAttachments)
                            {
                                try
                                {
                                    // Download image from Azure Blob Storage and convert to base64
                                    var imageStream = await _blobService.DownloadFileAsync(imageAttachment.Url, containerName);
                                    using var ms = new MemoryStream();
                                    await imageStream.CopyToAsync(ms);
                                    var imageBytes = ms.ToArray();
                                    var base64Image = Convert.ToBase64String(imageBytes);
                                    
                                    attachments.Add(new AttachmentContent
                                    {
                                        Base64Data = base64Image,
                                        MimeType = "image/jpeg", // Could be enhanced to detect actual type
                                        Type = "image"
                                    });
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error processing image attachment {Url}", imageAttachment.Url);
                                }
                            }
                            
                            // Add image context to prompt
                            var imageCount = imageAttachments.Count;
                            attachmentPrompt += $"Please analyze the {imageCount} image{(imageCount > 1 ? "s" : "")} the user has shared. ";
                        }
                        
                        // Handle other attachment types with prompts (for future expansion)
                        var documentCount = request.Attachments.Count(a => a.Type == "document");
                        var voiceCount = request.Attachments.Count(a => a.Type == "voice");
                        
                        if (documentCount > 0)
                        {
                            attachmentPrompt += $"Please read the {documentCount} document{(documentCount > 1 ? "s" : "")} the user has provided. ";
                        }
                        
                        if (voiceCount > 0)
                        {
                            attachmentPrompt += $"Please listen to the {voiceCount} voice note{(voiceCount > 1 ? "s" : "")} from the user. ";
                        }
                        
                        // Build combined prompt with attachment context
                        if (!string.IsNullOrEmpty(attachmentPrompt))
                        {
                            combinedPrompt = $"{attachmentPrompt}\n\nUser message: {request.Content ?? "No text message"}";
                        }
                    }
                    
                    // Generate AI nutritionist response using unified method
                    var aiResponseText = await _simpleGeminiService.GenerateNutritionistResponseAsync(
                        combinedPrompt, 
                        conversationContext,
                        attachments.Any() ? attachments : null);
                    
                    // Create AI response message
                    if (!string.IsNullOrEmpty(aiResponseText))
                    {
                        var aiMessage = new Message
                        {
                            Id = Guid.NewGuid(),
                            ConversationId = request.ConversationId,
                            SenderId = null, // NULL for AI messages
                            Content = aiResponseText,
                            MessageType = "text",
                            IsAiGenerated = true,
                            CreatedAt = DateTime.UtcNow.AddSeconds(1) // Ensure AI message comes after user message
                        };

                        await _supabaseClient
                            .From<Message>()
                            .Insert(aiMessage);

                        // Send AI response via Realtime
                        try
                        {
                            await _realtimeService.SendMessageAsync(request.ConversationId, aiMessage);
                        }
                        catch (Exception realtimeEx)
                        {
                            _logger.LogWarning(realtimeEx, "Failed to send AI response via realtime for conversation {ConversationId}", request.ConversationId);
                        }
                    }
                    
                    // Store health data in Qdrant vector database
                    // var conversationEmbedding = new ConversationEmbedding
                    // {
                    //     UserId = Guid.Parse(userId),
                    //     ConversationId = request.ConversationId,
                    //     MessageId = message.Id,
                    //     Content = request.Content ?? "",
                    //     ContentType = "text",
                    //     Timestamp = DateTime.UtcNow,
                    //     ExtractedHealthData = new Dictionary<string, object>
                    //     {
                    //         { "foods", healthData.Foods },
                    //         { "exercises", healthData.Exercises },
                    //         { "symptoms", healthData.Symptoms },
                    //         { "dietary_restrictions", healthData.DietaryRestrictions },
                    //         { "health_goals", healthData.HealthGoals },
                    //         { "measurements", healthData.Measurements }
                    //     },
                    //     MessageType = request.MessageType,
                    //     AiResponse = aiResponseText,
                    //     Priority = !string.IsNullOrEmpty(healthData.Summary) ? 2 : 1
                    // };
                    
                    // await _qdrantService.StoreConversationDataAsync(conversationEmbedding);
                    _logger.LogInformation("Stored conversation data in Qdrant for message {MessageId}", message.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process message with Gemini AI for conversation {ConversationId}", request.ConversationId);
                    }
                }

                var response = new MessageResponse
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    SenderId = message.SenderId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    IsAiGenerated = message.IsAiGenerated,
                    Attachments = ConvertToMediaAttachmentResponses(request.Attachments),
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
                var conversationResult = await _supabaseClient
                    .From<Conversation>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, request.ConversationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Get();
                
                var conversation = conversationResult.Models.FirstOrDefault();

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
                    // Get full blob URL
                    var blobUrl = await _blobService.GetBlobUrlAsync(request.BlobName, "user-uploads");

                    var attachment = new MediaAttachment
                    {
                        Id = Guid.NewGuid(),
                        MessageId = message.Id,
                        FileUrl = blobUrl,
                        FileType = request.MessageType,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _supabaseClient
                        .From<MediaAttachment>()
                        .Insert(attachment);
                }

                // Process multimedia with Gemini AI
                try
                {
                    if (!string.IsNullOrEmpty(request.BlobName))
                    {
                        // Download blob from Azure Storage
                        using var blobStream = await _blobService.DownloadFileAsync(request.BlobName, "user-uploads");
                        var blobInfo = await _blobService.GetBlobInfoAsync(request.BlobName, "user-uploads");
                        
                        // Process with Gemini AI
                        var geminiResponse = await _geminiService.ProcessMultimediaAsync(
                            blobStream, 
                            blobInfo.ContentType, 
                            request.Content);
                        
                        // Extract health data
                        var healthData = await _geminiService.ExtractHealthDataAsync(
                            geminiResponse.Text, 
                            geminiResponse.ContentType);
                        
                        // Create AI response message
                        if (!string.IsNullOrEmpty(geminiResponse.Text))
                        {
                            var aiMessage = new Message
                            {
                                Id = Guid.NewGuid(),
                                ConversationId = request.ConversationId,
                                SenderId = null, // NULL for AI messages
                                Content = geminiResponse.Text,
                                MessageType = "ai_response",
                                IsAiGenerated = true,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _supabaseClient
                                .From<Message>()
                                .Insert(aiMessage);

                            // Send AI response via Realtime
                            await _realtimeService.SendMessageAsync(request.ConversationId, aiMessage);
                        }
                        
                        // Store extracted data in Qdrant vector database
                        // var multimediaEmbedding = new ConversationEmbedding
                        // {
                        //     UserId = Guid.Parse(userId),
                        //     ConversationId = request.ConversationId,
                        //     MessageId = message.Id,
                        //     Content = geminiResponse.Text,
                        //     ContentType = geminiResponse.ContentType,
                        //     Timestamp = DateTime.UtcNow,
                        //     ExtractedHealthData = new Dictionary<string, object>
                        //     {
                        //         { "foods", healthData.Foods },
                        //         { "exercises", healthData.Exercises },
                        //         { "symptoms", healthData.Symptoms },
                        //         { "dietary_restrictions", healthData.DietaryRestrictions },
                        //         { "health_goals", healthData.HealthGoals },
                        //         { "measurements", healthData.Measurements }
                        //     },
                        //     MessageType = request.MessageType,
                        //     AiResponse = geminiResponse.Text,
                        //     Priority = 2 // Higher priority for multimedia content
                        // };
                        
                        // await _qdrantService.StoreConversationDataAsync(multimediaEmbedding);
                        _logger.LogInformation("Stored multimedia conversation data in Qdrant for message {MessageId}", message.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process multimedia with Gemini AI for conversation {ConversationId}", request.ConversationId);
                }

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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Get blob from Azure Storage
                using var blobStream = await _blobService.DownloadFileAsync(request.BlobName, "user-uploads");
                var blobInfo = await _blobService.GetBlobInfoAsync(request.BlobName, "user-uploads");
                
                // Process with Gemini AI
                var geminiResponse = await _geminiService.ProcessMultimediaAsync(
                    blobStream, 
                    blobInfo.ContentType, 
                    request.TextPrompt);
                
                // Extract health data
                var healthData = await _geminiService.ExtractHealthDataAsync(
                    geminiResponse.Text, 
                    geminiResponse.ContentType);
                
                // Store extracted data in Qdrant vector database
                // var processedEmbedding = new ConversationEmbedding
                // {
                //     UserId = Guid.Parse(userId),
                //     ConversationId = request.ConversationId,
                //     Content = geminiResponse.Text,
                //     ContentType = geminiResponse.ContentType,
                //     Timestamp = DateTime.UtcNow,
                //     ExtractedHealthData = new Dictionary<string, object>
                //     {
                //         { "foods", healthData.Foods },
                //         { "exercises", healthData.Exercises },
                //         { "symptoms", healthData.Symptoms },
                //         { "dietary_restrictions", healthData.DietaryRestrictions },
                //         { "health_goals", healthData.HealthGoals },
                //         { "measurements", healthData.Measurements }
                //     },
                //     MessageType = request.MessageType,
                //     AiResponse = geminiResponse.Text,
                //     Priority = 3 // Highest priority for processed multimedia
                // };
                
                // await _qdrantService.StoreConversationDataAsync(processedEmbedding);
                
                return Ok(new ProcessedMessageResponse
                {
                    MessageId = Guid.NewGuid(),
                    AiResponse = geminiResponse.Text,
                    BlobUrl = await _blobService.GetBlobUrlAsync(request.BlobName, "user-uploads"),
                    ExtractedHealthData = new Dictionary<string, object>
                    {
                        { "foods", healthData.Foods },
                        { "exercises", healthData.Exercises },
                        { "symptoms", healthData.Symptoms },
                        { "dietary_restrictions", healthData.DietaryRestrictions },
                        { "health_goals", healthData.HealthGoals },
                        { "measurements", healthData.Measurements },
                        { "summary", healthData.Summary }
                    }
                });
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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Verify user has access to this conversation
                var conversationResult = await _supabaseClient
                    .From<Conversation>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, conversationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Get();
                
                var conversation = conversationResult.Models.FirstOrDefault();

                if (conversation == null)
                    return NotFound(new { error = "Conversation not found" });

                await _realtimeService.JoinChatChannelAsync(conversationId, userId);

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

        private List<MediaAttachmentResponse>? ConvertToMediaAttachmentResponses(List<AttachmentDto>? attachments)
        {
            if (attachments == null || !attachments.Any())
                return null;

            return attachments.Select(a => new MediaAttachmentResponse
            {
                Id = Guid.NewGuid(),
                FileUrl = a.Url,
                FileType = a.Type,
                FileName = a.Name,
                FileSize = a.Size.HasValue ? (int)a.Size.Value : null,
                CreatedAt = DateTime.UtcNow
            }).ToList();
        }

        private List<MediaAttachmentResponse>? ConvertAttachmentsFromJson(object? attachmentsJson)
        {
            if (attachmentsJson == null)
                return null;

            try
            {
                // Convert JSONB to List<AttachmentDto>
                var json = attachmentsJson.ToString();
                if (string.IsNullOrEmpty(json))
                    return null;

                var attachments = System.Text.Json.JsonSerializer.Deserialize<List<AttachmentDto>>(json);
                return ConvertToMediaAttachmentResponses(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize attachments from JSON");
                return null;
            }
        }

        [HttpPost("ai-chat")]
        public async Task<IActionResult> DirectAiChat([FromBody] DirectAiChatRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Process text with Gemini AI
                var aiResponse = await _geminiService.ProcessTextAsync(
                    request.Message, 
                    request.Context);
                
                // Extract health data if requested
                HealthDataExtractionResult? healthData = null;
                if (request.ExtractHealthData)
                {
                    healthData = await _geminiService.ExtractHealthDataAsync(request.Message, "text");
                }

                return Ok(new DirectAiChatResponse
                {
                    AiResponse = aiResponse,
                    ExtractedHealthData = healthData,
                    ProcessedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing direct AI chat");
                return StatusCode(500, new { error = "Failed to process AI chat" });
            }
        }
    }
}