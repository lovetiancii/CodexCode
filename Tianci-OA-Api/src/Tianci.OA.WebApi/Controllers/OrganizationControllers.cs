using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Organization;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController, Authorize, Permission("organization:manage"), Route("api/v1/departments")]
public sealed class DepartmentsController(IOrganizationService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<DepartmentDto>> List(CancellationToken ct) => service.DepartmentsAsync(ct);
    [HttpPost] public Task<DepartmentDto> Create(DepartmentRequest request, CancellationToken ct) => service.CreateDepartmentAsync(request, ct);
    [HttpPut("{id}")] public Task<DepartmentDto> Update(string id, DepartmentRequest request, CancellationToken ct) => service.UpdateDepartmentAsync(id, request, ct);
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(string id, CancellationToken ct) { await service.DeleteDepartmentAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize, Permission("organization:manage"), Route("api/v1/positions")]
public sealed class PositionsController(IOrganizationService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<PositionDto>> List([FromQuery] string? departmentId, CancellationToken ct) => service.PositionsAsync(departmentId, ct);
    [HttpPost] public Task<PositionDto> Create(PositionRequest request, CancellationToken ct) => service.CreatePositionAsync(request, ct);
    [HttpPut("{id}")] public Task<PositionDto> Update(string id, PositionRequest request, CancellationToken ct) => service.UpdatePositionAsync(id, request, ct);
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(string id, CancellationToken ct) { await service.DeletePositionAsync(id, ct); return NoContent(); }
}
