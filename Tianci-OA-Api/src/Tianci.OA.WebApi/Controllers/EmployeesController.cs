using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Modules.Employees;
using Tianci.OA.Domain.Common;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController, Authorize, Route("api/v1/employees")]
public sealed class EmployeesController(IEmployeeService service, IPermissionService permissions, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet, Permission("employee:view")]
    public Task<PagedResult<EmployeeDto>> List([FromQuery] string? keyword, [FromQuery] string? departmentId, [FromQuery] string? positionId, [FromQuery] EmployeeStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync(new(keyword, departmentId, positionId, status, pageNumber, pageSize), ct);
    [HttpGet("{id}"), Permission("employee:view")]
    public async Task<EmployeeDetailDto> Get(string id, [FromQuery] bool includeSensitive, CancellationToken ct)
    {
        var allowed = includeSensitive && currentUser.UserId.HasValue && await permissions.HasPermissionAsync(currentUser.UserId.Value, "employee:sensitive");
        return await service.GetAsync(id, allowed, ct);
    }
    [HttpPost, Permission("employee:create")] public Task<EmployeeDto> Create(EmployeeRequest request, CancellationToken ct) => service.CreateAsync(request, ct);
    [HttpPut("{id}"), Permission("employee:edit")] public Task<EmployeeDto> Update(string id, EmployeeRequest request, [FromQuery] int version, CancellationToken ct) => service.UpdateAsync(id, request, version, ct);
    [HttpPost("{id}/regularize"), Permission("employee:edit")] public Task<EmployeeDto> Regularize(string id, RegularizeEmployeeRequest request, CancellationToken ct) => service.RegularizeAsync(id, request, ct);
    [HttpPost("{id}/terminate"), Permission("employee:terminate")] public async Task<IActionResult> Terminate(string id, TerminateEmployeeRequest request, CancellationToken ct) { await service.TerminateAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id}/archive"), Permission("employee:archive")] public async Task<IActionResult> Archive(string id, [FromQuery] int version, CancellationToken ct) { await service.ArchiveAsync(id, version, ct); return NoContent(); }
}
