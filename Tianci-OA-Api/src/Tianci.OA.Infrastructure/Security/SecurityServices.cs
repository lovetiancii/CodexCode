using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Domain.Identity;

namespace Tianci.OA.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Tianci.OA";
    public string Audience { get; set; } = "Tianci.OA.Web";
    public string Secret { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 120;
}

public sealed class TokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    public TokenResult Issue(long userId, string username, string displayName, string securityStamp)
    {
        var value = options.Value; var expires = clock.UtcNow.AddMinutes(value.ExpirationMinutes);
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Name, username), new Claim("display_name", displayName), new Claim("security_stamp", securityStamp), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")) };
        var token = new JwtSecurityToken(value.Issuer, value.Audience, claims, clock.UtcNow, expires, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(value.Secret)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<SysUser> _hasher = new();
    public string Hash(string username, string password) => _hasher.HashPassword(new SysUser { Username = username }, password);
    public bool Verify(string username, string hash, string password) => _hasher.VerifyHashedPassword(new SysUser { Username = username }, hash, password) != PasswordVerificationResult.Failed;
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public long? UserId => long.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;
    public string? Name => Principal?.Identity?.Name;
    public string? TraceId => accessor.HttpContext?.TraceIdentifier;
}

public sealed class SensitiveDataProtector(IDataProtectionProvider provider) : ISensitiveDataProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Tianci.OA.SensitiveData.v1");
    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
