using MessengerServer.Services;

namespace MessengerServer.Middleware
{
    /// <summary>
    /// Reads the Authorization: Bearer <token> header and exposes the validated userId.
    /// Returns 401 if the token is missing or invalid.
    /// </summary>
    public static class AuthHelper
    {
        public static int? GetUserId(HttpRequest request)
        {
            var authHeader = request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            var token = authHeader["Bearer ".Length..].Trim();
            return TokenService.ValidateToken(token);
        }

        public static IResult Unauthorized() =>
            Results.Json(new { ok = false, message = "Нет доступа или токен истёк" },
                         statusCode: 401);
    }
}
