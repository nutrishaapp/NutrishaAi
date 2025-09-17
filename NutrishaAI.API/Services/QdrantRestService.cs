using System.Text.Json;
using System.Text;
using NutrishaAI.API.Models;

namespace NutrishaAI.API.Services
{
    public interface IQdrantRestService
    {
        Task InitializeCollectionAsync();
        Task<bool> StoreMemoryAsync(MemoryVector memory);
        Task<List<MemorySearchResult>> SearchMemoriesAsync(float[] queryEmbedding, Guid userId, int limit = 200);
        Task<bool> DeleteMemoryAsync(Guid memoryId);
        Task<bool> DeleteUserMemoriesAsync(Guid userId);
    }

    public class QdrantRestService : IQdrantRestService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<QdrantRestService> _logger;
        private readonly string _collectionName;
        private readonly string _baseUrl;
        private readonly int _vectorSize = 768;

        public QdrantRestService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<QdrantRestService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            var host = _configuration["Qdrant:Host"] ?? "localhost";
            var port = _configuration.GetValue<int>("Qdrant:Port", 6333);
            var apiKey = _configuration["Qdrant:ApiKey"];
            _collectionName = _configuration["Qdrant:CollectionName"] ?? "user_memories";
            
            // Build base URL for REST API
            _baseUrl = $"https://{host}:{port}";
            
