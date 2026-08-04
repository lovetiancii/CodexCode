using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.Infrastructure.Security;

public sealed class CurrentUser(
    IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public long? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(value, out var userId)
                ? userId
                : null;
        }
    }

    public string? Name => Principal?.Identity?.Name;

    public string? TraceId => accessor.HttpContext?.TraceIdentifier;
}
