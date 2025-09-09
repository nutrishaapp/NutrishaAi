using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;

namespace NutrishaAI.API.Services
{
    public class AttachmentProcessingService : IAttachmentProcessingService
    {
        private readonly IAzureBlobService _blobService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AttachmentProcessingService> _logger;

        public AttachmentProcessingService(
            IAzureBlobService blobService,
            IConfiguration configuration,
            ILogger<AttachmentProcessingService> logger)
        {
            _blobService = blobService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(string combinedPrompt, List<AttachmentContent> attachments)> ProcessAttachmentsAsync(List<AttachmentDto>? attachments, string? userMessage)
        {
            var attachmentsList = new List<AttachmentContent>();
            var attachmentPrompt = "";

            if (attachments == null || !attachments.Any())
            {
                return (userMessage ?? "", attachmentsList);
            }

            var containerName = _configuration["AzureStorage:ContainerNames:UserUploads"] ?? "user-uploads";

            // Process different attachment types
            var imageAttachments = attachments.Where(a => a.Type == "image").ToList();
            var voiceAttachments = attachments.Where(a => a.Type == "voice").ToList();
            var documentAttachments = attachments.Where(a => a.Type == "document").ToList();

            // Process image attachments
            if (imageAttachments.Any())
            {
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

                        // Determine MIME type from extension
                        var mimeType = GetMimeTypeFromExtension(imageAttachment.Url, "image/jpeg");

                        attachmentsList.Add(new AttachmentContent
                        {
                            Base64Data = base64Image,
                            MimeType = mimeType,
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

            // Process voice attachments
            if (voiceAttachments.Any())
            {
                foreach (var voiceAttachment in voiceAttachments)
                {
                    try
                    {
                        // Download voice note from Azure Blob Storage
                        var voiceStream = await _blobService.DownloadFileAsync(voiceAttachment.Url, containerName);
                        using var ms = new MemoryStream();
                        await voiceStream.CopyToAsync(ms);
                        var voiceBytes = ms.ToArray();
                        var base64Voice = Convert.ToBase64String(voiceBytes);

                        // Determine MIME type from extension
                        var mimeType = GetMimeTypeFromExtension(voiceAttachment.Url, "audio/webm");

                        attachmentsList.Add(new AttachmentContent
                        {
                            Base64Data = base64Voice,
                            MimeType = mimeType,
                            Type = "audio"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing voice attachment {Url}", voiceAttachment.Url);
                    }
                }

                var voiceCount = voiceAttachments.Count;
                attachmentPrompt += $"Please transcribe and respond to the {voiceCount} voice note{(voiceCount > 1 ? "s" : "")} from the user. ";
            }

            // Process document attachments
            if (documentAttachments.Any())
            {
                foreach (var documentAttachment in documentAttachments)
                {
                    try
                    {
                        // Download document from Azure Blob Storage
                        var documentStream = await _blobService.DownloadFileAsync(documentAttachment.Url, containerName);
                        using var ms = new MemoryStream();
                        await documentStream.CopyToAsync(ms);
                        var documentBytes = ms.ToArray();
                        var base64Document = Convert.ToBase64String(documentBytes);

                        // Determine MIME type from extension
                        var mimeType = GetMimeTypeFromExtension(documentAttachment.Url, "application/pdf");

                        attachmentsList.Add(new AttachmentContent
                        {
                            Base64Data = base64Document,
                            MimeType = mimeType,
                            Type = "document"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing document attachment {Url}", documentAttachment.Url);
                    }
                }

                var documentCount = documentAttachments.Count;
                attachmentPrompt += $"Please analyze the {documentCount} document{(documentCount > 1 ? "s" : "")} the user has provided. ";
            }

            // Build combined prompt with attachment context
            var combinedPrompt = userMessage ?? "";
            if (!string.IsNullOrEmpty(attachmentPrompt))
            {
                combinedPrompt = $"{attachmentPrompt}\n\nUser message: {userMessage ?? "No text message"}";
            }

            return (combinedPrompt, attachmentsList);
        }

        public async Task<List<AttachmentContent>> DownloadAndConvertAttachmentsAsync(List<AttachmentDto> attachments, string containerName)
        {
            var attachmentsList = new List<AttachmentContent>();

            foreach (var attachment in attachments)
            {
                try
                {
                    var stream = await _blobService.DownloadFileAsync(attachment.Url, containerName);
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    var base64Data = Convert.ToBase64String(bytes);

                    var mimeType = GetMimeTypeFromExtension(attachment.Url);

                    attachmentsList.Add(new AttachmentContent
                    {
                        Base64Data = base64Data,
                        MimeType = mimeType,
                        Type = attachment.Type
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error downloading and converting attachment {Url}", attachment.Url);
                }
            }

            return attachmentsList;
        }

        public string GetMimeTypeFromExtension(string fileName, string defaultMimeType = "application/octet-stream")
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            return extension switch
            {
                // Images
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",

                // Audio
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".webm" => "audio/webm",
                ".m4a" => "audio/mp4",
                ".ogg" => "audio/ogg",

                // Documents
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                ".rtf" => "application/rtf",

                _ => defaultMimeType
            };
        }

        public List<MediaAttachmentResponse>? ConvertToMediaAttachmentResponses(List<AttachmentDto>? attachments)
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
    }
}