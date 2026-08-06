using SqlSugar;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Contracts;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Identity;
using Tianci.OA.Domain.Organization;
using Tianci.OA.Domain.Recruitment;

namespace Tianci.OA.Infrastructure.Authorization;

public sealed class DataScopeService : IDataScopeService
{
    private readonly ISqlSugarClient _database;
    private readonly ICurrentUser _currentUser;

    public DataScopeService(
        ISqlSugarClient database,
        ICurrentUser currentUser)
    {
        _database = database;
        _currentUser = currentUser;
    }

    public async Task<DataScopeContext> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("当前用户未登录");
        var user = await _database.Queryable<SysUser>()
            .Where(entity => entity.Id == userId
                && !entity.IsDeleted
                && entity.Status == UserStatus.Enabled)
            .FirstAsync()
            ?? throw new UnauthorizedAccessException("当前用户不存在或已停用");

        var scopes = await _database
            .Queryable<SysUserRole, SysRole>(
                (userRole, role) => userRole.RoleId == role.Id)
            .Where((userRole, role) =>
                userRole.UserId == userId
                && role.Status == EnabledStatus.Enabled
                && !role.IsDeleted)
            .Select((userRole, role) => role.DataScope)
            .ToListAsync();

        var scope = scopes.Count == 0
            ? DataScope.Self
            : scopes.Min();
        var employeeId = user.EmployeeId;
        var departmentId = user.DepartmentId;

        if (employeeId.HasValue && !departmentId.HasValue)
        {
            var linkedEmployeeId = employeeId.Value;
            departmentId = await _database.Queryable<Employee>()
                .Where(employee => employee.Id == linkedEmployeeId
                    && !employee.IsDeleted)
                .Select(employee => (long?)employee.DepartmentId)
                .FirstAsync();
        }

        var departmentIds = scope == DataScope.DepartmentAndChildren
            ? await GetDepartmentAndChildrenIdsAsync(departmentId)
            : new HashSet<long>();

        return new DataScopeContext(
            scope,
            userId,
            employeeId,
            departmentId,
            departmentIds);
    }

    public async Task EnsureCanAccessDepartmentAsync(
        long departmentId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetCurrentAsync(cancellationToken);
        if (!context.IncludesDepartment(departmentId))
        {
            throw CreateForbiddenException();
        }
    }

    public async Task EnsureCanAccessEmployeeAsync(
        long employeeId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetCurrentAsync(cancellationToken);
        if (context.Scope == DataScope.All)
        {
            return;
        }

        if (context.Scope == DataScope.Self)
        {
            if (context.EmployeeId == employeeId)
            {
                return;
            }

            throw CreateForbiddenException();
        }

        var departmentIds = context.DepartmentIds.ToArray();
        var canAccess = await _database.Queryable<Employee>()
            .Where(employee => employee.Id == employeeId
                && !employee.IsDeleted
                && departmentIds.Contains(employee.DepartmentId))
            .AnyAsync();

        if (!canAccess)
        {
            throw CreateForbiddenException();
        }
    }

    public async Task EnsureCanAccessResumeAsync(
        long resumeId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetCurrentAsync(cancellationToken);
        if (context.Scope == DataScope.All)
        {
            return;
        }

        bool canAccess;
        if (context.Scope == DataScope.Self)
        {
            var ownsResume = await _database.Queryable<Resume>()
                .Where(resume => resume.Id == resumeId
                    && !resume.IsDeleted
                    && resume.OwnerUserId == context.UserId)
                .AnyAsync();
            var hasInterviewTask = await _database.Queryable<InterviewRecord>()
                .Where(interview => interview.ResumeId == resumeId
                    && !interview.IsDeleted
                    && interview.InterviewerUserId == context.UserId)
                .AnyAsync();

            canAccess = ownsResume || hasInterviewTask;
        }
        else
        {
            var departmentIds = context.DepartmentIds.ToArray();
            canAccess = await _database
                .Queryable<Resume, Position>(
                    (resume, position) => resume.AppliedPositionId == position.Id)
                .Where((resume, position) =>
                    resume.Id == resumeId
                    && !resume.IsDeleted
                    && !position.IsDeleted
                    && departmentIds.Contains(position.DepartmentId))
                .AnyAsync();
        }

        if (!canAccess)
        {
            throw CreateForbiddenException();
        }
    }

    public async Task EnsureCanAccessContractAsync(
        long contractId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetCurrentAsync(cancellationToken);
        if (context.Scope == DataScope.All)
        {
            return;
        }

        bool canAccess;
        if (context.Scope == DataScope.Self)
        {
            var employeeId = context.EmployeeId;
            if (!employeeId.HasValue)
            {
                canAccess = false;
            }
            else
            {
                var currentEmployeeId = employeeId.Value;
                canAccess = await _database.Queryable<EmployeeContract>()
                    .Where(contract => contract.Id == contractId
                        && !contract.IsDeleted
                        && contract.EmployeeId == currentEmployeeId)
                    .AnyAsync();
            }
        }
        else
        {
            var departmentIds = context.DepartmentIds.ToArray();
            canAccess = await _database
                .Queryable<EmployeeContract, Employee>(
                    (contract, employee) => contract.EmployeeId == employee.Id)
                .Where((contract, employee) =>
                    contract.Id == contractId
                    && !contract.IsDeleted
                    && !employee.IsDeleted
                    && departmentIds.Contains(employee.DepartmentId))
                .AnyAsync();
        }

        if (!canAccess)
        {
            throw CreateForbiddenException();
        }
    }

    public async Task EnsureCanAccessEntryAsync(
        long entryId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetCurrentAsync(cancellationToken);
        if (context.Scope == DataScope.All)
        {
            return;
        }

        bool canAccess;
        if (context.Scope == DataScope.Self)
        {
            var employeeId = context.EmployeeId;
            if (!employeeId.HasValue)
            {
                canAccess = false;
            }
            else
            {
                var currentEmployeeId = employeeId.Value;
                canAccess = await _database.Queryable<EmployeeEntry>()
                    .Where(entry => entry.Id == entryId
                        && !entry.IsDeleted
                        && entry.EmployeeId == currentEmployeeId)
                    .AnyAsync();
            }
        }
        else
        {
            var departmentIds = context.DepartmentIds.ToArray();
            canAccess = await _database.Queryable<EmployeeEntry>()
                .Where(entry => entry.Id == entryId
                    && !entry.IsDeleted
                    && departmentIds.Contains(entry.DepartmentId))
                .AnyAsync();
        }

        if (!canAccess)
        {
            throw CreateForbiddenException();
        }
    }

    private async Task<HashSet<long>> GetDepartmentAndChildrenIdsAsync(
        long? rootDepartmentId)
    {
        if (!rootDepartmentId.HasValue)
        {
            return [];
        }

        var departments = await _database.Queryable<Department>()
            .Where(department => !department.IsDeleted)
            .ToListAsync();
        var result = new HashSet<long> { rootDepartmentId.Value };
        var pending = new Queue<long>();

        pending.Enqueue(rootDepartmentId.Value);
        while (pending.TryDequeue(out var parentId))
        {
            foreach (var child in departments.Where(
                department => department.ParentId == parentId))
            {
                if (result.Add(child.Id))
                {
                    pending.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    private static ForbiddenException CreateForbiddenException()
    {
        return new ForbiddenException("当前记录超出你的数据权限范围");
    }
}