            // Set up HTTP client headers
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            }
            
            // Initialize collection on startup
            _ = Task.Run(async () => await InitializeCollectionAsync());
        }

        public async Task InitializeCollectionAsync()
        {
            try
            {
                // Check if collection exists
                var response = await _httpClient.GetAsync($"{_baseUrl}/collections/{_collectionName}");
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Collection {CollectionName} already exists", _collectionName);
                    return;
                }

                // Create collection
                var createRequest = new
                {
                    vectors = new
                    {
                        size = _vectorSize,
                        distance = "Cosine"
                    }
                };

                var jsonContent = JsonSerializer.Serialize(createRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var createResponse = await _httpClient.PutAsync($"{_baseUrl}/collections/{_collectionName}", content);
                
                if (createResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully created collection {CollectionName}", _collectionName);
                    
                    // Create indexes for filtering
                    await CreateIndexAsync("user_id", "keyword");
                    await CreateIndexAsync("conversation_id", "keyword");
                    await CreateIndexAsync("created_at", "integer");
                }
                else
                {
                    var errorContent = await createResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create collection. Status: {Status}, Content: {Content}", 
                        createResponse.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Qdrant collection");
            }
        }

        private async Task CreateIndexAsync(string fieldName, string fieldType)
        {
            try
            {
                var indexRequest = new
                {
                    field_name = fieldName,
                    field_schema = fieldType
                };

                var jsonContent = JsonSerializer.Serialize(indexRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                await _httpClient.PutAsync($"{_baseUrl}/collections/{_collectionName}/index", content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create index for field {FieldName}", fieldName);
            }
        }

        public async Task<bool> StoreMemoryAsync(MemoryVector memory)
        {
            try
            {
                if (memory.Embedding == null || memory.Embedding.Length != _vectorSize)
                {
                    _logger.LogWarning("Invalid embedding dimensions for memory {MemoryId}", memory.Id);
                    return false;
                }

                var point = new
                {
                    id = memory.Id.ToString(),
                    vector = memory.Embedding,
                    payload = new Dictionary<string, object>
                    {
                        ["user_id"] = memory.UserId.ToString(),
                        ["conversation_id"] = memory.ConversationId.ToString(),
                        ["summary"] = memory.Summary,
                        ["message_content"] = memory.MessageContent,
                        ["topics"] = string.Join(",", memory.Topics),
                        ["created_at"] = new DateTimeOffset(memory.CreatedAt).ToUnixTimeSeconds()
                    }
                };

                // Add metadata fields
                foreach (var kvp in memory.Metadata)
                {
                    point.payload[$"metadata_{kvp.Key}"] = kvp.Value?.ToString() ?? "";
                }

                var upsertRequest = new { points = new[] { point } };
                var jsonContent = JsonSerializer.Serialize(upsertRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"{_baseUrl}/collections/{_collectionName}/points", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully stored memory {MemoryId} for user {UserId}", 
                        memory.Id, memory.UserId);
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to store memory {MemoryId}. Status: {Status}, Content: {Content}", 
                    memory.Id, response.StatusCode, errorContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing memory {MemoryId} in Qdrant", memory.Id);
                return false;
            }
        }

        public async Task<List<MemorySearchResult>> SearchMemoriesAsync(float[] queryEmbedding, Guid userId, int limit = 200)
        {
            try
            {
                if (queryEmbedding == null || queryEmbedding.Length != _vectorSize)
                {
                    _logger.LogWarning("Invalid query embedding dimensions");
                    return new List<MemorySearchResult>();
                }

                var searchRequest = new
                {
                    vector = queryEmbedding,
                    filter = new
                    {
                        must = new[]
                        {
                            new
                            {
                                key = "user_id",
                                match = new { value = userId.ToString() }
                            }
                        }
                    },
                    limit = limit,
                    with_payload = true
                };

                var jsonContent = JsonSerializer.Serialize(searchRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/collections/{_collectionName}/points/search", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var searchResponse = JsonDocument.Parse(responseContent);
                    
                    var results = new List<MemorySearchResult>();
                    
                    if (searchResponse.RootElement.TryGetProperty("result", out var resultArray))
                    {
                        foreach (var item in resultArray.EnumerateArray())
                        {
                            if (item.TryGetProperty("payload", out var payload) &&
                                item.TryGetProperty("score", out var scoreElement) &&
                                item.TryGetProperty("id", out var idElement))
                            {
                                var memory = new MemoryVector
                                {
                                    Id = Guid.Parse(idElement.GetString()!),
                                    UserId = Guid.Parse(payload.GetProperty("user_id").GetString()!),
                                    ConversationId = Guid.Parse(payload.GetProperty("conversation_id").GetString()!),
                                    Summary = payload.GetProperty("summary").GetString()!,
                                    MessageContent = payload.GetProperty("message_content").GetString()!,
                                    Topics = payload.TryGetProperty("topics", out var topicsElement) && 
                                            !string.IsNullOrEmpty(topicsElement.GetString())
                                        ? topicsElement.GetString()!.Split(',').ToList()
                                        : new List<string>(),
                                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds(
                                        payload.GetProperty("created_at").GetInt64()).UtcDateTime
                                };

                                results.Add(new MemorySearchResult
                                {
                                    Memory = memory,
                                    Score = (float)scoreElement.GetDouble()
                                });
                            }
                        }
                    }

                    _logger.LogDebug("Found {Count} memories for user {UserId}", results.Count, userId);
                    return results;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to search memories. Status: {Status}, Content: {Content}", 
                    response.StatusCode, errorContent);
                return new List<MemorySearchResult>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching memories for user {UserId}", userId);
                return new List<MemorySearchResult>();
            }
        }


        public async Task<bool> DeleteMemoryAsync(Guid memoryId)
        {
            try
            {
                var deleteRequest = new
                {
                    points = new[] { memoryId.ToString() }
                };

                var jsonContent = JsonSerializer.Serialize(deleteRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/collections/{_collectionName}/points/delete", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting memory {MemoryId}", memoryId);
                return false;
            }
        }

        public async Task<bool> DeleteUserMemoriesAsync(Guid userId)
        {
            try
            {
                var deleteRequest = new
                {
                    filter = new
                    {
                        must = new[]
                        {
                            new
                            {
                                key = "user_id",
                                match = new { value = userId.ToString() }
                            }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(deleteRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/collections/{_collectionName}/points/delete", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Deleted memories for user {UserId}", userId);
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting memories for user {UserId}", userId);
                return false;
            }
        }
    }
}