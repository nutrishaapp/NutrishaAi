using System.Text;
using System.Text.Json;

namespace NutrishaAI.API.Services
{
    public interface ISimpleGeminiService
    {
        Task<string> GenerateResponseAsync(string prompt);
        Task<string> GenerateNutritionistResponseAsync(string userMessage, string? conversationContext = null);
    }

    public class SimpleGeminiService : ISimpleGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SimpleGeminiService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public SimpleGeminiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<SimpleGeminiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
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

        public async Task<string> GenerateNutritionistResponseAsync(string userMessage, string? conversationContext = null)
        {
            var systemPrompt = @"
You are NutrishaAI, a professional AI nutritionist and health coach. You provide personalized nutrition advice, meal planning, and health guidance.

Your expertise includes:
- Nutritional analysis and meal planning
- Dietary restrictions and allergies management
- Weight management strategies
- Sports nutrition
- Health condition-specific diets (diabetes, heart disease, etc.)
- Food science and nutrient interactions
- Behavioral nutrition coaching

Guidelines:
- Always provide evidence-based advice
- Ask clarifying questions when needed
- Be encouraging and supportive
- Provide practical, actionable recommendations
- Consider individual needs, preferences, and constraints
- Mention when medical consultation is recommended
- Keep responses conversational but professional
- Include specific examples and portion sizes when relevant

Current conversation context: " + (conversationContext ?? "This is the start of a new conversation.");

            var fullPrompt = $@"{systemPrompt}

User Message: {userMessage}

Please respond as NutrishaAI, providing helpful nutrition and health guidance:";

            return await GenerateResponseAsync(fullPrompt);
        }
    }
}