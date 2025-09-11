using System.Text;
using System.Text.Json;

namespace NutrishaAI.API.Services
{
    public class GeminiJsonResponse
    {
        public string Reply { get; set; } = string.Empty;
        public int ContentCategory { get; set; }
    }

    public class AttachmentContent
    {
        public string Base64Data { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "image", "audio", "document", etc.
    }

    public class GeminiResponse
    {
        public string Text { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }

    public interface IGeminiService
    {
        Task<string> ProcessTextAsync(string text, string? context = null);
        Task<GeminiResponse> ProcessMultimediaAsync(Stream fileStream, string contentType, string? textPrompt = null);
        Task<GeminiResponse> ProcessImageAsync(Stream imageStream, string? textPrompt = null);
        Task<GeminiResponse> ProcessVoiceNoteAsync(Stream audioStream, string? textPrompt = null);
    }

    public interface ISimpleGeminiService
    {
        Task<string> GenerateNutritionistResponseAsync(string userMessage, string? conversationContext = null, List<AttachmentContent>? attachments = null);
        Task<string> ExtractContentAsync(string prompt, List<AttachmentContent>? attachments = null);
        Task<Models.ExtractedMemory> ExtractMemoryAsync(string message, string? conversationContext = null);
    }

    public class SimpleGeminiService : ISimpleGeminiService, IGeminiService
    {
        private readonly HttpClient _httpClient;
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
            _configService = configService;
            _logger = logger;
            
            _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
            _baseUrl = configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
            _model = configuration["Gemini:Model"] ?? "gemini-1.5-flash";
        }

        public async Task<string> GenerateNutritionistResponseAsync(string userMessage, string? conversationContext = null, List<AttachmentContent>? attachments = null)
        {
            try
            {
                _logger.LogInformation("GenerateNutritionistResponseAsync called with:");
                _logger.LogInformation("  - User Message: '{UserMessage}'", userMessage);
                _logger.LogInformation("  - Context Length: {ContextLength}", conversationContext?.Length ?? 0);
                _logger.LogInformation("  - Context Value: '{Context}'", conversationContext ?? "NULL");
                _logger.LogInformation("  - Attachments Count: {AttachmentCount}", attachments?.Count ?? 0);

                // Get configuration
                var systemPromptTemplate = await _configService.GetConfigAsync("risha_prompt");
                var contextPromptTemplate = await _configService.GetConfigAsync("conversation_context_prompt");
                var jsonStructure = await _configService.GetConfigAsync("response_json_structure");

                _logger.LogInformation("Configuration retrieved:");
                _logger.LogInformation("  - System Prompt Template Length: {Length}", systemPromptTemplate?.Length ?? 0);
                _logger.LogInformation("  - System Prompt Template Preview: '{Preview}'", 
                    systemPromptTemplate?.Length > 200 ? systemPromptTemplate.Substring(0, 200) + "..." : systemPromptTemplate ?? "NULL");
                _logger.LogInformation("  - Context Prompt Template Length: {Length}", contextPromptTemplate?.Length ?? 0);
                _logger.LogInformation("  - JSON Structure Length: {Length}", jsonStructure?.Length ?? 0);

                if (string.IsNullOrEmpty(systemPromptTemplate) || string.IsNullOrEmpty(jsonStructure))
                {
                    _logger.LogWarning("Missing configuration: risha_prompt or response_json_structure not found");
                    return "I apologize, but I'm experiencing configuration issues. Please try again later.";
                }

                // Build the full nutritionist prompt with context
                var contextToUse = conversationContext ?? "This is the start of a new conversation.";
                _logger.LogInformation("Context to use for replacement: '{ContextToUse}'", contextToUse);
                
                // Combine the main prompt with the context prompt
                var systemPrompt = systemPromptTemplate;
                if (!string.IsNullOrEmpty(contextPromptTemplate))
                {
                    var contextPrompt = contextPromptTemplate.Replace("{conversationContext}", contextToUse);
                    systemPrompt = systemPromptTemplate + contextPrompt;
                }
                else
                {
                }
                
                _logger.LogInformation("Final system prompt after context addition (first 500 chars): '{SystemPrompt}'", 
                    systemPrompt.Length > 500 ? systemPrompt.Substring(0, 500) + "..." : systemPrompt);

                var fullPrompt = $@"{systemPrompt}

User Message: {userMessage}

{jsonStructure}";

                _logger.LogInformation("Full prompt length: {Length} characters", fullPrompt.Length);
                _logger.LogDebug("Full prompt being sent to Gemini: '{FullPrompt}'", fullPrompt);

                // Generate response with or without attachments
                var rawResponse = await GenerateGeminiResponseAsync(fullPrompt, attachments);
                
                // Parse and format the response
                var parsedResponse = ParseGeminiJsonResponse(rawResponse);
                return $"{parsedResponse.Reply} (Content:{parsedResponse.ContentCategory})";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating nutritionist response");
                return "I apologize, but I'm experiencing technical difficulties. Please try again later.";
            }
        }

