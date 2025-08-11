using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using NutrishaAI.Core.Entities;
using Supabase;

namespace NutrishaAI.API.Services
{
    public interface IApiKeyService
    {
        Task<ApiKeyValidationResult> ValidateApiKey(string apiKey, string userId);
        Task<ApiKey> GenerateApiKey(string userId, string name, string[]? permissions);
        Task RevokeApiKey(string apiKeyId);
        Task<bool> CheckRateLimit(string apiKeyId);
        Task LogUsage(string apiKeyId, string endpoint);
        Task<IEnumerable<ApiKey>> GetUserApiKeys(string userId);
    }

    public class ApiKeyService : IApiKeyService
    {
        private readonly Client _supabaseClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ApiKeyService> _logger;

        public ApiKeyService(
            Client supabaseClient,
            IMemoryCache cache,
            ILogger<ApiKeyService> logger)
        {
            _supabaseClient = supabaseClient;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApiKey> GenerateApiKey(string userId, string name, string[]? permissions)
        {
            // Generate secure random key
            var apiKey = $"ntr_prod_{GenerateSecureToken(32)}";
            var keyHash = BCrypt.Net.BCrypt.HashPassword(apiKey);

            var apiKeyRecord = new ApiKey
            {
                Id = Guid.NewGuid(),
                KeyHash = keyHash,
                KeyPrefix = apiKey.Substring(0, 12),
                Name = name,
                UserId = Guid.Parse(userId),
                Permissions = permissions,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Insert into database
            await _supabaseClient
                .From<ApiKey>()
                .Insert(apiKeyRecord);

            // Return with the full key (only time it's shown)
            apiKeyRecord.FullKey = apiKey;
            return apiKeyRecord;
        }

        public async Task<ApiKeyValidationResult> ValidateApiKey(string apiKey, string userId)
        {
            // Cache check for performance
            var cacheKey = $"apikey_{apiKey.Substring(0, Math.Min(12, apiKey.Length))}_{userId}";
            if (_cache.TryGetValue<ApiKeyValidationResult>(cacheKey, out var cachedResult))
            {
                if (cachedResult != null && cachedResult.IsValid)
                {
                    return cachedResult;
                }
            }

            try
            {
                // Extract prefix for database lookup
                var keyPrefix = apiKey.Substring(0, Math.Min(12, apiKey.Length));

                // Database lookup
                var response = await _supabaseClient
                    .From<ApiKey>()
                    .Where(x => x.KeyPrefix == keyPrefix)
                    .Where(x => x.UserId == Guid.Parse(userId))
                    .Single();

                if (response == null)
                {
                    return new ApiKeyValidationResult 
                    { 
                        IsValid = false, 
                        Error = "Invalid API key" 
                    };
                }

                // Verify hash
                if (!BCrypt.Net.BCrypt.Verify(apiKey, response.KeyHash))
                {
                    return new ApiKeyValidationResult 
                    { 
                        IsValid = false, 
                        Error = "Invalid API key" 
                    };
                }

                if (!response.IsActive)
                {
                    return new ApiKeyValidationResult 
                    { 
                        IsValid = false, 
                        Error = "API key is inactive" 
                    };
                }

                if (response.ExpiresAt.HasValue && response.ExpiresAt < DateTime.UtcNow)
                {
                    return new ApiKeyValidationResult 
                    { 
                        IsValid = false, 
                        Error = "API key expired" 
                    };
                }

                // Check rate limit
                if (!await CheckRateLimit(response.Id.ToString()))
                {
                    return new ApiKeyValidationResult 
                    { 
                        IsValid = false, 
                        Error = "Rate limit exceeded" 
                    };
                }

                var result = new ApiKeyValidationResult
                {
                    IsValid = true,
                    ApiKeyId = response.Id.ToString(),
                    UserId = response.UserId.ToString(),
                    UserRole = await GetUserRole(userId),
                    Permissions = response.Permissions
                };

                // Cache for future requests (5 minutes)
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating API key");
                return new ApiKeyValidationResult 
                { 
                    IsValid = false, 
                    Error = "Error validating API key" 
                };
            }
        }

        public async Task<bool> CheckRateLimit(string apiKeyId)
        {
            var rateLimitKey = $"ratelimit_{apiKeyId}_{DateTime.UtcNow.Hour}";
            var currentCount = _cache.Get<int>(rateLimitKey);

            if (currentCount >= 1000) // Default rate limit
            {
                return false;
            }

            _cache.Set(rateLimitKey, currentCount + 1, TimeSpan.FromHours(1));
            return true;
        }

        public async Task LogUsage(string apiKeyId, string endpoint)
        {
            try
            {
                // Update last used timestamp
                await _supabaseClient
                    .From<ApiKey>()
                    .Where(x => x.Id == Guid.Parse(apiKeyId))
                    .Set(x => x.LastUsedAt, DateTime.UtcNow)
                    .Update();

                // You can also log to api_key_usage table if needed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging API key usage");
            }
        }

        public async Task RevokeApiKey(string apiKeyId)
        {
            await _supabaseClient
                .From<ApiKey>()
                .Where(x => x.Id == Guid.Parse(apiKeyId))
                .Set(x => x.IsActive, false)
                .Update();
        }

        public async Task<IEnumerable<ApiKey>> GetUserApiKeys(string userId)
        {
            var response = await _supabaseClient
                .From<ApiKey>()
                .Where(x => x.UserId == Guid.Parse(userId))
                .Where(x => x.IsActive == true)
                .Get();

            return response.Models;
        }

        private string GenerateSecureToken(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var data = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[data[i] % chars.Length];
            }
            return new string(result);
        }

        private async Task<string> GetUserRole(string userId)
        {
            try
            {
                var user = await _supabaseClient
                    .From<User>()
                    .Where(x => x.Id == Guid.Parse(userId))
                    .Single();
                
                return user?.Role ?? "patient";
            }
            catch
            {
                return "patient";
            }
        }
    }

    public class ApiKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string? Error { get; set; }
        public string ApiKeyId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? UserRole { get; set; }
        public string[]? Permissions { get; set; }
    }
}