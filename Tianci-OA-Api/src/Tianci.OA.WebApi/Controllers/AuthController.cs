using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Identity;

namespace Tianci.OA.WebApi.Controllers;

[ApiController, Route("api/v1/auth")]
public sealed class AuthController(IIdentityService service, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous, HttpPost("login")]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => service.LoginAsync(request, ct);

    [AllowAnonymous, HttpPost("initialize-admin")]
    public async Task<IActionResult> InitializeAdmin(InitializeAdminRequest request, CancellationToken ct)
    {
        var expected = configuration["Initialization:Token"];
        var supplied = Request.Headers["X-Initialization-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(supplied ?? "")))
            return Forbid();
        await service.InitializeAdminAsync(request, ct); return NoContent();
    }
}
