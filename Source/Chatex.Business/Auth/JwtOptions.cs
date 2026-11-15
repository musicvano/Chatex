using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Chatex.Business.Auth;

/// <summary>Settings for access and refresh tokens, bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    /// <summary>HMAC-SHA256 key, at least 32 characters. Keep it out of source control in production.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = "";

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), "01:00:00", "365.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>The key both for signing (here) and for validating tokens (in the Api).</summary>
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
