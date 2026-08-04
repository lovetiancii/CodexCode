using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Organization;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Permission("organization:manage")]
[Route("api/v1/departments")]
public sealed class DepartmentsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public DepartmentsController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    public Task<IReadOnlyList<DepartmentDto>> List(
        CancellationToken cancellationToken)
    {
        return _organizationService.DepartmentsAsync(cancellationToken);
    }

    [HttpPost]
    public Task<DepartmentDto> Create(
        DepartmentRequest request,
        CancellationToken cancellationToken)
    {
        return _organizationService.CreateDepartmentAsync(
            request,
            cancellationToken);
    }

    [HttpPut("{id}")]
    public Task<DepartmentDto> Update(
        string id,
        DepartmentRequest request,
        CancellationToken cancellationToken)
    {
        return _organizationService.UpdateDepartmentAsync(
            id,
            request,
            cancellationToken);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        await _organizationService.DeleteDepartmentAsync(
            id,
            cancellationToken);

        return NoContent();
    }
}
