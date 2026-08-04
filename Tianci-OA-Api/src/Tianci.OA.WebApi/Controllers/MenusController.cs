using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Identity;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Permission("system:menu")]
[Route("api/v1/menus")]
public sealed class MenusController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public MenusController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public Task<IReadOnlyList<MenuDto>> List(
        CancellationToken cancellationToken)
    {
        return _identityService.GetMenusAsync(cancellationToken);
    }

    [HttpPost]
    public Task<MenuDto> Create(
        MenuUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return _identityService.CreateMenuAsync(request, cancellationToken);
    }

    [HttpPut("{id}")]
    public Task<MenuDto> Update(
        string id,
        MenuUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return _identityService.UpdateMenuAsync(
            id,
            request,
            cancellationToken);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        await _identityService.DeleteMenuAsync(id, cancellationToken);

        return NoContent();
    }
}
