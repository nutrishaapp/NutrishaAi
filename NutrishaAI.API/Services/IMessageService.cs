using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.Core.Entities;

namespace NutrishaAI.API.Services
{
    public interface IMessageService
    {
        Task<List<MessageResponse>> GetMessagesAsync(Guid conversationId, int limit = 50, int offset = 0);
        Task<MessageResponse> SaveUserMessageAsync(Guid conversationId, Guid senderId, SendMessageRequest request);
        Task<MessageResponse> SaveAiMessageAsync(Guid conversationId, string content);
        Task<MessageResponse> SaveMultimediaMessageAsync(Guid conversationId, Guid senderId, SendMultimediaMessageRequest request);
        Task UpdateConversationTimestampAsync(Guid conversationId);
        Task<List<Message>> GetRecentMessagesForContextAsync(Guid conversationId, int limit = 5);
    }
}