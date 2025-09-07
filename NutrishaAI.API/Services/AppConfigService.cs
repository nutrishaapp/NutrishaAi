using Microsoft.Extensions.Caching.Memory;
using NutrishaAI.Core.Entities;
using Supabase;
using System.Security.Claims;

namespace NutrishaAI.API.Services
{
    public interface IAppConfigService
    {
        Task<string?> GetConfigAsync(string key);
        Task<string> GetConfigAsync(string key, string defaultValue);
        Task<AppConfig?> GetFullConfigAsync(string key);
        Task<List<AppConfig>> GetAllConfigsAsync();
        Task<bool> UpdateConfigAsync(string key, string value, Guid updatedBy);
        Task<bool> CreateConfigAsync(string key, string value, Guid createdBy);
        Task<bool> DeleteConfigAsync(string key);
        void ClearCache();
        void ClearCache(string key);
    }

    public class AppConfigService : IAppConfigService
    {
        private readonly Client _supabaseClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AppConfigService> _logger;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);
        private const string CACHE_KEY_PREFIX = "appconfig:";

        public AppConfigService(
            Client supabaseClient,
            IMemoryCache cache,
            ILogger<AppConfigService> logger)
        {
            _supabaseClient = supabaseClient;
            _cache = cache;
            _logger = logger;
        }

        public async Task<string?> GetConfigAsync(string key)
        {
            try
            {
                var cacheKey = CACHE_KEY_PREFIX + key;
                
                if (_cache.TryGetValue(cacheKey, out string? cachedValue))
                {
                    return cachedValue;
                }

                var config = await _supabaseClient
                    .From<AppConfig>()
                    .Filter("key", Supabase.Postgrest.Constants.Operator.Equals, key)
                    .Single();

                if (config != null)
                {
                    _cache.Set(cacheKey, config.Value, _cacheExpiration);
                    return config.Value;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving config for key: {Key}", key);
                return null;
            }
        }

        public async Task<string> GetConfigAsync(string key, string defaultValue)
        {
            var value = await GetConfigAsync(key);
            return value ?? defaultValue;
        }

        public async Task<AppConfig?> GetFullConfigAsync(string key)
        {
            try
            {
                return await _supabaseClient
                    .From<AppConfig>()
                    .Filter("key", Supabase.Postgrest.Constants.Operator.Equals, key)
                    .Single();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving full config for key: {Key}", key);
                return null;
            }
        }

        public async Task<List<AppConfig>> GetAllConfigsAsync()
        {
            try
            {
                _logger.LogInformation("Starting to retrieve all configs from Supabase");
                
                var configs = await _supabaseClient
                    .From<AppConfig>()
                    .Order("key", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                _logger.LogInformation("Successfully retrieved {Count} configs", configs.Models.Count);
                return configs.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all configs: {Message}", ex.Message);
                throw; // Re-throw to see the actual error in controller
            }
        }

        public async Task<bool> UpdateConfigAsync(string key, string value, Guid updatedBy)
        {
            try
            {
                // Check if config exists
                var existingConfig = await GetFullConfigAsync(key);
                
                if (existingConfig != null)
                {
                    // Update existing config
                    existingConfig.Value = value;
                    existingConfig.UpdatedAt = DateTime.UtcNow;
                    existingConfig.UpdatedBy = updatedBy;

                    await _supabaseClient
                        .From<AppConfig>()
                        .Filter("key", Supabase.Postgrest.Constants.Operator.Equals, key)
                        .Set(x => x.Value!, value)
                        .Set(x => x.UpdatedAt!, DateTime.UtcNow)
                        .Set(x => x.UpdatedBy!, updatedBy)
                        .Update();
                }
                else
                {
                    // Create new config
                    return await CreateConfigAsync(key, value, updatedBy);
                }

                // Clear cache for this key
                ClearCache(key);
                
                _logger.LogInformation("Config updated successfully: {Key}", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating config: {Key}", key);
                return false;
            }
        }

        public async Task<bool> CreateConfigAsync(string key, string value, Guid createdBy)
        {
            try
            {
                var config = new AppConfig
                {
                    Key = key,
                    Value = value,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = createdBy
                };

                await _supabaseClient
                    .From<AppConfig>()
                    .Insert(config);

                // Clear cache to ensure fresh data
                ClearCache(key);
                
                _logger.LogInformation("Config created successfully: {Key}", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating config: {Key}", key);
                return false;
            }
        }

        public async Task<bool> DeleteConfigAsync(string key)
        {
            try
            {
                await _supabaseClient
                    .From<AppConfig>()
                    .Filter("key", Supabase.Postgrest.Constants.Operator.Equals, key)
                    .Delete();

                // Clear cache for this key
                ClearCache(key);
                
                _logger.LogInformation("Config deleted successfully: {Key}", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting config: {Key}", key);
                return false;
            }
        }

        public void ClearCache()
        {
            // Note: IMemoryCache doesn't have a clear all method, so we'd need to track keys
            // For now, we'll rely on cache expiration
            _logger.LogInformation("Cache clear requested - relying on expiration");
        }

        public void ClearCache(string key)
        {
            var cacheKey = CACHE_KEY_PREFIX + key;
            _cache.Remove(cacheKey);
            _logger.LogDebug("Cache cleared for key: {Key}", key);
        }
    }
}