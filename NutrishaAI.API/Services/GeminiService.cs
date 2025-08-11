using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;

namespace NutrishaAI.API.Services
{
    public interface IGeminiService
    {
        Task<string> ProcessTextAsync(string text, string? context = null);
        Task<GeminiResponse> ProcessMultimediaAsync(Stream fileStream, string contentType, string? textPrompt = null);
        Task<GeminiResponse> ProcessImageAsync(Stream imageStream, string? textPrompt = null);
        Task<GeminiResponse> ProcessVoiceNoteAsync(Stream audioStream, string? textPrompt = null);
        Task<HealthDataExtractionResult> ExtractHealthDataAsync(string content, string contentType);
    }

    public class GeminiService : IGeminiService
    {
        private readonly PredictionServiceClient _predictionClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _projectId;
        private readonly string _location;
        private readonly string _modelName;

        public GeminiService(
            IConfiguration configuration,
            ILogger<GeminiService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            _projectId = configuration["GoogleCloud:ProjectId"] ?? throw new InvalidOperationException("GoogleCloud:ProjectId not configured");
            _location = configuration["GoogleCloud:Location"] ?? "us-central1";
            _modelName = configuration["GoogleCloud:ModelName"] ?? "gemini-1.5-pro";

            // Initialize the client
            var clientBuilder = new PredictionServiceClientBuilder
            {
                Endpoint = $"{_location}-aiplatform.googleapis.com"
            };

            // Set credentials if provided
            var credentialsPath = configuration["GoogleCloud:CredentialsPath"];
            if (!string.IsNullOrEmpty(credentialsPath))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
            }

            _predictionClient = clientBuilder.Build();
        }

        public async Task<string> ProcessTextAsync(string text, string? context = null)
        {
            try
            {
                var prompt = context != null 
                    ? $"Context: {context}\n\nUser Message: {text}\n\nAs an AI nutritionist, provide helpful advice:"
                    : $"As an AI nutritionist, analyze and respond to: {text}";

                var request = CreateTextRequest(prompt);
                var response = await _predictionClient.PredictAsync(request);

                return ExtractTextFromResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text with Gemini");
                throw;
            }
        }

