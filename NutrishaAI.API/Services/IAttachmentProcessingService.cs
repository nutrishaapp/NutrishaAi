using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;

namespace NutrishaAI.API.Services
{
    public interface IAttachmentProcessingService
    {
        Task<(string combinedPrompt, List<AttachmentContent> attachments)> ProcessAttachmentsAsync(List<AttachmentDto>? attachments, string? userMessage);
        Task<List<AttachmentContent>> DownloadAndConvertAttachmentsAsync(List<AttachmentDto> attachments, string containerName);
        string GetMimeTypeFromExtension(string fileName, string defaultMimeType = "application/octet-stream");
        List<MediaAttachmentResponse>? ConvertToMediaAttachmentResponses(List<AttachmentDto>? attachments);
    }
}