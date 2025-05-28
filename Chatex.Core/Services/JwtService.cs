using Chatex.Data.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Chatex.Core.Services
{
    public class JwtService(IConfiguration config)
    {
        public JwtSecurityToken GetToken(User user)
        {
            return new JwtSecurityToken(
                issuer: config["Jwt:Issuer"],
                audience: config["Jwt:Audience"],
                claims: GetClaims(user),
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(config["Jwt:ExpirationTimeInMinutes"])),
                signingCredentials: GetSigningCredentials());
        }

        /// <summary>
        /// Generates signing credentials 
        /// </summary>
        private SigningCredentials GetSigningCredentials()
        {
            var key = Encoding.UTF8.GetBytes(config["Jwt:SecurityKey"]!);
            var secret = new SymmetricSecurityKey(key);
            return new SigningCredentials(secret, SecurityAlgorithms.HmacSha256);
        }

        /// <summary>
        /// Creates the claim base current user id and email
        /// </summary>
        private static List<Claim> GetClaims(User user)
        {
            return new List<Claim> {
                new("id", user.Id.ToString()),
                new("email", user.Email),
            };
        }
    }
}
