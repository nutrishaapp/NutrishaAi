using System.Text.Json;
using System.Text;

namespace NutrishaAI.API.Services
{
    public interface IGeminiEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts);
    }

    public class GeminiEmbeddingService : IGeminiEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiEmbeddingService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public GeminiEmbeddingService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            _apiKey = _configuration["Gemini:ApiKey"] 
                ?? throw new InvalidOperationException("Gemini:ApiKey not configured");
            _baseUrl = _configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Empty text provided for embedding generation");
                    return Array.Empty<float>();
                }

                var request = new
                {
                    model = "models/text-embedding-004",
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = text }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(request);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var url = $"{_baseUrl}/models/text-embedding-004:embedContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(url, httpContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseJson = JsonDocument.Parse(responseContent);
                    
                    if (responseJson.RootElement.TryGetProperty("embedding", out var embeddingElement) &&
                        embeddingElement.TryGetProperty("values", out var valuesElement))
                    {
                        var embedding = valuesElement.EnumerateArray()
                            .Select(x => (float)x.GetDouble())
                            .ToArray();
                        
                        _logger.LogDebug("Generated embedding with {Dimensions} dimensions", embedding.Length);
                        return embedding;
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to generate embedding. Status: {Status}, Content: {Content}", 
                    response.StatusCode, errorContent);
                
                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text");
                return Array.Empty<float>();
            }
        }

        public async Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts)
        {
            try
            {
                if (texts == null || texts.Count == 0)
                {
                    return new List<float[]>();
                }

                // Generate embeddings one by one for now (Gemini API doesn't support batch embeddings directly)
                var embeddings = new List<float[]>();
                foreach (var text in texts)
                {
                    var embedding = await GenerateEmbeddingAsync(text);
                    embeddings.Add(embedding);
                }

                _logger.LogDebug("Generated {Count} embeddings in batch", embeddings.Count);
                return embeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings");
                return new List<float[]>();
            }
        }
    }
}