using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Contracts;
using Tianci.OA.Domain.Common;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/contracts")]
public sealed class ContractsController : ControllerBase
{
    private readonly IContractService _contractService;

    public ContractsController(IContractService contractService)
    {
        _contractService = contractService;
    }

    [HttpGet]
    [Permission("contract:view")]
    public Task<PagedResult<ContractDto>> List(
        [FromQuery] string? keyword,
        [FromQuery] string? employeeId,
        [FromQuery] ContractStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ContractQuery(
            keyword,
            employeeId,
            status,
            pageNumber,
            pageSize);

        return _contractService.ListAsync(query, cancellationToken);
    }

    [HttpGet("{id}")]
    [Permission("contract:view")]
    public Task<ContractDto> Get(
        string id,
        CancellationToken cancellationToken)
    {
        return _contractService.GetAsync(id, cancellationToken);
    }

    [HttpGet("expiring")]
    [Permission("contract:view")]
    public Task<IReadOnlyList<ContractDto>> Expiring(
        [FromQuery] int? withinDays,
        CancellationToken cancellationToken)
    {
        return _contractService.ExpiringAsync(withinDays, cancellationToken);
    }

    [HttpPost]
    [Permission("contract:manage")]
    public Task<ContractDto> Create(
        ContractRequest request,
        CancellationToken cancellationToken)
    {
        return _contractService.CreateAsync(request, cancellationToken);
    }

    [HttpPut("{id}")]
    [Permission("contract:manage")]
    public Task<ContractDto> Update(
        string id,
        ContractRequest request,
        [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        return _contractService.UpdateAsync(
            id,
            request,
            version,
            cancellationToken);
    }

    [HttpPost("{id}/activate")]
    [Permission("contract:manage")]
    public async Task<IActionResult> Activate(
        string id,
        ContractActionRequest request,
        CancellationToken cancellationToken)
    {
        await _contractService.ActivateAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id}/terminate")]
    [Permission("contract:manage")]
    public async Task<IActionResult> Terminate(
        string id,
        ContractActionRequest request,
        CancellationToken cancellationToken)
    {
        await _contractService.TerminateAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id}/renew")]
    [Permission("contract:manage")]
    public Task<ContractDto> Renew(
        string id,
        ContractRequest request,
        [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        return _contractService.RenewAsync(
            id,
            request,
            version,
            cancellationToken);
    }

    [HttpPost("{id}/archive")]
    [Permission("contract:manage")]
    public async Task<IActionResult> Archive(
        string id,
        ContractActionRequest request,
        CancellationToken cancellationToken)
    {
        await _contractService.ArchiveAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }
}
