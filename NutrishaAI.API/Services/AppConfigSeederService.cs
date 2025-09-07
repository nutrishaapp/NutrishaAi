using NutrishaAI.Core.Entities;
using Supabase;

namespace NutrishaAI.API.Services
{
    public interface IAppConfigSeederService
    {
        Task SeedDefaultConfigsAsync();
    }

    public class AppConfigSeederService : IAppConfigSeederService
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<AppConfigSeederService> _logger;

        public AppConfigSeederService(
            Client supabaseClient,
            ILogger<AppConfigSeederService> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        public async Task SeedDefaultConfigsAsync()
        {
            try
            {
                var defaultConfigs = new List<AppConfig>
                {
                    new AppConfig
                    {
                        Key = "nutritionist_system_prompt",
                        Value = @"
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

Current conversation context: {conversationContext}",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = Guid.Empty // Will be set to NULL in database
                    },
                    new AppConfig
                    {
                        Key = "image_analysis_prompt",
                        Value = @"You are a professional nutritionist analyzing food images.
                
Analyze this image and provide:
1. Food identification and portion estimation
2. Nutritional analysis (calories, macros, etc.)
3. Health recommendations
4. Any dietary concerns or benefits
                
{textPrompt}
                
[Image data provided as base64]",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = Guid.Empty // Will be set to NULL in database
                    },
                    new AppConfig
                    {
                        Key = "health_data_extraction_prompt",
                        Value = @"Extract health and nutrition data from the following content.
                
Content Type: {contentType}
Content: {content}
                
Extract and return in JSON format:
- foods: array of {{name, calories, servingSize}}
- exercises: array of {{activity, durationMinutes, intensity}}
- symptoms: array of strings
- dietary_restrictions: array of strings
- health_goals: array of strings
- measurements: object with key-value pairs
- summary: brief text summary
                
If no relevant data is found, return empty arrays/objects.",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = Guid.Empty // Will be set to NULL in database
                    },
                    new AppConfig
                    {
                        Key = "error_response_message",
                        Value = "I apologize, but I'm experiencing technical difficulties. Please try again later.",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = Guid.Empty // Will be set to NULL in database
                    },
                    new AppConfig
                    {
                        Key = "voice_processing_prompt",
                        Value = @"You are a professional nutritionist processing audio content.
                
{textPrompt}
                
Note: Audio transcription is not yet implemented. Please provide nutrition advice based on this audio.",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = Guid.Empty // Will be set to NULL in database
                    }
                };

                foreach (var config in defaultConfigs)
                {
                    try
                    {
                        // Check if config already exists
                        var existing = await _supabaseClient
                            .From<AppConfig>()
                            .Filter("key", Supabase.Postgrest.Constants.Operator.Equals, config.Key)
                            .Single();

                        if (existing == null)
                        {
                            await _supabaseClient
                                .From<AppConfig>()
                                .Insert(config);

                            _logger.LogInformation("Seeded config: {Key}", config.Key);
                        }
                        else
                        {
                            _logger.LogInformation("Config already exists: {Key}", config.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error seeding config: {Key}", config.Key);
                    }
                }

                _logger.LogInformation("Config seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during config seeding");
                throw;
            }
        }
    }
}