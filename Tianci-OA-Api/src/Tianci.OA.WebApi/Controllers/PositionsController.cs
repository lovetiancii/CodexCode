using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Organization;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Permission("organization:manage")]
[Route("api/v1/positions")]
public sealed class PositionsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public PositionsController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    public Task<IReadOnlyList<PositionDto>> List(
        [FromQuery] string? departmentId,
        CancellationToken cancellationToken)
    {
        return _organizationService.PositionsAsync(
            departmentId,
            cancellationToken);
    }

    [HttpPost]
    public Task<PositionDto> Create(
        PositionRequest request,
        CancellationToken cancellationToken)
    {
        return _organizationService.CreatePositionAsync(
            request,
            cancellationToken);
    }

    [HttpPut("{id}")]
    public Task<PositionDto> Update(
        string id,
        PositionRequest request,
        CancellationToken cancellationToken)
    {
        return _organizationService.UpdatePositionAsync(
            id,
            request,
            cancellationToken);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        await _organizationService.DeletePositionAsync(
            id,
            cancellationToken);

        return NoContent();
    }
}
