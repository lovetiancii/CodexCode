using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Identity;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Permission("system:role")]
[Route("api/v1/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public RolesController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public Task<IReadOnlyList<RoleDto>> List(
        CancellationToken cancellationToken)
    {
        return _identityService.GetRolesAsync(cancellationToken);
    }

    [HttpPost]
    public Task<RoleDto> Create(
        RoleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return _identityService.CreateRoleAsync(request, cancellationToken);
    }

    [HttpPut("{id}")]
    public Task<RoleDto> Update(
        string id,
        RoleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return _identityService.UpdateRoleAsync(
            id,
            request,
            cancellationToken);
    }

    [HttpGet("{id}/menus")]
    public Task<IReadOnlyList<string>> GetMenus(
        string id,
        CancellationToken cancellationToken)
    {
        return _identityService.GetRoleMenuIdsAsync(id, cancellationToken);
    }

    [HttpPut("{id}/menus")]
    public async Task<IActionResult> AssignMenus(
        string id,
        AssignIdsRequest request,
        CancellationToken cancellationToken)
    {
        await _identityService.AssignRoleMenusAsync(
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
        await _identityService.DeleteRoleAsync(id, cancellationToken);

        return NoContent();
    }
}
