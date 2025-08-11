using Supabase.Realtime;
using System.Text.Json;
using NutrishaAI.Core.Entities;

namespace NutrishaAI.API.Services
{
    public interface ISupabaseRealtimeService
    {
        Task InitializeAsync();
        Task JoinChatChannelAsync(Guid conversationId, string userId);
        Task SendMessageAsync(Guid conversationId, Message message);
        Task LeaveChannelAsync(Guid conversationId);
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
        event EventHandler<UserStatusEventArgs> UserStatusChanged;
    }

    public class SupabaseRealtimeService : ISupabaseRealtimeService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger<SupabaseRealtimeService> _logger;
        private readonly Dictionary<Guid, RealtimeChannel> _channels = new();

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<UserStatusEventArgs>? UserStatusChanged;

        public SupabaseRealtimeService(
            Supabase.Client supabaseClient, 
            ILogger<SupabaseRealtimeService> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _supabaseClient.Realtime.ConnectAsync();
                _logger.LogInformation("Supabase Realtime connected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Supabase Realtime");
                throw;
            }
        }

        public async Task JoinChatChannelAsync(Guid conversationId, string userId)
        {
            try
            {
                var channelName = $"conversation:{conversationId}";
                
                // Leave existing channel if already joined
                if (_channels.ContainsKey(conversationId))
                {
                    await LeaveChannelAsync(conversationId);
                }

                var channel = _supabaseClient.Realtime.Channel(channelName);

                // Subscribe to channel
                await channel.Subscribe();

                _channels[conversationId] = channel;
                
                _logger.LogInformation("Joined chat channel for conversation {ConversationId}", conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join chat channel for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task SendMessageAsync(Guid conversationId, Message message)
        {
            try
            {
                if (_channels.TryGetValue(conversationId, out var channel))
                {
                    await channel.Send(Supabase.Realtime.Constants.ChannelEventName.Broadcast, null, new
                    {
                        type = "new_message",
                        id = message.Id,
                        conversation_id = message.ConversationId,
                        sender_id = message.SenderId,
                        content = message.Content,
                        message_type = message.MessageType,
                        created_at = message.CreatedAt
                    });

                    _logger.LogDebug("Message sent to conversation {ConversationId}", conversationId);
                }
                else
                {
                    _logger.LogWarning("No active channel found for conversation {ConversationId}", conversationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task LeaveChannelAsync(Guid conversationId)
        {
            try
            {
                if (_channels.TryGetValue(conversationId, out var channel))
                {
                    channel.Unsubscribe();
                    _channels.Remove(conversationId);
                    _logger.LogInformation("Left chat channel for conversation {ConversationId}", conversationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to leave chat channel for conversation {ConversationId}", conversationId);
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                // Leave all channels
                var channelTasks = _channels.Keys.Select(LeaveChannelAsync);
                await Task.WhenAll(channelTasks);

                // Disconnect from Realtime
                _supabaseClient.Realtime.Disconnect();
                _logger.LogInformation("Disconnected from Supabase Realtime");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from Supabase Realtime");
            }
        }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public Guid ConversationId { get; set; }
        public Message Message { get; set; } = null!;
    }

    public class UserStatusEventArgs : EventArgs
    {
        public Guid ConversationId { get; set; }
        public string? UserJoined { get; set; }
        public string? UserLeft { get; set; }
        public List<string> OnlineUsers { get; set; } = new();
    }
}