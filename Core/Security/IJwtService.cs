using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Core.Security
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        ClaimsPrincipal ValidateToken(string token, bool validateLifetime = true);
        string RefreshToken(string token);
        User getUserFromToken(string token);
        string ExtractTokenFromHeader(string authorizationHeader);
    }
}
