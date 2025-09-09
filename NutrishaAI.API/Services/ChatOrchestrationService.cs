using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.Core.Entities;
using System.Security.Claims;

namespace NutrishaAI.API.Services
{
    public class ChatOrchestrationService : IChatOrchestrationService
    {
        private readonly IConversationService _conversationService;
        private readonly IMessageService _messageService;
        private readonly IAttachmentProcessingService _attachmentService;
        private readonly ISupabaseRealtimeService _realtimeService;
        private readonly IFirebaseNotificationService _notificationService;
        private readonly ISimpleGeminiService _geminiService;
        private readonly IGeminiService _geminiServiceLegacy;
        private readonly IAzureBlobService _blobService;
        private readonly ILogger<ChatOrchestrationService> _logger;

        public ChatOrchestrationService(
            IConversationService conversationService,
            IMessageService messageService,
            IAttachmentProcessingService attachmentService,
            ISupabaseRealtimeService realtimeService,
            IFirebaseNotificationService notificationService,
            ISimpleGeminiService geminiService,
            IGeminiService geminiServiceLegacy,
            IAzureBlobService blobService,
            ILogger<ChatOrchestrationService> logger)
        {
            _conversationService = conversationService;
            _messageService = messageService;
            _attachmentService = attachmentService;
            _realtimeService = realtimeService;
            _notificationService = notificationService;
            _geminiService = geminiService;
            _geminiServiceLegacy = geminiServiceLegacy;
            _blobService = blobService;
            _logger = logger;
        }

        public async Task<MessageResponse> SendUserMessageAsync(Guid userId, string userRole, SendMessageRequest request)
        {
            try
            {
                // Verify user has access to conversation
                var conversation = await _conversationService.GetConversationByIdAsync(request.ConversationId, userId);
                if (conversation == null)
                    throw new InvalidOperationException("Conversation not found or access denied");

                // Save user message
                var messageResponse = await _messageService.SaveUserMessageAsync(request.ConversationId, userId, request);

                // Update conversation timestamp
                await _messageService.UpdateConversationTimestampAsync(request.ConversationId);

                // Send message via Realtime
                try
                {
                    await _realtimeService.SendMessageAsync(request.ConversationId, new Message
                    {
                        Id = messageResponse.Id,
                        ConversationId = messageResponse.ConversationId,
                        SenderId = messageResponse.SenderId,
                        Content = messageResponse.Content,
                        MessageType = messageResponse.MessageType,
                        IsAiGenerated = messageResponse.IsAiGenerated,
                        CreatedAt = messageResponse.CreatedAt
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send realtime message for conversation {ConversationId}", request.ConversationId);
                }

                // Send push notification if nutritionist sends message to patient
                await HandlePushNotificationAsync(request.ConversationId, userId, messageResponse, userRole, conversation);

                // Generate AI response only if conversation mode is "ai"
                if (conversation.ConversationMode == "ai")
                {
                    _ = Task.Run(async () => await HandleAiResponseAsync(request.ConversationId, request, conversation));
                }

                return messageResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendUserMessageAsync for conversation {ConversationId}", request.ConversationId);
                throw;
            }
        }

        public async Task<ProcessedMessageResponse> ProcessMultimediaAsync(Guid userId, ProcessMultimediaRequest request)
        {
            try
            {
                // Get blob from Azure Storage
                using var blobStream = await _blobService.DownloadFileAsync(request.BlobName, "user-uploads");
                var blobInfo = await _blobService.GetBlobInfoAsync(request.BlobName, "user-uploads");

                // Process with Gemini AI
                var geminiResponse = await _geminiServiceLegacy.ProcessMultimediaAsync(
                    blobStream,
                    blobInfo.ContentType,
                    request.TextPrompt);

                return new ProcessedMessageResponse
                {
                    MessageId = Guid.NewGuid(),
                    AiResponse = geminiResponse.Text,
                    BlobUrl = await _blobService.GetBlobUrlAsync(request.BlobName, "user-uploads")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multimedia for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> ProcessDirectAiChatAsync(string message, string? context)
        {
            try
            {
                return await _geminiServiceLegacy.ProcessTextAsync(message, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing direct AI chat");
                throw;
            }
        }

        private async Task HandleAiResponseAsync(Guid conversationId, SendMessageRequest request, Conversation conversation)
        {
            try
            {
                // Get recent conversation context
                var recentMessages = await _messageService.GetRecentMessagesForContextAsync(conversationId, 5);
                var conversationContext = string.Join("\n",
                    recentMessages.Select(m => $"{(m.IsAiGenerated ? "AI" : "User")}: {m.Content}"));

                // Process attachments if any
                var (combinedPrompt, attachments) = await _attachmentService.ProcessAttachmentsAsync(
                    request.Attachments, request.Content);

                // Generate AI nutritionist response
                var aiResponseText = await _geminiService.GenerateNutritionistResponseAsync(
                    combinedPrompt,
                    conversationContext,
                    attachments.Any() ? attachments : null);

                // Save AI response message
                if (!string.IsNullOrEmpty(aiResponseText))
                {
                    var aiMessageResponse = await _messageService.SaveAiMessageAsync(conversationId, aiResponseText);

                    // Send AI response via Realtime
                    try
                    {
                        await _realtimeService.SendMessageAsync(conversationId, new Message
                        {
                            Id = aiMessageResponse.Id,
                            ConversationId = aiMessageResponse.ConversationId,
                            SenderId = aiMessageResponse.SenderId,
                            Content = aiMessageResponse.Content,
                            MessageType = aiMessageResponse.MessageType,
                            IsAiGenerated = aiMessageResponse.IsAiGenerated,
                            CreatedAt = aiMessageResponse.CreatedAt
                        });
                    }
                    catch (Exception realtimeEx)
                    {
                        _logger.LogWarning(realtimeEx, "Failed to send AI response via realtime for conversation {ConversationId}", conversationId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process message with Gemini AI for conversation {ConversationId}", conversationId);
            }
        }

        private async Task HandlePushNotificationAsync(Guid conversationId, Guid senderId, MessageResponse message, string senderRole, Conversation conversation)
        {
            try
            {
                if ((senderRole == "nutritionist" || senderRole == "admin") && conversation.UserId != senderId)
                {
                    // Nutritionist is sending message to patient - send push notification
                    var notificationRequest = new SendNotificationRequest
                    {
                        Title = "Message from your Nutritionist",
                        Body = string.IsNullOrEmpty(message.Content)
                            ? "You received a new message"
                            : (message.Content.Length > 100 ? message.Content.Substring(0, 100) + "..." : message.Content),
                        Data = new Dictionary<string, string>
                        {
                            { "type", "message" },
                            { "conversationId", conversation.Id.ToString() },
                            { "senderId", senderId.ToString() },
                            { "messageType", message.MessageType }
                        }
                    };

                    var result = await _notificationService.SendNotificationToUserAsync(conversation.UserId.ToString(), notificationRequest);
                    if (result.Success)
                    {
                        _logger.LogInformation("Push notification sent successfully for message in conversation {ConversationId}", conversation.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send push notification for conversation {ConversationId}: {Error}", conversation.Id, result.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send push notification for conversation {ConversationId}", conversationId);
            }
        }
    }
}