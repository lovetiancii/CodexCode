namespace Tianci.OA.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Tianci.OA";

    public string Audience { get; set; } = "Tianci.OA.Web";

    public string Secret { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 120;
}
