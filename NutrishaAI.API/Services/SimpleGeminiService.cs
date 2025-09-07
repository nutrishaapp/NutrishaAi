using System.Text;
using System.Text.Json;

namespace NutrishaAI.API.Services
{
    public class GeminiJsonResponse
    {
        public string Reply { get; set; } = string.Empty;
        public int ContentCategory { get; set; }
    }

    public interface ISimpleGeminiService
    {
        Task<string> GenerateResponseAsync(string prompt);
        Task<string> GenerateNutritionistResponseAsync(string userMessage, string? conversationContext = null);
    }

    public class SimpleGeminiService : ISimpleGeminiService, IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IAppConfigService _configService;
        private readonly ILogger<SimpleGeminiService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public SimpleGeminiService(
            HttpClient httpClient,
            IConfiguration configuration,
            IAppConfigService configService,
            ILogger<SimpleGeminiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _configService = configService;
            _logger = logger;
            
            _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
            _baseUrl = configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
            _model = configuration["Gemini:Model"] ?? "gemini-1.5-flash";
        }

        public async Task<string> GenerateResponseAsync(string prompt)
        {
            try
            {
                var requestUrl = $"{_baseUrl}/models/{_model}:generateContent?key={_apiKey}";
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 8192,
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(requestUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    return "I apologize, but I'm having trouble generating a response right now. Please try again.";
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                // Extract the generated text
                if (responseObj.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var contentElement) &&
                        contentElement.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var firstPart = parts[0];
                        if (firstPart.TryGetProperty("text", out var textElement))
                        {
                            return textElement.GetString() ?? "I couldn't generate a response.";
                        }
                    }
                }

                return "I couldn't generate a response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response with Gemini API");
                return "I apologize, but I'm experiencing technical difficulties. Please try again later.";
            }
        }

        private string CleanJsonResponse(string rawResponse)
        {
            // Remove common markdown code block markers
            rawResponse = System.Text.RegularExpressions.Regex.Replace(
                rawResponse, 
                @"```json\s*|\s*```", 
                "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove common prefixes
            var prefixes = new[] { "json:", "JSON:", "Here's my response:", "Response:", "Here is the JSON:" };
            foreach (var prefix in prefixes)
            {
                var index = rawResponse.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && index < 50) // Only remove if prefix is near the beginning
                {
                    rawResponse = rawResponse.Substring(index + prefix.Length);
                    break;
                }
            }
            
            return rawResponse.Trim();
        }

        private string ExtractJsonFromResponse(string response)
        {
            // Try to find JSON object boundaries with smart brace matching
            var startIndex = response.IndexOf('{');
            
            if (startIndex < 0)
                return string.Empty;
            
            var braceCount = 0;
            var inString = false;
            var escapeNext = false;
            var endIndex = -1;
            
            for (int i = startIndex; i < response.Length; i++)
            {
                var ch = response[i];
                
                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }
                
                if (ch == '\\' && inString)
                {
                    escapeNext = true;
                    continue;
                }
                
                if (ch == '"' && !escapeNext)
                {
                    inString = !inString;
                }
                
                if (!inString)
                {
                    if (ch == '{')
                        braceCount++;
                    else if (ch == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            endIndex = i;
                            break;
                        }
                    }
                }
            }
            
            if (endIndex > startIndex)
            {
                return response.Substring(startIndex, endIndex - startIndex + 1);
            }
            
            return string.Empty;
        }

        private GeminiJsonResponse ParseGeminiJsonResponse(string rawResponse)
        {
            try
            {
                // First, clean the response
                var cleanedResponse = CleanJsonResponse(rawResponse);
                
                // Try to extract JSON from the cleaned response
                var jsonText = ExtractJsonFromResponse(cleanedResponse);
                
                if (!string.IsNullOrEmpty(jsonText))
                {
                    try
                    {
                        // Try parsing with flexible options
                        var response = JsonSerializer.Deserialize<GeminiJsonResponse>(jsonText, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            ReadCommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        });
                        
                        if (response != null)
                        {
                            // Validate the response
                            if (string.IsNullOrWhiteSpace(response.Reply))
                            {
                                response.Reply = cleanedResponse;
                            }
                            
                            // Ensure content category is in valid range (0-2)
                            if (response.ContentCategory < 0 || response.ContentCategory > 2)
                            {
                                response.ContentCategory = 0; // Default to general conversation
                            }
                            
                            return response;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "JSON deserialization failed for extracted text: {JsonText}", jsonText);
                    }
                }
                
                // Fallback: Try to extract just the reply field using regex
                var replyMatch = System.Text.RegularExpressions.Regex.Match(
                    cleanedResponse,
                    @"""reply""\s*:\s*""([^""\\]*(?:\\.[^""\\]*)*)""",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                var categoryMatch = System.Text.RegularExpressions.Regex.Match(
                    cleanedResponse,
                    @"""contentCategory""\s*:\s*(\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (replyMatch.Success && replyMatch.Groups.Count > 1)
                {
                    var reply = System.Text.RegularExpressions.Regex.Unescape(replyMatch.Groups[1].Value);
                    var category = 0;
                    
                    if (categoryMatch.Success && categoryMatch.Groups.Count > 1)
                    {
                        if (int.TryParse(categoryMatch.Groups[1].Value, out var parsedCategory))
                        {
                            category = Math.Max(0, Math.Min(2, parsedCategory)); // Clamp to 0-2
                        }
                    }
                    
                    return new GeminiJsonResponse
                    {
                        Reply = reply,
                        ContentCategory = category
                    };
                }
                
                // Final fallback: use the cleaned response as-is
                _logger.LogWarning("All JSON parsing attempts failed, using raw response: {Response}", cleanedResponse.Substring(0, Math.Min(100, cleanedResponse.Length)));
                
                return new GeminiJsonResponse
                {
                    Reply = cleanedResponse,
                    ContentCategory = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error parsing Gemini response");
                
                // Absolute fallback
                return new GeminiJsonResponse
                {
                    Reply = rawResponse,
                    ContentCategory = 0
                };
            }
        }

        public async Task<string> GenerateNutritionistResponseAsync(string userMessage, string? conversationContext = null)
        {
            // Get both system prompt and JSON structure from config service
            var systemPromptTemplate = await _configService.GetConfigAsync("risha_prompt");
            var jsonStructure = await _configService.GetConfigAsync("response_json_structure");

            // Check if configs are available
            if (string.IsNullOrEmpty(systemPromptTemplate) || string.IsNullOrEmpty(jsonStructure))
            {
                _logger.LogWarning("Missing configuration: risha_prompt or response_json_structure not found");
                return "I apologize, but I'm experiencing configuration issues. Please try again later.";
            }

            // Replace template variables
            var systemPrompt = systemPromptTemplate.Replace("{conversationContext}", 
                conversationContext ?? "This is the start of a new conversation.");

            var fullPrompt = $@"{systemPrompt}

User Message: {userMessage}

{jsonStructure}";

            // Get raw JSON response from Gemini
            var rawResponse = await GenerateResponseAsync(fullPrompt);
            
            // Parse JSON and extract reply + contentCategory
            var parsedResponse = ParseGeminiJsonResponse(rawResponse);
            
            // Return formatted message: "reply text (Content:0)"
            return $"{parsedResponse.Reply} (Content:{parsedResponse.ContentCategory})";
        }

        public async Task<string> ProcessTextAsync(string text, string? context = null)
        {
            var prompt = context != null 
                ? $"Context: {context}\n\nUser Message: {text}\n\nAs an AI nutritionist, provide helpful advice:"
                : $"As an AI nutritionist, analyze and respond to: {text}";

            return await GenerateResponseAsync(prompt);
        }

        public async Task<GeminiResponse> ProcessMultimediaAsync(Stream fileStream, string contentType, string? textPrompt = null)
        {
            try
            {
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
                return new GeminiResponse
                {
                    Text = "I apologize, but I couldn't process the multimedia content.",
                    ContentType = contentType,
                    ProcessedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<GeminiResponse> ProcessImageAsync(Stream imageStream, string? textPrompt = null)
        {
            try
            {
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms);
                var imageBytes = ms.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);

                var promptTemplate = await _configService.GetConfigAsync("image_analysis_prompt", @"You are a professional nutritionist analyzing food images.
                
Analyze this image and provide:
1. Food identification and portion estimation
2. Nutritional analysis (calories, macros, etc.)
3. Health recommendations
4. Any dietary concerns or benefits
                
{textPrompt}
                
[Image data provided as base64]");

                var prompt = promptTemplate.Replace("{textPrompt}", textPrompt ?? "Please analyze this image.");

                var response = await GenerateResponseAsync(prompt);

                return new GeminiResponse
                {
                    Text = response,
                    ContentType = "image",
                    ProcessedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        { "imageSize", imageBytes.Length }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image with Gemini");
                return new GeminiResponse
                {
                    Text = "I apologize, but I couldn't analyze the image. Please try again.",
                    ContentType = "image",
                    ProcessedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<GeminiResponse> ProcessVoiceNoteAsync(Stream audioStream, string? textPrompt = null)
        {
            try
            {
                var promptTemplate = await _configService.GetConfigAsync("voice_processing_prompt", @"You are a professional nutritionist processing audio content.
                
{textPrompt}
                
Note: Audio transcription is not yet implemented. Please provide nutrition advice based on this audio.");

                var prompt = promptTemplate.Replace("{textPrompt}", textPrompt ?? "Please provide nutrition advice based on this audio.");

                var response = await GenerateResponseAsync(prompt);

                return new GeminiResponse
                {
                    Text = response,
                    ContentType = "audio",
                    ProcessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing voice note with Gemini");
                return new GeminiResponse
                {
                    Text = "I apologize, but I couldn't process the audio. Please try typing your question instead.",
                    ContentType = "audio",
                    ProcessedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<HealthDataExtractionResult> ExtractHealthDataAsync(string content, string contentType)
        {
            try
            {
                var promptTemplate = await _configService.GetConfigAsync("health_data_extraction_prompt");
                
                if (string.IsNullOrEmpty(promptTemplate))
                {
                    _logger.LogWarning("Missing configuration: health_data_extraction_prompt not found");
                    return new HealthDataExtractionResult
                    {
                        Summary = "Configuration error: Unable to extract health data",
                        ExtractedAt = DateTime.UtcNow,
                        OriginalContent = content,
                        ContentType = contentType
                    };
                }

                var prompt = promptTemplate
                    .Replace("{contentType}", contentType)
                    .Replace("{content}", content);

                var textResponse = await GenerateResponseAsync(prompt);
                
                HealthDataExtractionResult result;
                try
                {
                    var jsonStart = textResponse.IndexOf('{');
                    var jsonEnd = textResponse.LastIndexOf('}');
                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        var jsonText = textResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                        result = JsonSerializer.Deserialize<HealthDataExtractionResult>(jsonText) ?? new HealthDataExtractionResult();
                    }
                    else
                    {
                        result = new HealthDataExtractionResult
                        {
                            Summary = textResponse,
                            ExtractedAt = DateTime.UtcNow
                        };
                    }
                }
                catch (JsonException)
                {
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
                return new HealthDataExtractionResult
                {
                    Summary = "Error extracting health data",
                    ExtractedAt = DateTime.UtcNow,
                    OriginalContent = content,
                    ContentType = contentType
                };
            }
        }
    }
}