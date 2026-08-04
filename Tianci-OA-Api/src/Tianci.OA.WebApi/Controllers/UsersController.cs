using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Identity;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Permission("system:user")]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public UsersController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public Task<PagedResult<UserDto>> List(
        [FromQuery] PageRequest page,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        return _identityService.GetUsersAsync(
            page,
            keyword,
            cancellationToken);
    }

    [HttpGet("{id}")]
    public Task<UserDto> Get(
        string id,
        CancellationToken cancellationToken)
    {
        return _identityService.GetUserAsync(id, cancellationToken);
    }

    [HttpPost]
    public Task<UserDto> Create(
        UserCreateRequest request,
        CancellationToken cancellationToken)
    {
        return _identityService.CreateUserAsync(request, cancellationToken);
    }

    [HttpPut("{id}")]
    public Task<UserDto> Update(
        string id,
        UserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        return _identityService.UpdateUserAsync(
            id,
            request,
            cancellationToken);
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        string id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _identityService.ResetPasswordAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpGet("{id}/roles")]
    public Task<IReadOnlyList<string>> GetRoles(
        string id,
        CancellationToken cancellationToken)
    {
        return _identityService.GetUserRoleIdsAsync(id, cancellationToken);
    }

    [HttpPut("{id}/roles")]
    public async Task<IActionResult> AssignRoles(
        string id,
        AssignIdsRequest request,
        CancellationToken cancellationToken)
    {
        await _identityService.AssignUserRolesAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        await _identityService.DeleteUserAsync(id, cancellationToken);

        return NoContent();
    }
}
