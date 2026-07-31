using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Contracts;
using Tianci.OA.Domain.Common;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController, Authorize, Route("api/v1/contracts")]
public sealed class ContractsController(IContractService service) : ControllerBase
{
    [HttpGet, Permission("contract:view")]
    public Task<PagedResult<ContractDto>> List([FromQuery] string? keyword, [FromQuery] string? employeeId, [FromQuery] ContractStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync(new(keyword, employeeId, status, pageNumber, pageSize), ct);
    [HttpGet("{id}"), Permission("contract:view")] public Task<ContractDto> Get(string id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpGet("expiring"), Permission("contract:view")] public Task<IReadOnlyList<ContractDto>> Expiring([FromQuery] int? withinDays, CancellationToken ct) => service.ExpiringAsync(withinDays, ct);
    [HttpPost, Permission("contract:manage")] public Task<ContractDto> Create(ContractRequest request, CancellationToken ct) => service.CreateAsync(request, ct);
    [HttpPut("{id}"), Permission("contract:manage")] public Task<ContractDto> Update(string id, ContractRequest request, [FromQuery] int version, CancellationToken ct) => service.UpdateAsync(id, request, version, ct);
    [HttpPost("{id}/activate"), Permission("contract:manage")] public async Task<IActionResult> Activate(string id, ContractActionRequest request, CancellationToken ct) { await service.ActivateAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id}/terminate"), Permission("contract:manage")] public async Task<IActionResult> Terminate(string id, ContractActionRequest request, CancellationToken ct) { await service.TerminateAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id}/renew"), Permission("contract:manage")] public Task<ContractDto> Renew(string id, ContractRequest request, [FromQuery] int version, CancellationToken ct) => service.RenewAsync(id, request, version, ct);
    [HttpPost("{id}/archive"), Permission("contract:manage")] public async Task<IActionResult> Archive(string id, ContractActionRequest request, CancellationToken ct) { await service.ArchiveAsync(id, request, ct); return NoContent(); }
}