        public async Task<string> ExtractContentAsync(string prompt, List<AttachmentContent>? attachments = null)
        {
            try
            {
                // Direct Gemini API call without nutritionist prompt or JSON formatting
                var rawResponse = await GenerateGeminiResponseAsync(prompt, attachments);
                return rawResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting content with Gemini API");
                return "Failed to extract content from the document.";
            }
        }

        private async Task<string> GenerateGeminiResponseAsync(string prompt, List<AttachmentContent>? attachments = null)
        {
            try
            {
                _logger.LogInformation("GenerateGeminiResponseAsync called");
                _logger.LogInformation("  - Prompt length: {Length} characters", prompt?.Length ?? 0);
                _logger.LogInformation("  - Prompt preview (first 300 chars): '{PromptPreview}'", 
                    prompt?.Length > 300 ? prompt.Substring(0, 300) + "..." : prompt ?? "NULL");
                _logger.LogInformation("  - Attachments count: {Count}", attachments?.Count ?? 0);
                
                var requestUrl = $"{_baseUrl}/models/{_model}:generateContent?key={_apiKey}";
                _logger.LogDebug("Request URL (without API key): {BaseUrl}/models/{Model}:generateContent", _baseUrl, _model);
                
                // Build the request parts
                var parts = new List<object> { new { text = prompt } };
                _logger.LogDebug("Added text part to request");
                
                // Add attachments if present
                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        if (attachment.Type == "image" && !string.IsNullOrEmpty(attachment.Base64Data))
                        {
                            parts.Add(new 
                            { 
                                inline_data = new 
                                {
                                    mime_type = attachment.MimeType,
                                    data = attachment.Base64Data
                                }
                            });
                            _logger.LogInformation("Added image attachment: {MimeType}, Data length: {Length}", attachment.MimeType, attachment.Base64Data.Length);
                        }
                        else if (attachment.Type == "document" && !string.IsNullOrEmpty(attachment.Base64Data))
                        {
                            // Gemini API supports PDFs directly - send as inline_data
                            parts.Add(new 
                            { 
                                inline_data = new 
                                {
                                    mime_type = "application/pdf",
                                    data = attachment.Base64Data
                                }
                            });
                            _logger.LogInformation("Added PDF document to Gemini request, Data length: {Length}", attachment.Base64Data.Length);
                        }
                        else if (attachment.Type == "audio" && !string.IsNullOrEmpty(attachment.Base64Data))
                        {
                            // Gemini supports audio files - send as inline_data
                            parts.Add(new 
                            { 
                                inline_data = new 
                                {
                                    mime_type = attachment.MimeType,
                                    data = attachment.Base64Data
                                }
                            });
                            _logger.LogInformation("Added audio file to Gemini request: {MimeType}, Data length: {Length}", attachment.MimeType, attachment.Base64Data.Length);
                        }
                    }
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = parts.ToArray() }
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
                _logger.LogDebug("Request JSON length: {Length} characters", json.Length);
                _logger.LogDebug("Request JSON (first 1000 chars): '{JsonPreview}'", 
                    json.Length > 1000 ? json.Substring(0, 1000) + "..." : json);
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _logger.LogInformation("Sending request to Gemini API...");

                var response = await _httpClient.PostAsync(requestUrl, content);
                _logger.LogInformation("Received response from Gemini API: {StatusCode}", response.StatusCode);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    return attachments?.Any() == true 
                        ? "I apologize, but I'm having trouble analyzing the content right now. Please try again."
                        : "I apologize, but I'm having trouble generating a response right now. Please try again.";
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Raw Gemini response length: {Length} characters", responseContent.Length);
                _logger.LogInformation("Raw Gemini response (first 500 chars): '{Response}'", 
                    responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);
                
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                _logger.LogDebug("Successfully deserialized Gemini response JSON");
                
                // Extract the generated text
                if (responseObj.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    _logger.LogDebug("Found {CandidateCount} candidates in response", candidates.GetArrayLength());
                    var firstCandidate = candidates[0];
                    
                    if (firstCandidate.TryGetProperty("content", out var contentElement) &&
                        contentElement.TryGetProperty("parts", out var responseParts) &&
                        responseParts.GetArrayLength() > 0)
                    {
                        _logger.LogDebug("Found {PartCount} parts in first candidate", responseParts.GetArrayLength());
                        var firstPart = responseParts[0];
                        
                        if (firstPart.TryGetProperty("text", out var textElement))
                        {
                            var extractedText = textElement.GetString() ?? "I couldn't generate a response.";
                            _logger.LogInformation("Successfully extracted text from Gemini response: {Length} characters", extractedText.Length);
                            _logger.LogInformation("Extracted text preview: '{TextPreview}'", 
                                extractedText.Length > 200 ? extractedText.Substring(0, 200) + "..." : extractedText);
                            return extractedText;
                        }
                        else
                        {
                            _logger.LogWarning("No 'text' property found in first part of Gemini response");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No 'content' or 'parts' found in first candidate of Gemini response");
                    }
                }
                else
                {
                    _logger.LogWarning("No 'candidates' found in Gemini response");
                }

                _logger.LogError("Failed to extract text from Gemini response structure");
                return "I couldn't generate a response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response with Gemini API");
                return "I apologize, but I'm experiencing technical difficulties. Please try again later.";
            }
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

