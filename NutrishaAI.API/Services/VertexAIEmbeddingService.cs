using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;
using Value = Google.Protobuf.WellKnownTypes.Value;

namespace NutrishaAI.API.Services
{
    public interface IVertexAIEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts);
    }

    public class VertexAIEmbeddingService : IVertexAIEmbeddingService
    {
        private readonly PredictionServiceClient _predictionClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VertexAIEmbeddingService> _logger;
        private readonly string _projectId;
        private readonly string _location;
        private readonly string _embeddingModel;
        private readonly string _endpointId;

        public VertexAIEmbeddingService(
            IConfiguration configuration,
            ILogger<VertexAIEmbeddingService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            _projectId = _configuration["VertexAI:ProjectId"] 
                ?? throw new InvalidOperationException("VertexAI:ProjectId not configured");
            _location = _configuration["VertexAI:Location"] ?? "us-central1";
            _embeddingModel = _configuration["VertexAI:EmbeddingModel"] ?? "text-embedding-004";
            
            // Initialize the Vertex AI client
            var endpointName = $"projects/{_projectId}/locations/{_location}/publishers/google/models/{_embeddingModel}";
            _endpointId = endpointName;
            
            _predictionClient = new PredictionServiceClientBuilder
            {
                Endpoint = $"{_location}-aiplatform.googleapis.com"
            }.Build();
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

                // Prepare the request
                var instance = Value.ForStruct(new Struct
                {
                    Fields =
                    {
                        ["content"] = Value.ForString(text),
                        ["task_type"] = Value.ForString("RETRIEVAL_DOCUMENT")
                    }
                });

                var parameters = Value.ForStruct(new Struct
                {
                    Fields =
                    {
                        ["outputDimensionality"] = Value.ForNumber(768) // Standard dimension for text-embedding-004
                    }
                });

                var request = new PredictRequest
                {
                    Endpoint = _endpointId,
                    Instances = { instance },
                    Parameters = parameters
                };

                // Make the API call
                var response = await _predictionClient.PredictAsync(request);
                
                if (response.Predictions.Count > 0)
                {
                    var prediction = response.Predictions[0];
                    var embeddingStruct = prediction.StructValue.Fields["embeddings"].StructValue;
                    var values = embeddingStruct.Fields["values"].ListValue.Values;
                    
                    var embedding = values.Select(v => (float)v.NumberValue).ToArray();
                    
                    _logger.LogDebug("Generated embedding with {Dimensions} dimensions", embedding.Length);
                    return embedding;
                }

                _logger.LogWarning("No predictions returned from Vertex AI");
                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text");
                throw new InvalidOperationException("Failed to generate embedding", ex);
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

                // Vertex AI supports batch embeddings
                var instances = texts.Select(text => 
                    Value.ForStruct(new Struct
                    {
                        Fields =
                        {
                            ["content"] = Value.ForString(text),
                            ["task_type"] = Value.ForString("RETRIEVAL_DOCUMENT")
                        }
                    })).ToList();

                var parameters = Value.ForStruct(new Struct
                {
                    Fields =
                    {
                        ["outputDimensionality"] = Value.ForNumber(768)
                    }
                });

                var request = new PredictRequest
                {
                    Endpoint = _endpointId,
                    Parameters = parameters
                };
                request.Instances.AddRange(instances);

                var response = await _predictionClient.PredictAsync(request);
                
                var embeddings = new List<float[]>();
                foreach (var prediction in response.Predictions)
                {
                    var embeddingStruct = prediction.StructValue.Fields["embeddings"].StructValue;
                    var values = embeddingStruct.Fields["values"].ListValue.Values;
                    var embedding = values.Select(v => (float)v.NumberValue).ToArray();
                    embeddings.Add(embedding);
                }

                _logger.LogDebug("Generated {Count} embeddings in batch", embeddings.Count);
                return embeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings");
                throw new InvalidOperationException("Failed to generate batch embeddings", ex);
            }
        }
    }
}