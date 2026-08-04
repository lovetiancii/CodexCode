using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Identity;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly IConfiguration _configuration;

    public AuthController(
        IIdentityService identityService,
        IConfiguration configuration)
    {
        _identityService = identityService;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        return _identityService.LoginAsync(request, cancellationToken);
    }

    [AllowAnonymous]
    [HttpPost("initialize-admin")]
    public async Task<IActionResult> InitializeAdmin(
        InitializeAdminRequest request,
        CancellationToken cancellationToken)
    {
        var expectedToken = _configuration["Initialization:Token"];
        var suppliedToken = Request.Headers["X-Initialization-Token"].FirstOrDefault();

        if (!IsValidInitializationToken(expectedToken, suppliedToken))
        {
            return Forbid();
        }

        await _identityService.InitializeAdminAsync(request, cancellationToken);

        return NoContent();
    }

    private static bool IsValidInitializationToken(
        string? expectedToken,
        string? suppliedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedToken),
            Encoding.UTF8.GetBytes(suppliedToken ?? string.Empty));
    }
}