        public async Task<Models.ExtractedMemory> ExtractMemoryAsync(string message, string? conversationContext = null)
        {
            try
            {
                var prompt = $@"Analyze the following user message and extract important information that should be remembered for future conversations.

Context from conversation:
{conversationContext ?? "This is the start of a conversation."}

User Message:
{message}

Extract and return a JSON response with the following structure:
{{
  ""summary"": ""A concise summary of the important information to remember (max 200 characters)"",
  ""shouldSave"": true/false (whether this contains information worth remembering),
  ""topics"": [""array"", ""of"", ""relevant"", ""topics""],
  ""metadata"": {{
    ""hasHealthInfo"": true/false,
    ""hasDietaryPreferences"": true/false,
    ""hasGoals"": true/false,
    ""hasAllergies"": true/false,
    ""category"": ""health|diet|lifestyle|general""
  }}
}}

Focus on extracting:
- Health conditions, symptoms, or medical history
- Dietary preferences, restrictions, or allergies
- Personal goals or objectives
- Important personal information
- Preferences about nutrition or health

Do NOT save:
- Casual greetings or small talk
- Temporary questions without personal context
- Information already in the conversation context

Return ONLY the JSON object, no additional text or markdown formatting.";

                var rawResponse = await GenerateGeminiResponseAsync(prompt, null);
                
                // Parse the JSON response
                var cleanedResponse = CleanJsonResponse(rawResponse);
                var jsonText = ExtractJsonFromResponse(cleanedResponse);
                
                if (!string.IsNullOrEmpty(jsonText))
                {
                    try
                    {
                        var memoryData = JsonSerializer.Deserialize<JsonElement>(jsonText, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        var extractedMemory = new Models.ExtractedMemory
                        {
                            Summary = memoryData.GetProperty("summary").GetString() ?? string.Empty,
                            ShouldSave = memoryData.GetProperty("shouldSave").GetBoolean(),
                            Topics = memoryData.TryGetProperty("topics", out var topics) 
                                ? topics.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToList()
                                : new List<string>(),
                            Metadata = new Dictionary<string, object>()
                        };

                        // Parse metadata if present
                        if (memoryData.TryGetProperty("metadata", out var metadata))
                        {
                            foreach (var prop in metadata.EnumerateObject())
                            {
                                extractedMemory.Metadata[prop.Name] = prop.Value.ValueKind switch
                                {
                                    JsonValueKind.String => prop.Value.GetString(),
                                    JsonValueKind.Number => prop.Value.GetDouble(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    _ => prop.Value.ToString()
                                };
                            }
                        }

                        _logger.LogDebug("Extracted memory: ShouldSave={ShouldSave}, Topics={TopicCount}", 
                            extractedMemory.ShouldSave, extractedMemory.Topics.Count);
                        
                        return extractedMemory;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse memory extraction JSON response");
                    }
                }

                // Return default if parsing fails
                return new Models.ExtractedMemory
                {
                    Summary = string.Empty,
                    ShouldSave = false,
                    Topics = new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting memory from message");
                return new Models.ExtractedMemory
                {
                    Summary = string.Empty,
                    ShouldSave = false,
                    Topics = new List<string>()
                };
            }
        }

        // Legacy methods to maintain compatibility with existing IGeminiService interface
        public async Task<string> ProcessTextAsync(string text, string? context = null)
        {
            return await GenerateNutritionistResponseAsync(text, context);
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
                // Don't use 'using' here to avoid disposing the stream prematurely
                var ms = new MemoryStream();
                imageStream.Position = 0; // Reset position
                await imageStream.CopyToAsync(ms);
                var imageBytes = ms.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);
                ms.Dispose();

                // Use the unified nutritionist response method with image attachment
                var attachments = new List<AttachmentContent>
                {
                    new AttachmentContent
                    {
                        Base64Data = base64Image,
                        MimeType = "image/jpeg",
                        Type = "image"
                    }
                };

                var userMessage = textPrompt ?? "Please analyze this image and provide nutritional information.";
                var response = await GenerateNutritionistResponseAsync(userMessage, null, attachments);

                return new GeminiResponse
                {
                    Text = response,
                    ContentType = "image",
                    ProcessedAt = DateTime.UtcNow
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
                // Note: Audio transcription not yet implemented
                // Use the unified nutritionist response method
                var userMessage = textPrompt ?? "Please provide nutrition advice. Note: Audio processing is not yet implemented.";
                var response = await GenerateNutritionistResponseAsync(userMessage);

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
    }
}