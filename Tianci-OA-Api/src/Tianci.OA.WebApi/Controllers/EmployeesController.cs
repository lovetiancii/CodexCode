using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Employees;
using Tianci.OA.Domain.Common;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees")]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUser _currentUser;

    public EmployeesController(
        IEmployeeService employeeService,
        IPermissionService permissionService,
        ICurrentUser currentUser)
    {
        _employeeService = employeeService;
        _permissionService = permissionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Permission("employee:view")]
    public Task<PagedResult<EmployeeDto>> List(
        [FromQuery] string? keyword,
        [FromQuery] string? departmentId,
        [FromQuery] string? positionId,
        [FromQuery] EmployeeStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new EmployeeQuery(
            keyword,
            departmentId,
            positionId,
            status,
            pageNumber,
            pageSize);

        return _employeeService.ListAsync(query, cancellationToken);
    }

    [HttpGet("{id}")]
    [Permission("employee:view")]
    public async Task<EmployeeDetailDto> Get(
        string id,
        [FromQuery] bool includeSensitive,
        CancellationToken cancellationToken)
    {
        var canViewSensitive = includeSensitive
            && _currentUser.UserId.HasValue
            && await _permissionService.HasPermissionAsync(
                _currentUser.UserId.Value,
                "employee:sensitive");

        return await _employeeService.GetAsync(
            id,
            canViewSensitive,
            cancellationToken);
    }

    [HttpPost]
    [Permission("employee:create")]
    public Task<EmployeeDto> Create(
        EmployeeRequest request,
        CancellationToken cancellationToken)
    {
        return _employeeService.CreateAsync(request, cancellationToken);
    }

    [HttpPut("{id}")]
    [Permission("employee:edit")]
    public Task<EmployeeDto> Update(
        string id,
        EmployeeRequest request,
        [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        return _employeeService.UpdateAsync(
            id,
            request,
            version,
            cancellationToken);
    }

    [HttpPost("{id}/regularize")]
    [Permission("employee:edit")]
    public Task<EmployeeDto> Regularize(
        string id,
        RegularizeEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        return _employeeService.RegularizeAsync(
            id,
            request,
            cancellationToken);
    }

    [HttpPost("{id}/terminate")]
    [Permission("employee:terminate")]
    public async Task<IActionResult> Terminate(
        string id,
        TerminateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        await _employeeService.TerminateAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id}/archive")]
    [Permission("employee:archive")]
    public async Task<IActionResult> Archive(
        string id,
        [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        await _employeeService.ArchiveAsync(
            id,
            version,
            cancellationToken);

        return NoContent();
    }
}
