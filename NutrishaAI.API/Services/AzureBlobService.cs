using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace NutrishaAI.API.Services
{
    public interface IAzureBlobService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string containerName, string? contentType = null);
        Task<Stream> DownloadFileAsync(string blobName, string containerName);
        Task<string> GetBlobUrlAsync(string blobName, string containerName);
        Task<bool> DeleteFileAsync(string blobName, string containerName);
        Task<bool> BlobExistsAsync(string blobName, string containerName);
        Task<BlobInfo> GetBlobInfoAsync(string blobName, string containerName);
    }

    public class AzureBlobService : IAzureBlobService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureBlobService> _logger;
        private readonly Dictionary<string, long> _maxFileSizes;

        public AzureBlobService(
            IConfiguration configuration,
            ILogger<AzureBlobService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            var connectionString = configuration.GetConnectionString("AzureStorage") 
                ?? configuration["AzureStorage:ConnectionString"];
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Azure Storage connection string not configured");
            }

            _blobServiceClient = new BlobServiceClient(connectionString);

            // Load max file sizes from configuration
            _maxFileSizes = new Dictionary<string, long>
            {
                { "Image", configuration.GetValue<long>("AzureStorage:MaxFileSizes:Image", 10 * 1024 * 1024) }, // 10MB
                { "Voice", configuration.GetValue<long>("AzureStorage:MaxFileSizes:Voice", 5 * 1024 * 1024) },   // 5MB
                { "Document", configuration.GetValue<long>("AzureStorage:MaxFileSizes:Document", 20 * 1024 * 1024) }, // 20MB
                { "Video", configuration.GetValue<long>("AzureStorage:MaxFileSizes:Video", 50 * 1024 * 1024) }  // 50MB
            };
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string containerName, string? contentType = null)
        {
            try
            {
                // Validate file size
                var fileType = GetFileTypeFromExtension(Path.GetExtension(fileName));
                if (_maxFileSizes.ContainsKey(fileType) && fileStream.Length > _maxFileSizes[fileType])
                {
                    throw new InvalidOperationException($"File size exceeds maximum allowed size for {fileType} files");
                }

                // Get or create container
                var containerClient = await GetOrCreateContainerAsync(containerName);

                // Generate unique blob name
                var blobName = GenerateUniqueBlobName(fileName);

                // Get blob client
                var blobClient = containerClient.GetBlobClient(blobName);

                // Set blob upload options
                var uploadOptions = new BlobUploadOptions();
                if (!string.IsNullOrEmpty(contentType))
                {
                    uploadOptions.HttpHeaders = new BlobHttpHeaders { ContentType = contentType };
                }

                // Add metadata - sanitize the filename for metadata as well
                uploadOptions.Metadata = new Dictionary<string, string>
                {
                    { "OriginalFileName", SanitizeFileName(fileName) },
                    { "UploadedAt", DateTime.UtcNow.ToString("O") },
                    { "FileType", fileType }
                };

                // Upload the file
                await blobClient.UploadAsync(fileStream, uploadOptions);

                _logger.LogInformation("File uploaded successfully: {BlobName} to container {ContainerName}", blobName, containerName);

                return blobName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {FileName} to container {ContainerName}", fileName, containerName);
                throw;
            }
        }

        public async Task<Stream> DownloadFileAsync(string blobName, string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"Blob {blobName} not found in container {containerName}");
                }

                var response = await blobClient.DownloadAsync();
                return response.Value.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading blob {BlobName} from container {ContainerName}", blobName, containerName);
                throw;
            }
        }

        public async Task<string> GetBlobUrlAsync(string blobName, string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"Blob {blobName} not found in container {containerName}");
                }

                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting URL for blob {BlobName} from container {ContainerName}", blobName, containerName);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string blobName, string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.DeleteIfExistsAsync();
                
                if (response.Value)
                {
                    _logger.LogInformation("Blob deleted successfully: {BlobName} from container {ContainerName}", blobName, containerName);
                }

                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob {BlobName} from container {ContainerName}", blobName, containerName);
                throw;
            }
        }

        public async Task<bool> BlobExistsAsync(string blobName, string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.ExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if blob exists {BlobName} in container {ContainerName}", blobName, containerName);
                throw;
            }
        }

        public async Task<BlobInfo> GetBlobInfoAsync(string blobName, string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var properties = await blobClient.GetPropertiesAsync();
                var metadata = properties.Value.Metadata;

                return new BlobInfo
                {
                    Name = blobName,
                    ContainerName = containerName,
                    Size = properties.Value.ContentLength,
                    ContentType = properties.Value.ContentType,
                    CreatedOn = properties.Value.CreatedOn,
                    LastModified = properties.Value.LastModified,
                    OriginalFileName = metadata.TryGetValue("OriginalFileName", out var originalName) ? originalName : blobName,
                    FileType = metadata.TryGetValue("FileType", out var fileType) ? fileType : "Unknown",
                    Url = blobClient.Uri.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blob info for {BlobName} from container {ContainerName}", blobName, containerName);
                throw;
            }
        }

        private async Task<BlobContainerClient> GetOrCreateContainerAsync(string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            return containerClient;
        }

        private string GenerateUniqueBlobName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            
            // Sanitize the filename - remove or replace non-ASCII characters
            nameWithoutExtension = SanitizeFileName(nameWithoutExtension);
            
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var guid = Guid.NewGuid().ToString("N")[..8];
            
            return $"{nameWithoutExtension}_{timestamp}_{guid}{extension}";
        }
        
        private string SanitizeFileName(string fileName)
        {
            // Replace non-ASCII characters with underscores
            var sanitized = System.Text.RegularExpressions.Regex.Replace(fileName, @"[^\x20-\x7E]", "_");
            
            // Replace other problematic characters
            sanitized = sanitized.Replace(" ", "_")
                                 .Replace(":", "-")
                                 .Replace("/", "-")
                                 .Replace("\\", "-")
                                 .Replace("?", "_")
                                 .Replace("*", "_")
                                 .Replace("<", "_")
                                 .Replace(">", "_")
                                 .Replace("|", "_")
                                 .Replace("\"", "_");
            
            // Remove multiple consecutive underscores
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"_{2,}", "_");
            
            // Trim underscores from start and end
            sanitized = sanitized.Trim('_', '-');
            
            // If the result is empty, use a default name
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "file";
            }
            
            return sanitized;
        }

        private string GetFileTypeFromExtension(string extension)
        {
            return extension?.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "Image",
                ".mp3" or ".wav" or ".m4a" or ".webm" or ".ogg" => "Voice",
                ".mp4" or ".avi" or ".mov" or ".mkv" or ".wmv" => "Video",
                ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" => "Document",
                _ => "Unknown"
            };
        }
    }

    public class BlobInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTimeOffset CreatedOn { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}