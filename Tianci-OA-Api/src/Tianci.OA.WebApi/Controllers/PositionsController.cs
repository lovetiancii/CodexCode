using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Organization;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/positions")]
public sealed class PositionsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public PositionsController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    [Permission("organization:view")]
    public Task<IReadOnlyList<PositionDto>> List(
        [FromQuery] string? departmentId,
        CancellationToken cancellationToken)
    {
        return _organizationService.PositionsAsync(
            departmentId,
            cancellationToken);
    }

    [HttpPost]
    [Permission("organization:manage")]
    public Task<PositionDto> Create(
        PositionRequest request,
        CancellationToken cancellationToken)
    {
        return _organizationService.CreatePositionAsync(
            request,
            cancellationToken);
    }

    [HttpPut("{id}")]
    [Permission("organization:manage")]
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
    [Permission("organization:manage")]
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
