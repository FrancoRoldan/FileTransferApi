using Data.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Core.Security
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user)
        {
            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            SigningCredentials credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            Claim[] claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal ValidateToken(string token, bool validateLifetime = true)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            byte[] key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);

            TokenValidationParameters tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = validateLifetime,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            return tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
        }

        public string RefreshToken(string token)
        {
            try
            {
                ClaimsPrincipal principal = ValidateToken(token, validateLifetime: false);
                string userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
                string email = principal.FindFirst(ClaimTypes.Email)?.Value ?? "";

                User user = new User { Id = int.Parse(userId), Email = email , Nombre = "sd"};
                return GenerateToken(user);
            }
            catch (SecurityTokenException)
            {
                return "";
            }
        }

        public User getUserFromToken(string token) {
            ClaimsPrincipal principal = ValidateToken(token, validateLifetime: false);

            string userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            string email = principal.FindFirst(ClaimTypes.Email)?.Value ?? "";

            User user = new User { Id = int.Parse(userId), Email = email };
            return user;
        }

        public string ExtractTokenFromHeader(string authorizationHeader)
        {
            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return authorizationHeader.Substring("Bearer ".Length).Trim();
        }
    }
}
