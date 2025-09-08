using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.API.Models.Entities;
using Supabase;
using Client = Supabase.Client;

namespace NutrishaAI.API.Services
{
    public class FirebaseNotificationService : IFirebaseNotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FirebaseNotificationService> _logger;
        private readonly Client _supabaseClient;

        public FirebaseNotificationService(
            IConfiguration configuration, 
            ILogger<FirebaseNotificationService> logger,
            Client supabaseClient)
        {
            _configuration = configuration;
            _logger = logger;
            _supabaseClient = supabaseClient;
            
            InitializeFirebase();
        }

        private void InitializeFirebase()
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var firebaseConfigPath = _configuration["Firebase:ServiceAccountKeyPath"];
                if (string.IsNullOrEmpty(firebaseConfigPath))
                {
                    _logger.LogWarning("Firebase service account key path not configured");
                    return;
                }

                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(firebaseConfigPath),
                });

                _logger.LogInformation("Firebase initialized successfully");
            }
        }

        public async Task<NotificationResponse> SendNotificationToTokenAsync(string token, SendNotificationRequest request)
        {
            try
            {
                var message = CreateMessage(request, token);
                
                // Add timeout for Firebase operation
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken: cts.Token);
                
                _logger.LogInformation("Notification sent successfully to token. MessageId: {MessageId}", messageId);
                
                return new NotificationResponse
                {
                    Success = true,
                    MessageId = messageId
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Firebase notification request timed out for token: {Token}", token);
                return new NotificationResponse
                {
                    Success = false,
                    Error = "Notification request timed out"
                };
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogWarning(ex, "Firebase messaging error for token: {Token}. Error: {Error}", token, ex.Message);
                return new NotificationResponse
                {
                    Success = false,
                    Error = $"Firebase error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to token: {Token}", token);
                return new NotificationResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<NotificationResponse> SendNotificationToUserAsync(string userId, SendNotificationRequest request)
        {
            try
            {
                // Get user's FCM tokens from database
                var tokens = await GetUserTokensAsync(userId);
                
                if (!tokens.Any())
                {
                    return new NotificationResponse
                    {
                        Success = false,
                        Error = "No FCM tokens found for user"
                    };
                }

                return await SendNotificationToMultipleTokensAsync(tokens.ToArray(), request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to user: {UserId}", userId);
                return new NotificationResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<NotificationResponse> SendNotificationToTopicAsync(string topic, SendNotificationRequest request)
        {
            try
            {
                var message = CreateMessage(request, topic: topic);
                
                // Add timeout for Firebase operation
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken: cts.Token);
                
                _logger.LogInformation("Notification sent successfully to topic: {Topic}. MessageId: {MessageId}", topic, messageId);
                
                return new NotificationResponse
                {
                    Success = true,
                    MessageId = messageId
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Firebase notification request timed out for topic: {Topic}", topic);
                return new NotificationResponse
                {
                    Success = false,
                    Error = "Notification request timed out"
                };
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogWarning(ex, "Firebase messaging error for topic: {Topic}. Error: {Error}", topic, ex.Message);
                return new NotificationResponse
                {
                    Success = false,
                    Error = $"Firebase error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to topic: {Topic}", topic);
                return new NotificationResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<NotificationResponse> SendNotificationToMultipleTokensAsync(string[] tokens, SendNotificationRequest request)
        {
            try
            {
                var message = CreateMulticastMessage(request, tokens);
                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
                
                _logger.LogInformation("Multicast notification sent. Success: {SuccessCount}, Failure: {FailureCount}", 
                    response.SuccessCount, response.FailureCount);
                
                return new NotificationResponse
                {
                    Success = response.SuccessCount > 0,
                    SuccessCount = response.SuccessCount,
                    FailureCount = response.FailureCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send multicast notification");
                return new NotificationResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<TopicSubscriptionResponse> SubscribeToTopicAsync(string[] tokens, string topic)
        {
            try
            {
                var response = await FirebaseMessaging.DefaultInstance.SubscribeToTopicAsync(tokens, topic);
                
                _logger.LogInformation("Topic subscription completed. Success: {SuccessCount}, Failure: {FailureCount}", 
                    response.SuccessCount, response.FailureCount);
                
                return new TopicSubscriptionResponse
                {
                    Success = response.SuccessCount > 0,
                    SuccessCount = response.SuccessCount,
                    FailureCount = response.FailureCount,
                    FailedTokens = response.Errors.Select(e => tokens[e.Index]).ToArray()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to topic: {Topic}", topic);
                return new TopicSubscriptionResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<TopicSubscriptionResponse> UnsubscribeFromTopicAsync(string[] tokens, string topic)
        {
            try
            {
                var response = await FirebaseMessaging.DefaultInstance.UnsubscribeFromTopicAsync(tokens, topic);
                
                _logger.LogInformation("Topic unsubscription completed. Success: {SuccessCount}, Failure: {FailureCount}", 
                    response.SuccessCount, response.FailureCount);
                
                return new TopicSubscriptionResponse
                {
                    Success = response.SuccessCount > 0,
                    SuccessCount = response.SuccessCount,
                    FailureCount = response.FailureCount,
                    FailedTokens = response.Errors.Select(e => tokens[e.Index]).ToArray()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from topic: {Topic}", topic);
                return new TopicSubscriptionResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<TokenRegistrationResponse> RegisterTokenAsync(RegisterTokenRequest request)
        {
            try
            {
                // Store the token in your database
                // This is a placeholder - you'll need to implement the actual database storage
                await StoreTokenInDatabaseAsync(request);
                
                _logger.LogInformation("FCM token registered for user: {UserId}", request.UserId);
                
                return new TokenRegistrationResponse
                {
                    Success = true,
                    TokenId = Guid.NewGuid().ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register FCM token for user: {UserId}", request.UserId);
                return new TokenRegistrationResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                // Create a test message to validate the token
                var message = new Message()
                {
                    Token = token,
                    Notification = new Notification()
                    {
                        Title = "Test",
                        Body = "Test"
                    }
                };

                // Try to send a dry run
                await FirebaseMessaging.DefaultInstance.SendAsync(message, dryRun: true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Token validation failed: {Error}", ex.Message);
                return false;
            }
        }

        private Message CreateMessage(SendNotificationRequest request, string? token = null, string? topic = null)
        {
            var notification = new Notification()
            {
                Title = request.Title,
                Body = request.Body,
                ImageUrl = request.ImageUrl
            };

            var message = new Message()
            {
                Notification = notification,
                Data = request.Data
            };

            if (!string.IsNullOrEmpty(token))
            {
                message.Token = token;
            }
            else if (!string.IsNullOrEmpty(topic))
            {
                message.Topic = topic;
            }

            // Add Android-specific configuration
            message.Android = new AndroidConfig()
            {
                Notification = new AndroidNotification()
                {
                    ClickAction = request.ClickAction,
                    Icon = "ic_notification",
                    Color = "#FF6B35"
                }
            };

            // Add iOS-specific configuration
            message.Apns = new ApnsConfig()
            {
                Headers = new Dictionary<string, string>
                {
                    {"apns-priority", "10"}
                },
                Aps = new Aps()
                {
                    Alert = new ApsAlert()
                    {
                        Title = request.Title,
                        Body = request.Body
                    },
                    Badge = 1,
                    Sound = "default"
                }
            };

            return message;
        }

        private MulticastMessage CreateMulticastMessage(SendNotificationRequest request, string[] tokens)
        {
            var notification = new Notification()
            {
                Title = request.Title,
                Body = request.Body,
                ImageUrl = request.ImageUrl
            };

            return new MulticastMessage()
            {
                Tokens = tokens,
                Notification = notification,
                Data = request.Data,
                Android = new AndroidConfig()
                {
                    Notification = new AndroidNotification()
                    {
                        ClickAction = request.ClickAction,
                        Icon = "ic_notification",
                        Color = "#FF6B35"
                    }
                },
                Apns = new ApnsConfig()
                {
                    Headers = new Dictionary<string, string>
                    {
                        {"apns-priority", "10"}
                    },
                    Aps = new Aps()
                    {
                        Alert = new ApsAlert()
                        {
                            Title = request.Title,
                            Body = request.Body
                        },
                        Badge = 1,
                        Sound = "default"
                    }
                }
            };
        }

        private async Task<List<string>> GetUserTokensAsync(string userId)
        {
            try
            {
                var response = await _supabaseClient
                    .From<FcmToken>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                    .Get();

                return response.Models?.Select(t => t.Token).ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve user tokens for user: {UserId}", userId);
                return new List<string>();
            }
        }

        private async Task StoreTokenInDatabaseAsync(RegisterTokenRequest request)
        {
            try
            {
                // First, deactivate any existing tokens for this user and device combination
                if (!string.IsNullOrEmpty(request.DeviceId))
                {
                    var existingTokens = await _supabaseClient
                        .From<FcmToken>()
                        .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, request.UserId)
                        .Filter("device_id", Supabase.Postgrest.Constants.Operator.Equals, request.DeviceId)
                        .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                        .Get();

                    if (existingTokens.Models?.Any() == true)
                    {
                        foreach (var existingToken in existingTokens.Models)
                        {
                            existingToken.IsActive = false;
                            await _supabaseClient
                                .From<FcmToken>()
                                .Update(existingToken);
                        }
                    }
                }

                // Create new FCM token
                var fcmToken = new FcmToken
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(request.UserId),
                    Token = request.Token,
                    DeviceId = request.DeviceId,
                    Platform = request.Platform,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<FcmToken>()
                    .Insert(fcmToken);

                _logger.LogInformation("FCM token registered successfully for user: {UserId}", request.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store FCM token for user: {UserId}", request.UserId);
                throw;
            }
        }

        public async Task<NotificationResponse> SendNotificationByEmailAsync(SendNotificationByEmailRequest request)
        {
            try
            {
                // Lookup user by email
                var user = await GetUserByEmailAsync(request.Email);
                if (user == null)
                {
                    return new NotificationResponse
                    {
                        Success = false,
                        Error = $"User with email {request.Email} not found"
                    };
                }

                // Send notification to the user using existing method
                return await SendNotificationToUserAsync(user.Id.ToString(), request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification by email: {Email}", request.Email);
                return new NotificationResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<DeviceDeactivationResponse> DeactivateDeviceAsync(string userId, string deviceId)
        {
            try
            {
                // Find the FCM token for this user and device
                var tokens = await _supabaseClient
                    .From<FcmToken>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Filter("device_id", Supabase.Postgrest.Constants.Operator.Equals, deviceId)
                    .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                    .Get();

                if (tokens.Models?.Any() != true)
                {
                    return new DeviceDeactivationResponse
                    {
                        Success = false,
                        Error = "No active FCM tokens found for this device"
                    };
                }

                // Deactivate all matching tokens
                int deactivatedCount = 0;
                foreach (var token in tokens.Models)
                {
                    token.IsActive = false;
                    token.UpdatedAt = DateTime.UtcNow;
                    
                    await _supabaseClient
                        .From<FcmToken>()
                        .Update(token);
                    
                    deactivatedCount++;
                }

                _logger.LogInformation("Deactivated {Count} FCM tokens for user {UserId} device {DeviceId}", 
                    deactivatedCount, userId, deviceId);

                return new DeviceDeactivationResponse
                {
                    Success = true,
                    Message = $"Successfully deactivated {deactivatedCount} token(s) for device {deviceId}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate device {DeviceId} for user {UserId}", deviceId, userId);
                return new DeviceDeactivationResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<NutrishaAI.Core.Entities.User?> GetUserByEmailAsync(string email)
        {
            try
            {
                var response = await _supabaseClient
                    .From<NutrishaAI.Core.Entities.User>()
                    .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, email)
                    .Get();

                return response.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to lookup user by email: {Email}", email);
                return null;
            }
        }
    }
}