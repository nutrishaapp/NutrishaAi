using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.Core.Entities;
using Supabase;
using System.Text.Json;

namespace NutrishaAI.API.Services
{
    public class MessageService : IMessageService
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            Client supabaseClient,
            ILogger<MessageService> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        public async Task<List<MessageResponse>> GetMessagesAsync(Guid conversationId, int limit = 50, int offset = 0)
        {
            try
            {
                var messages = await _supabaseClient
                    .From<Message>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId.ToString())
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Limit(limit)
                    .Offset(offset)
                    .Get();

                return messages.Models.Select(m => new MessageResponse
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId = m.SenderId,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    IsAiGenerated = m.IsAiGenerated,
                    Attachments = ConvertAttachmentsFromJson(m.Attachments),
                    CreatedAt = m.CreatedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<MessageResponse> SaveUserMessageAsync(Guid conversationId, Guid senderId, SendMessageRequest request)
        {
            try
            {
                // Determine message type based on attachments
                string messageType = "text";
                if (request.Attachments != null && request.Attachments.Any())
                {
                    var primaryAttachment = request.Attachments.First();
                    messageType = primaryAttachment.Type switch
                    {
                        "document" => "document",
                        "image" => "image",
                        "voice" => "voice",
                        "audio" => "voice",
                        _ => request.MessageType ?? "text"
                    };
                }
                else if (!string.IsNullOrEmpty(request.MessageType))
                {
                    messageType = request.MessageType;
                }

                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderId = senderId,
                    Content = request.Content,
                    MessageType = messageType,
                    IsAiGenerated = false,
                    Attachments = request.Attachments,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<Message>()
                    .Insert(message);

                return new MessageResponse
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user message for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<MessageResponse> SaveAiMessageAsync(Guid conversationId, string content)
        {
            try
            {
                var aiMessage = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderId = null, // NULL for AI messages
                    Content = content,
                    MessageType = "text",
                    IsAiGenerated = true,
                    CreatedAt = DateTime.UtcNow.AddSeconds(1) // Ensure AI message comes after user message
                };

                await _supabaseClient
                    .From<Message>()
                    .Insert(aiMessage);

                return new MessageResponse
                {
                    Id = aiMessage.Id,
                    ConversationId = aiMessage.ConversationId,
                    SenderId = aiMessage.SenderId,
                    Content = aiMessage.Content,
                    MessageType = aiMessage.MessageType,
                    IsAiGenerated = aiMessage.IsAiGenerated,
                    CreatedAt = aiMessage.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving AI message for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<MessageResponse> SaveMultimediaMessageAsync(Guid conversationId, Guid senderId, SendMultimediaMessageRequest request)
        {
            try
            {
                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    SenderId = senderId,
                    Content = request.Content,
                    MessageType = request.MessageType,
                    IsAiGenerated = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<Message>()
                    .Insert(message);

                return new MessageResponse
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    SenderId = message.SenderId,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    IsAiGenerated = message.IsAiGenerated,
                    CreatedAt = message.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving multimedia message for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task UpdateConversationTimestampAsync(Guid conversationId)
        {
            try
            {
                await _supabaseClient
                    .From<Conversation>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, conversationId.ToString())
                    .Set(c => c.UpdatedAt!, DateTime.UtcNow)
                    .Update();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update conversation timestamp for {ConversationId}", conversationId);
            }
        }

        public async Task<List<Message>> GetRecentMessagesForContextAsync(Guid conversationId, int limit = 5)
        {
            try
            {
                var recentMessages = await _supabaseClient
                    .From<Message>()
                    .Filter("conversation_id", Supabase.Postgrest.Constants.Operator.Equals, conversationId.ToString())
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
                    .Get();

                return recentMessages.Models.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent messages for conversation {ConversationId}", conversationId);
                throw;
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
                var json = attachmentsJson.ToString();
                if (string.IsNullOrEmpty(json))
                    return null;

                var attachments = JsonSerializer.Deserialize<List<AttachmentDto>>(json);
                return ConvertToMediaAttachmentResponses(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize attachments from JSON");
                return null;
            }
        }
    }
}