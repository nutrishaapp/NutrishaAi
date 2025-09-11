using Qdrant.Client;
using Qdrant.Client.Grpc;
using NutrishaAI.API.Models;

namespace NutrishaAI.API.Services
{
    public interface IQdrantVectorService
    {
        Task InitializeCollectionAsync();
        Task<bool> StoreMemoryAsync(MemoryVector memory);
        Task<List<MemorySearchResult>> SearchMemoriesAsync(float[] queryEmbedding, Guid userId, int limit = 5);
        Task<bool> DeleteMemoryAsync(Guid memoryId);
        Task<bool> DeleteUserMemoriesAsync(Guid userId);
    }

    public class QdrantVectorService : IQdrantVectorService
    {
        private readonly QdrantClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<QdrantVectorService> _logger;
        private readonly string _collectionName;
        private readonly int _vectorSize = 768; // Standard size for text-embedding-004

        public QdrantVectorService(
            IConfiguration configuration,
            ILogger<QdrantVectorService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            var host = _configuration["Qdrant:Host"] ?? "localhost";
            var port = _configuration.GetValue<int>("Qdrant:Port", 6333);
            var apiKey = _configuration["Qdrant:ApiKey"];
            _collectionName = _configuration["Qdrant:CollectionName"] ?? "user_memories";
            
            // Initialize Qdrant client - always use HTTPS for cloud instances
            var isCloudInstance = host.Contains("cloud.qdrant.io") || host.Contains("gcp.cloud.qdrant.io");
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                _client = new QdrantClient(host, port, https: true, apiKey: apiKey);
            }
            else
            {
                _client = new QdrantClient(host, port, https: false);
            }
            
            // Initialize collection on startup
            _ = Task.Run(async () => await InitializeCollectionAsync());
        }

        public async Task InitializeCollectionAsync()
        {
            try
            {
                // Check if collection exists
                var collections = await _client.ListCollectionsAsync();
                if (collections.Any(c => c == _collectionName))
                {
                    _logger.LogInformation("Collection {CollectionName} already exists", _collectionName);
                    return;
                }

                // Create collection with vector configuration
                await _client.CreateCollectionAsync(
                    collectionName: _collectionName,
                    vectorsConfig: new VectorParams 
                    { 
                        Size = (ulong)_vectorSize, 
                        Distance = Distance.Cosine 
                    }
                );

                // Create indexes for filtering
                await _client.CreatePayloadIndexAsync(
                    collectionName: _collectionName,
                    fieldName: "user_id",
                    schemaType: PayloadSchemaType.Keyword
                );

                await _client.CreatePayloadIndexAsync(
                    collectionName: _collectionName,
                    fieldName: "conversation_id",
                    schemaType: PayloadSchemaType.Keyword
                );

                await _client.CreatePayloadIndexAsync(
                    collectionName: _collectionName,
                    fieldName: "created_at",
                    schemaType: PayloadSchemaType.Integer
                );

                _logger.LogInformation("Successfully created collection {CollectionName}", _collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Qdrant collection");
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

                // Prepare the point for Qdrant
                var payload = new Dictionary<string, Qdrant.Client.Grpc.Value>
                {
                    ["user_id"] = memory.UserId.ToString(),
                    ["conversation_id"] = memory.ConversationId.ToString(),
                    ["summary"] = memory.Summary,
                    ["message_content"] = memory.MessageContent,
                    ["topics"] = string.Join(",", memory.Topics),
                    ["created_at"] = new DateTimeOffset(memory.CreatedAt).ToUnixTimeSeconds()
                };
                
                // Add metadata fields as separate payload fields
                foreach (var kvp in memory.Metadata)
                {
                    payload[$"metadata_{kvp.Key}"] = kvp.Value?.ToString() ?? "";
                }

                var point = new PointStruct
                {
                    Id = new PointId { Uuid = memory.Id.ToString() },
                    Vectors = memory.Embedding
                };
                
                // Add payload to point
                foreach (var kvp in payload)
                {
                    point.Payload.Add(kvp.Key, kvp.Value);
                }

                // Upsert the point to Qdrant
                var result = await _client.UpsertAsync(
                    collectionName: _collectionName,
                    points: new List<PointStruct> { point }
                );

                if (result.Status == UpdateStatus.Completed)
                {
                    _logger.LogInformation("Successfully stored memory {MemoryId} for user {UserId}", 
                        memory.Id, memory.UserId);
                    return true;
                }

                _logger.LogWarning("Failed to store memory {MemoryId}, status: {Status}", 
                    memory.Id, result.Status);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing memory {MemoryId} in Qdrant", memory.Id);
                return false;
            }
        }

        public async Task<List<MemorySearchResult>> SearchMemoriesAsync(float[] queryEmbedding, Guid userId, int limit = 5)
        {
            try
            {
                if (queryEmbedding == null || queryEmbedding.Length != _vectorSize)
                {
                    _logger.LogWarning("Invalid query embedding dimensions");
                    return new List<MemorySearchResult>();
                }

                // Search with user filter
                var searchResult = await _client.SearchAsync(
                    collectionName: _collectionName,
                    vector: queryEmbedding,
                    filter: new Filter
                    {
                        Must = 
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "user_id",
                                    Match = new Match { Keyword = userId.ToString() }
                                }
                            }
                        }
                    },
                    limit: (ulong)limit
                );

                var results = new List<MemorySearchResult>();
                foreach (var point in searchResult)
                {
                    var memory = new MemoryVector
                    {
                        Id = Guid.Parse(point.Id.Uuid),
                        UserId = Guid.Parse(point.Payload["user_id"].StringValue),
                        ConversationId = Guid.Parse(point.Payload["conversation_id"].StringValue),
                        Summary = point.Payload["summary"].StringValue,
                        MessageContent = point.Payload["message_content"].StringValue,
                        Topics = point.Payload.ContainsKey("topics") && !string.IsNullOrEmpty(point.Payload["topics"].StringValue)
                            ? point.Payload["topics"].StringValue.Split(',').ToList()
                            : new List<string>(),
                        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(
                            point.Payload["created_at"].IntegerValue).UtcDateTime
                    };

                    results.Add(new MemorySearchResult
                    {
                        Memory = memory,
                        Score = point.Score
                    });
                }

                _logger.LogDebug("Found {Count} memories for user {UserId}", results.Count, userId);
                return results;
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
                var result = await _client.DeleteAsync(
                    collectionName: _collectionName,
                    ids: new List<PointId> { new PointId { Uuid = memoryId.ToString() } }
                );

                return result.Status == UpdateStatus.Completed;
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
                var result = await _client.DeleteAsync(
                    collectionName: _collectionName,
                    filter: new Filter
                    {
                        Must = 
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "user_id",
                                    Match = new Match { Keyword = userId.ToString() }
                                }
                            }
                        }
                    }
                );

                _logger.LogInformation("Deleted memories for user {UserId}", userId);
                return result.Status == UpdateStatus.Completed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting memories for user {UserId}", userId);
                return false;
            }
        }
    }
}