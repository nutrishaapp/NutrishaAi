using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;

namespace NutrishaAI.API.Services
{
    public interface IChatOrchestrationService
    {
        Task<MessageResponse> SendUserMessageAsync(Guid userId, string userRole, SendMessageRequest request);
        Task<ProcessedMessageResponse> ProcessMultimediaAsync(Guid userId, ProcessMultimediaRequest request);
        Task<string> ProcessDirectAiChatAsync(string message, string? context);
    }
}