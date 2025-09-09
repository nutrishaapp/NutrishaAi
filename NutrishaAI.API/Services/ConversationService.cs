using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.Core.Entities;
using Supabase;

namespace NutrishaAI.API.Services
{
    public class ConversationService : IConversationService
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<ConversationService> _logger;

        public ConversationService(
            Client supabaseClient,
            ILogger<ConversationService> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        public async Task<ConversationResponse> CreateConversationAsync(Guid userId, CreateConversationRequest request)
        {
            try
            {
                var conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
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

                return new ConversationResponse
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation for user {UserId}", userId);
                throw;
            }
        }

        public async Task<ConversationResponse> UpdateConversationModeAsync(Guid conversationId, Guid userId, string mode)
        {
            try
            {
                var conversation = await GetConversationByIdAsync(conversationId, userId);
                if (conversation == null)
                    throw new InvalidOperationException("Conversation not found or access denied");

                conversation.ConversationMode = mode;
                conversation.UpdatedAt = DateTime.UtcNow;

                await _supabaseClient
                    .From<Conversation>()
                    .Where(c => c.Id == conversationId)
                    .Update(conversation);

                return new ConversationResponse
                {
                    Id = conversation.Id,
                    UserId = conversation.UserId,
                    NutritionistId = conversation.NutritionistId,
                    Title = conversation.Title,
                    Status = conversation.Status,
                    ConversationMode = conversation.ConversationMode,
                    CreatedAt = conversation.CreatedAt,
                    UpdatedAt = conversation.UpdatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating conversation mode for {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task<List<ConversationResponse>> GetUserConversationsAsync(Guid userId)
        {
            try
            {
                var conversations = await _supabaseClient
                    .From<Conversation>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                    .Order("updated_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                return conversations.Models.Select(c => new ConversationResponse
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    NutritionistId = c.NutritionistId,
                    Title = c.Title,
                    Status = c.Status,
                    ConversationMode = c.ConversationMode,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Conversation?> GetConversationByIdAsync(Guid conversationId, Guid userId)
        {
            try
            {
                var conversationResult = await _supabaseClient
                    .From<Conversation>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, conversationId.ToString())
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                    .Get();

                return conversationResult.Models.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation {ConversationId} for user {UserId}", conversationId, userId);
                throw;
            }
        }

        public async Task<bool> UserHasAccessToConversationAsync(Guid conversationId, Guid userId)
        {
            try
            {
                var conversation = await GetConversationByIdAsync(conversationId, userId);
                return conversation != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user access to conversation {ConversationId} for user {UserId}", conversationId, userId);
                return false;
            }
        }
    }
}