        public async Task<GeminiResponse> ProcessMultimediaAsync(Stream fileStream, string contentType, string? textPrompt = null)
        {
            try
            {
                // Determine processing method based on content type
                if (contentType.StartsWith("image/"))
                {
                    return await ProcessImageAsync(fileStream, textPrompt);
                }
                else if (contentType.StartsWith("audio/"))
                {
                    return await ProcessVoiceNoteAsync(fileStream, textPrompt);
                }
                else
                {
                    // For documents, convert to text first (simplified approach)
                    using var reader = new StreamReader(fileStream);
                    var content = await reader.ReadToEndAsync();
                    var textResponse = await ProcessTextAsync(content, textPrompt);
                    
                    return new GeminiResponse
                    {
                        Text = textResponse,
                        ContentType = "text",
                        ProcessedAt = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multimedia with Gemini");
                throw;
            }
        }

        public async Task<GeminiResponse> ProcessImageAsync(Stream imageStream, string? textPrompt = null)
        {
            try
            {
                var prompt = textPrompt ?? 
                    "As an AI nutritionist, analyze this image. If it contains food, provide nutritional information, calorie estimates, and health advice. If it's a document or chart, extract and explain the relevant health/nutrition information.";

                // Convert image to base64
                var imageBytes = new byte[imageStream.Length];
                await imageStream.ReadAsync(imageBytes, 0, (int)imageStream.Length);
                var base64Image = Convert.ToBase64String(imageBytes);

                var request = CreateMultimodalRequest(prompt, base64Image, "image");
                var response = await _predictionClient.PredictAsync(request);

                var textResult = ExtractTextFromResponse(response);

                return new GeminiResponse
                {
                    Text = textResult,
                    ContentType = "image_analysis",
                    ProcessedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        { "original_prompt", prompt },
                        { "image_size_bytes", imageBytes.Length }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image with Gemini");
                throw;
            }
        }

        public async Task<GeminiResponse> ProcessVoiceNoteAsync(Stream audioStream, string? textPrompt = null)
        {
            try
            {
                var prompt = textPrompt ?? 
                    "As an AI nutritionist, listen to this audio and provide relevant nutritional advice, meal planning suggestions, or health guidance based on what you hear. Include any food mentions, dietary concerns, or health goals discussed.";

                // Convert audio to base64
                var audioBytes = new byte[audioStream.Length];
                await audioStream.ReadAsync(audioBytes, 0, (int)audioStream.Length);
                var base64Audio = Convert.ToBase64String(audioBytes);

                var request = CreateMultimodalRequest(prompt, base64Audio, "audio");
                var response = await _predictionClient.PredictAsync(request);

                var textResult = ExtractTextFromResponse(response);

                return new GeminiResponse
                {
                    Text = textResult,
                    ContentType = "audio_analysis",
                    ProcessedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        { "original_prompt", prompt },
                        { "audio_size_bytes", audioBytes.Length }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing voice note with Gemini");
                throw;
            }
        }

        public async Task<HealthDataExtractionResult> ExtractHealthDataAsync(string content, string contentType)
        {
            try
            {
                var prompt = $@"
Analyze the following {contentType} content and extract structured health data in JSON format.
Extract information about:
- Foods mentioned (with estimated calories if possible)
- Exercise activities (with duration and intensity)
- Health symptoms or concerns
- Dietary restrictions or preferences
- Goals (weight loss, muscle gain, etc.)
- Measurements (weight, blood pressure, etc.)

Content: {content}

Respond with a JSON object containing the extracted data with these fields:
- foods: array of {{name, calories, serving_size}}
- exercises: array of {{activity, duration_minutes, intensity}}
- symptoms: array of strings
- dietary_restrictions: array of strings
- health_goals: array of strings
- measurements: object with key-value pairs
- summary: brief text summary

If no relevant data is found, return empty arrays/objects.";

                var textResponse = await ProcessTextAsync(prompt);
                
                // Try to parse JSON response
                HealthDataExtractionResult result;
                try
                {
                    result = JsonSerializer.Deserialize<HealthDataExtractionResult>(textResponse) ?? new HealthDataExtractionResult();
                }
                catch (JsonException)
                {
                    // If JSON parsing fails, create a basic result with the summary
                    result = new HealthDataExtractionResult
                    {
                        Summary = textResponse,
                        ExtractedAt = DateTime.UtcNow
                    };
                }

                result.ExtractedAt = DateTime.UtcNow;
                result.OriginalContent = content;
                result.ContentType = contentType;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting health data with Gemini");
                throw;
            }
        }

        private PredictRequest CreateTextRequest(string prompt)
        {
            var instanceDict = new Dictionary<string, object>
            {
                { "content", prompt }
            };

            var structValue = Struct.Parser.ParseJson(JsonSerializer.Serialize(instanceDict));
            var instance = Google.Protobuf.WellKnownTypes.Value.ForStruct(structValue);

            return new PredictRequest
            {
                Endpoint = $"projects/{_projectId}/locations/{_location}/publishers/google/models/{_modelName}",
                Instances = { instance }
            };
        }

        private PredictRequest CreateMultimodalRequest(string textPrompt, string base64Data, string dataType)
        {
            var instanceDict = new Dictionary<string, object>
            {
                { "text", textPrompt },
                { dataType, new { data = base64Data } }
            };

            var structValue = Struct.Parser.ParseJson(JsonSerializer.Serialize(instanceDict));
            var instance = Google.Protobuf.WellKnownTypes.Value.ForStruct(structValue);

            return new PredictRequest
            {
                Endpoint = $"projects/{_projectId}/locations/{_location}/publishers/google/models/{_modelName}",
                Instances = { instance }
            };
        }

        private string ExtractTextFromResponse(PredictResponse response)
        {
            try
            {
                // Extract text from Gemini response
                var prediction = response.Predictions.FirstOrDefault();
                if (prediction?.StructValue != null)
                {
                    if (prediction.StructValue.Fields.TryGetValue("content", out var contentValue))
                    {
                        return contentValue.StringValue;
                    }
                    
                    if (prediction.StructValue.Fields.TryGetValue("text", out var textValue))
                    {
                        return textValue.StringValue;
                    }
                    
                    // Fallback: return JSON representation
                    return prediction.ToString();
                }

                return "No response generated";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting text from Gemini response");
                return "Error processing response";
            }
        }
    }

    public class GeminiResponse
    {
        public string Text { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class HealthDataExtractionResult
    {
        public List<FoodItem> Foods { get; set; } = new();
        public List<ExerciseActivity> Exercises { get; set; } = new();
        public List<string> Symptoms { get; set; } = new();
        public List<string> DietaryRestrictions { get; set; } = new();
        public List<string> HealthGoals { get; set; } = new();
        public Dictionary<string, object> Measurements { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public DateTime ExtractedAt { get; set; }
        public string OriginalContent { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    public class FoodItem
    {
        public string Name { get; set; } = string.Empty;
        public int? Calories { get; set; }
        public string? ServingSize { get; set; }
    }

    public class ExerciseActivity
    {
        public string Activity { get; set; } = string.Empty;
        public int? DurationMinutes { get; set; }
        public string? Intensity { get; set; }
    }
}