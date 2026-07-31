using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Identity;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController, Authorize, Permission("system:user"), Route("api/v1/users")]
public sealed class UsersController(IIdentityService service) : ControllerBase
{
    [HttpGet] public Task<PagedResult<UserDto>> List([FromQuery] PageRequest page, [FromQuery] string? keyword, CancellationToken ct) => service.GetUsersAsync(page, keyword, ct);
    [HttpGet("{id}")] public Task<UserDto> Get(string id, CancellationToken ct) => service.GetUserAsync(id, ct);
    [HttpPost] public Task<UserDto> Create(UserCreateRequest request, CancellationToken ct) => service.CreateUserAsync(request, ct);
    [HttpPut("{id}")] public Task<UserDto> Update(string id, UserUpdateRequest request, CancellationToken ct) => service.UpdateUserAsync(id, request, ct);
    [HttpPost("{id}/reset-password")] public async Task<IActionResult> Reset(string id, ResetPasswordRequest request, CancellationToken ct) { await service.ResetPasswordAsync(id, request, ct); return NoContent(); }
    [HttpPut("{id}/roles")] public async Task<IActionResult> Roles(string id, AssignIdsRequest request, CancellationToken ct) { await service.AssignUserRolesAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(string id, CancellationToken ct) { await service.DeleteUserAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize, Permission("system:role"), Route("api/v1/roles")]
public sealed class RolesController(IIdentityService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<RoleDto>> List(CancellationToken ct) => service.GetRolesAsync(ct);
    [HttpPost] public Task<RoleDto> Create(RoleUpsertRequest request, CancellationToken ct) => service.CreateRoleAsync(request, ct);
    [HttpPut("{id}")] public Task<RoleDto> Update(string id, RoleUpsertRequest request, CancellationToken ct) => service.UpdateRoleAsync(id, request, ct);
    [HttpPut("{id}/menus")] public async Task<IActionResult> Menus(string id, AssignIdsRequest request, CancellationToken ct) { await service.AssignRoleMenusAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(string id, CancellationToken ct) { await service.DeleteRoleAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize, Permission("system:menu"), Route("api/v1/menus")]
public sealed class MenusController(IIdentityService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<MenuDto>> List(CancellationToken ct) => service.GetMenusAsync(ct);
    [HttpPost] public Task<MenuDto> Create(MenuUpsertRequest request, CancellationToken ct) => service.CreateMenuAsync(request, ct);
    [HttpPut("{id}")] public Task<MenuDto> Update(string id, MenuUpsertRequest request, CancellationToken ct) => service.UpdateMenuAsync(id, request, ct);
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(string id, CancellationToken ct) { await service.DeleteMenuAsync(id, ct); return NoContent(); }
}
