using Tianci.OA.Application.Common;

namespace Tianci.OA.Application.Modules.Contracts;

public interface IContractService
{
    Task<PagedResult<ContractDto>> ListAsync(
        ContractQuery query,
        CancellationToken cancellationToken);

    Task<ContractDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<ContractDto> CreateAsync(
        ContractRequest request,
        CancellationToken cancellationToken);

    Task<ContractDto> UpdateAsync(
        string id,
        ContractRequest request,
        int version,
        CancellationToken cancellationToken);

    Task ActivateAsync(
        string id,
        ContractActionRequest request,
        CancellationToken cancellationToken);

    Task TerminateAsync(
        string id,
        ContractActionRequest request,
        CancellationToken cancellationToken);

    Task<ContractDto> RenewAsync(
        string id,
        ContractRequest request,
        int version,
        CancellationToken cancellationToken);

    Task ArchiveAsync(
        string id,
        ContractActionRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContractDto>> ExpiringAsync(
        int? withinDays,
        CancellationToken cancellationToken);
}
