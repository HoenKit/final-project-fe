﻿using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

namespace final_project_fe.Utils
{
    public static class JwtHelper
    {
        public static string? GetRoleFromToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            var roleClaim = jsonToken?.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Role ||
                c.Type == "role" ||
                c.Type == "roles" ||
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value; // ASP.NET Identity

            return roleClaim;
        }
    }
}
