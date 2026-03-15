namespace MessengerServer.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsOnline { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public string SenderName { get; set; } = "";
    }

    public class FriendRequest
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; }
        public string SenderName { get; set; } = "";
    }

    // ── Request DTOs ─────────────────────────────────────────────────────────
    public record RegisterDto(string Username, string DisplayName, string Password);
    public record LoginDto(string Username, string Password);

    // Token is now sent via Authorization header — NOT in body for these
    public record SendMessageDto(int ReceiverId, string Content);
    public record SendFriendRequestDto(int ReceiverId);
    public record RespondFriendDto(int RequestId, bool Accept);
    public record SetOfflineDto(); // body is empty; userId comes from token

    // ── Response DTOs ────────────────────────────────────────────────────────
    public record AuthResponse(bool Ok, string Message, int UserId, string Token,
                               string DisplayName, string Username);
    public record ApiResponse(bool Ok, string Message);
}
