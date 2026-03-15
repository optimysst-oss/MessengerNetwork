using MessengerServer.Data;
using MessengerServer.Hubs;
using MessengerServer.Middleware;
using MessengerServer.Models;
using MessengerServer.Repositories;
using MessengerServer.Services;
using Microsoft.AspNetCore.SignalR;

Database.Init();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();
app.MapHub<ChatHub>("/chathub");

// ════════════════════════════════════════════
//  AUTH  (no token required)
// ════════════════════════════════════════════

app.MapPost("/auth/register", (RegisterDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Username) || dto.Username.Trim().Length < 3)
        return Results.Ok(new AuthResponse(false, "Логин должен быть не менее 3 символов", 0, "", "", ""));

    if (string.IsNullOrWhiteSpace(dto.DisplayName))
        return Results.Ok(new AuthResponse(false, "Укажите отображаемое имя", 0, "", "", ""));

    if (dto.Password == null || dto.Password.Length < 6)
        return Results.Ok(new AuthResponse(false, "Пароль должен быть не менее 6 символов", 0, "", "", ""));

    var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
    if (!UserRepository.TryCreate(dto.Username, dto.DisplayName, hash, out int userId))
        return Results.Ok(new AuthResponse(false, "Это имя пользователя уже занято", 0, "", "", ""));

    var token = TokenService.GenerateToken(userId);
    return Results.Ok(new AuthResponse(true, "Регистрация успешна!", userId, token,
                                       dto.DisplayName.Trim(), dto.Username.Trim().ToLower()));
});

app.MapPost("/auth/login", (LoginDto dto) =>
{
    var user = UserRepository.FindByUsername(dto.Username);
    if (user == null)
        return Results.Ok(new AuthResponse(false, "Пользователь не найден", 0, "", "", ""));

    if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        return Results.Ok(new AuthResponse(false, "Неверный пароль", 0, "", "", ""));

    UserRepository.SetOnline(user.Id, true);
    var token = TokenService.GenerateToken(user.Id);
    return Results.Ok(new AuthResponse(true, "Вход выполнен!", user.Id, token,
                                       user.DisplayName, user.Username));
});

app.MapPost("/auth/logout", (HttpRequest req) =>
{
    var userId = AuthHelper.GetUserId(req);
    if (userId == null) return AuthHelper.Unauthorized();

    UserRepository.SetOnline(userId.Value, false);
    return Results.Ok(new ApiResponse(true, "Выход выполнен"));
});

// ════════════════════════════════════════════
//  USERS / SEARCH  (requires auth)
// ════════════════════════════════════════════

app.MapGet("/users/search", (HttpRequest req, string query) =>
{
    var userId = AuthHelper.GetUserId(req);
    if (userId == null) return AuthHelper.Unauthorized();

    if (string.IsNullOrWhiteSpace(query))
        return Results.Ok(new List<object>());

    return Results.Ok(UserRepository.Search(query, userId.Value));
});

// ════════════════════════════════════════════
//  MESSAGES  (requires auth)
// ════════════════════════════════════════════

app.MapPost("/messages/send", async (HttpRequest req, SendMessageDto dto, IHubContext<ChatHub> hub) =>
{
    var senderId = AuthHelper.GetUserId(req);
    if (senderId == null) return AuthHelper.Unauthorized();

    var senderName = UserRepository.GetDisplayName(senderId.Value) ?? "";
    var (ok, error) = MessageRepository.Send(senderId.Value, dto.ReceiverId, dto.Content);
    if (!ok) return Results.Ok(new ApiResponse(false, error));

    var msgData = new
    {
        SenderId = senderId.Value,
        SenderName = senderName,
        Content = dto.Content.Trim(),
        SentAt = DateTime.UtcNow
    };
    await hub.Clients.Group($"user_{dto.ReceiverId}").SendAsync("ReceiveMessage", msgData);

    return Results.Ok(new ApiResponse(true, "Отправлено"));
});

app.MapGet("/messages", (HttpRequest req, int friendId) =>
{
    var userId = AuthHelper.GetUserId(req);
    if (userId == null) return AuthHelper.Unauthorized();

    return Results.Ok(MessageRepository.GetConversation(userId.Value, friendId));
});

app.MapGet("/messages/unread", (HttpRequest req, int friendId) =>
{
    var userId = AuthHelper.GetUserId(req);
    if (userId == null) return Results.Ok(0);

    return Results.Ok(MessageRepository.GetUnreadCount(userId.Value, friendId));
});

// ════════════════════════════════════════════
//  FRIENDS  (requires auth)
// ════════════════════════════════════════════

app.MapGet("/friends", (HttpRequest req) =>
{
    var userId = AuthHelper.GetUserId(req);
    if (userId == null) return AuthHelper.Unauthorized();

    return Results.Ok(FriendRepository.GetFriends(userId.Value));
});

app.MapPost("/friends/request", async (HttpRequest req, SendFriendRequestDto dto, IHubContext<ChatHub> hub) =>
{
    var senderId = AuthHelper.GetUserId(req);
    if (senderId == null) return AuthHelper.Unauthorized();

    var (ok, error) = FriendRepository.SendRequest(senderId.Value, dto.ReceiverId);
    if (!ok) return Results.Ok(new ApiResponse(false, error));

    await hub.Clients.Group($"user_{dto.ReceiverId}")
             .SendAsync("FriendRequest", new { FromId = senderId.Value });

    return Results.Ok(new ApiResponse(true, "Заявка отправлена!"));
});

app.MapGet("/friends/requests", (HttpRequest req) =>
{
    var userId = AuthHelper.GetUserId(req);
    if (userId == null) return AuthHelper.Unauthorized();

    return Results.Ok(FriendRepository.GetPendingRequests(userId.Value));
});

app.MapPost("/friends/respond", (HttpRequest req, RespondFriendDto dto) =>
{
    var userId = AuthHelper.GetUserId(req);
    if (userId == null) return AuthHelper.Unauthorized();

    var (ok, error, _) = FriendRepository.Respond(dto.RequestId, userId.Value, dto.Accept);
    return Results.Ok(new ApiResponse(ok, ok ? (dto.Accept ? "Принято" : "Отклонено") : error));
});

app.MapGet("/friends/areFriends", (HttpRequest req, int friendId) =>
{
    var userId = AuthHelper.GetUserId(req);
    if (userId == null) return AuthHelper.Unauthorized();

    return Results.Ok(FriendRepository.AreFriends(userId.Value, friendId));
});

Console.WriteLine("=== Messenger Server запущен на http://0.0.0.0:5000 ===");
Console.WriteLine("Нажмите Ctrl+C для остановки.");
app.Run();
