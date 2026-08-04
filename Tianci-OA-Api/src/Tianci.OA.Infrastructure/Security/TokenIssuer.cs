using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.Infrastructure.Security;

public sealed class TokenIssuer(
    IOptions<JwtOptions> options,
    IClock clock) : ITokenIssuer
{
    public TokenResult Issue(
        long userId,
        string username,
        string displayName,
        string securityStamp)
    {
        var value = options.Value;
        var expires = clock.UtcNow.AddMinutes(value.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim("display_name", displayName),
            new Claim("security_stamp", securityStamp),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(value.Secret));
        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            value.Issuer,
            value.Audience,
            claims,
            clock.UtcNow,
            expires,
            credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenResult(accessToken, expires);
    }
}
