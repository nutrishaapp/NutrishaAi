using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.Core.Entities;

namespace NutrishaAI.API.Services
{
    public interface IConversationService
    {
        Task<ConversationResponse> CreateConversationAsync(Guid userId, CreateConversationRequest request);
        Task<ConversationResponse> UpdateConversationModeAsync(Guid conversationId, Guid userId, string mode);
        Task<List<ConversationResponse>> GetUserConversationsAsync(Guid userId);
        Task<Conversation?> GetConversationByIdAsync(Guid conversationId, Guid userId);
        Task<bool> UserHasAccessToConversationAsync(Guid conversationId, Guid userId);
    }
}