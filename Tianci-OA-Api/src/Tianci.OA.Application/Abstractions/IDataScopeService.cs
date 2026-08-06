using Tianci.OA.Domain.Common;

namespace Tianci.OA.Application.Abstractions;

public sealed record DataScopeContext(
    DataScope Scope,
    long UserId,
    long? EmployeeId,
    long? DepartmentId,
    IReadOnlySet<long> DepartmentIds)
{
    public bool IncludesDepartment(long departmentId)
    {
        return Scope == DataScope.All
            || Scope == DataScope.DepartmentAndChildren
                && DepartmentIds.Contains(departmentId);
    }
}

public interface IDataScopeService
{
    Task<DataScopeContext> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task EnsureCanAccessDepartmentAsync(
        long departmentId,
        CancellationToken cancellationToken = default);

    Task EnsureCanAccessEmployeeAsync(
        long employeeId,
        CancellationToken cancellationToken = default);

    Task EnsureCanAccessResumeAsync(
        long resumeId,
        CancellationToken cancellationToken = default);

    Task EnsureCanAccessContractAsync(
        long contractId,
        CancellationToken cancellationToken = default);

    Task EnsureCanAccessEntryAsync(
        long entryId,
        CancellationToken cancellationToken = default);
}
