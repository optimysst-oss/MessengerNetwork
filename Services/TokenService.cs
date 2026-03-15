using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MessengerServer.Services
{
    public static class TokenService
    {
        // In production, load from environment variable / config
        private static readonly string Secret =
            Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? "messenger-super-secret-key-change-in-prod-32chars";

        private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

        public static string GenerateToken(int userId)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "MessengerServer",
                audience: "MessengerClient",
                claims: new[] { new Claim("uid", userId.ToString()) },
                expires: DateTime.UtcNow.Add(TokenLifetime),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Validates token and returns userId on success, null on failure.
        /// </summary>
        public static int? ValidateToken(string token)
        {
            try
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = "MessengerServer",
                    ValidateAudience = true,
                    ValidAudience = "MessengerClient",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out _);

                var claim = principal.FindFirst("uid");
                if (claim == null) return null;
                return int.Parse(claim.Value);
            }
            catch
            {
                return null;
            }
        }
    }
}
