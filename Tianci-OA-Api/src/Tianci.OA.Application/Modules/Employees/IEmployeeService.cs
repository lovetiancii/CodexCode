using Tianci.OA.Application.Common;

namespace Tianci.OA.Application.Modules.Employees;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(
        EmployeeQuery query,
        CancellationToken cancellationToken);

    Task<EmployeeDetailDto> GetAsync(
        string id,
        bool includeSensitive,
        CancellationToken cancellationToken);

    Task<EmployeeDto> CreateAsync(
        EmployeeRequest request,
        CancellationToken cancellationToken);

    Task<EmployeeDto> UpdateAsync(
        string id,
        EmployeeRequest request,
        int version,
        CancellationToken cancellationToken);

    Task<EmployeeDto> RegularizeAsync(
        string id,
        RegularizeEmployeeRequest request,
        CancellationToken cancellationToken);

    Task TerminateAsync(
        string id,
        TerminateEmployeeRequest request,
        CancellationToken cancellationToken);

    Task ArchiveAsync(
        string id,
        int version,
        CancellationToken cancellationToken);
}
