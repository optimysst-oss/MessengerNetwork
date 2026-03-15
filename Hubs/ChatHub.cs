using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using MessengerServer.Services;

namespace MessengerServer.Hubs
{
    public class ChatHub : Hub
    {
        // Thread-safe: connectionId -> userId
        private static readonly ConcurrentDictionary<string, int> _connectionUser = new();

        public async Task Register(string token)
        {
            var userId = TokenService.ValidateToken(token);
            if (userId == null) return;

            _connectionUser[Context.ConnectionId] = userId.Value;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _connectionUser.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
