using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.WebApi.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAttribute : TypeFilterAttribute
{
    public PermissionAttribute(string permission) : base(typeof(PermissionFilter))
    {
        Arguments = [permission];
    }
}

public sealed class PermissionFilter(IPermissionService permissions, string permission) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        var claim = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(claim, out var userId) || !await permissions.HasPermissionAsync(userId, permission))
        {
            context.Result = new ForbidResult();
        }
    }
}
