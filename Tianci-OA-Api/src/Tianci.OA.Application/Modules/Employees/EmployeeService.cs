using System.Linq.Expressions;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Organization;

namespace Tianci.OA.Application.Modules.Employees;

public sealed class EmployeeService(
    IRepository<Employee> employees,
    IRepository<Department> departments,
    IRepository<Position> positions,
    ISensitiveDataProtector protector,
    IDataScopeService dataScope,
    ISnowflakeIdGenerator ids,
    IClock clock,
    ICurrentUser user) : IEmployeeService
{
    public async Task<PagedResult<EmployeeDto>> ListAsync(EmployeeQuery q, CancellationToken ct)
    {
        var keyword = q.Keyword?.Trim() ?? "";
        var did = IdParser.ParseNullable(q.DepartmentId, "departmentId");
        var pid = IdParser.ParseNullable(q.PositionId, "positionId");
        var page = new PageRequest(q.PageNumber, q.PageSize);
        Expression<Func<Employee, bool>> predicate = employee =>
            !employee.IsDeleted
            && (keyword == ""
                || employee.Name.Contains(keyword)
                || employee.EmployeeNo.Contains(keyword)
                || employee.Phone.Contains(keyword));

        var scope = await dataScope.GetCurrentAsync(ct);
        if (scope.Scope == DataScope.DepartmentAndChildren)
        {
            var departmentIds = scope.DepartmentIds.ToArray();
            predicate = predicate.And(employee =>
                departmentIds.Contains(employee.DepartmentId));
        }
        else if (scope.Scope == DataScope.Self)
        {
            var employeeId = scope.EmployeeId ?? -1;
            predicate = predicate.And(employee => employee.Id == employeeId);
        }

        if (did.HasValue)
        {
            var departmentId = did.Value;
            predicate = predicate.And(x => x.DepartmentId == departmentId);
        }
        if (pid.HasValue)
        {
            var positionId = pid.Value;
            predicate = predicate.And(x => x.PositionId == positionId);
        }
        if (q.Status.HasValue)
        {
            var status = q.Status.Value;
            predicate = predicate.And(x => x.Status == status);
        }
        var (items, total) = await employees.PageAsync(
            predicate,
            page.SafePageNumber,
            page.SafePageSize,
            employee => employee.UpdatedAt,
            true,
            ct);

        return new PagedResult<EmployeeDto>(
            [.. items.Select(ToDto)],
            page.SafePageNumber,
            page.SafePageSize,
            total);
    }
    public async Task<EmployeeDetailDto> GetAsync(string id, bool includeSensitive, CancellationToken ct)
    {
        var e = await GetRequired(id, ct);
        var idCard = includeSensitive && e.IdCardCiphertext != null
            ? protector.Unprotect(e.IdCardCiphertext)
            : Mask(e.IdCardCiphertext);
        var monthlySalary = includeSensitive && e.MonthlySalaryCiphertext != null
            ? protector.Unprotect(e.MonthlySalaryCiphertext)
            : e.MonthlySalaryCiphertext == null
                ? null
                : "******";

        return new EmployeeDetailDto(
            ToDto(e),
            idCard,
            monthlySalary);
    }
    public async Task<EmployeeDto> CreateAsync(EmployeeRequest r, CancellationToken ct)
    {
        await ValidateAsync(r, null, ct);
        var e = new Employee
        {
            Status = r.ProbationMonths > 0
                ? EmployeeStatus.Probation
                : EmployeeStatus.Active
        };
        Apply(e, r);
        EntityAudit.Create(e, ids, clock, user);
        await employees.InsertAsync(e, ct);
        return ToDto(e);
    }
    public async Task<EmployeeDto> UpdateAsync(string id, EmployeeRequest r, int version, CancellationToken ct)
    {
        var e = await GetRequired(id, ct);
        if (e.Status is EmployeeStatus.Terminated or EmployeeStatus.Archived)
        {
            throw new ConflictException("离职或归档员工不可编辑", "INVALID_STATE_TRANSITION");
        }

        if (e.Version != version)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        await ValidateAsync(r, e.Id, ct);
        Apply(e, r);
        var old = e.Version;
        e.Version++;
        EntityAudit.Update(e, clock, user);
        if (await employees.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        return ToDto(e);
    }
    public async Task<EmployeeDto> RegularizeAsync(string id, RegularizeEmployeeRequest r, CancellationToken ct)
    {
        var e = await GetRequired(id, ct);
        if (e.Status != EmployeeStatus.Probation)
        {
            throw new ConflictException("仅试用员工可办理转正", "INVALID_STATE_TRANSITION");
        }

        if (r.RegularDate.Date < e.EntryDate.Date || r.RegularDate.Date > clock.UtcNow.Date)
        {
            throw new BusinessException("转正日期必须介于入职日期与今天之间");
        }

        if (e.Version != r.Version)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        var old = e.Version;
        e.Status = EmployeeStatus.Active;
        e.RegularDate = r.RegularDate.Date;
        e.Version++;
        EntityAudit.Update(e, clock, user);
        if (await employees.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        return ToDto(e);
    }
    public async Task TerminateAsync(string id, TerminateEmployeeRequest r, CancellationToken ct)
    {
        var e = await GetRequired(id, ct);
        if (e.Status is not (EmployeeStatus.Probation or EmployeeStatus.Active))
        {
            throw new ConflictException("仅试用或在职员工可办理离职", "INVALID_STATE_TRANSITION");
        }

        if (r.TerminationDate.Date < e.EntryDate.Date || r.TerminationDate.Date > clock.UtcNow.Date)
        {
            throw new BusinessException("离职日期必须介于入职日期与今天之间");
        }

        if (e.Version != r.Version)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        var old = e.Version;
        e.Status = EmployeeStatus.Terminated;
        e.TerminationDate = r.TerminationDate.Date;
        e.TerminationReason = r.Reason.Trim();
        e.Version++;
        EntityAudit.Update(e, clock, user);
        if (await employees.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0)
        {
            throw new ConflictException("数据已被其他用户修改");
        }
    }
    public async Task ArchiveAsync(string id, int version, CancellationToken ct)
    {
        var e = await GetRequired(id, ct);
        if (e.Status != EmployeeStatus.Terminated)
        {
            throw new ConflictException("仅离职员工可归档", "INVALID_STATE_TRANSITION");
        }

        if (e.Version != version)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        var old = e.Version;
        e.Status = EmployeeStatus.Archived;
        e.Version++;
        EntityAudit.Update(e, clock, user);
        if (await employees.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0)
        {
            throw new ConflictException("数据已被其他用户修改");
        }
    }
    private async Task ValidateAsync(EmployeeRequest r, long? currentId, CancellationToken ct)
    {
        var did = IdParser.Parse(r.DepartmentId, "departmentId");
        var pid = IdParser.Parse(r.PositionId, "positionId");
        await dataScope.EnsureCanAccessDepartmentAsync(did, ct);

        if (!await departments.ExistsAsync(x => x.Id == did && !x.IsDeleted && x.Status == EnabledStatus.Enabled, ct))
        {
            throw new NotFoundException("部门不存在或未启用");
        }

        if (!await positions.ExistsAsync(
            position => position.Id == pid
                && position.DepartmentId == did
                && !position.IsDeleted
                && position.Status == EnabledStatus.Enabled,
            ct))
        {
            throw new NotFoundException("岗位不存在、未启用或不属于该部门");
        }

        Expression<Func<Employee, bool>> duplicate = x => x.EmployeeNo == r.EmployeeNo && !x.IsDeleted;
        if (currentId.HasValue)
        {
            var employeeId = currentId.Value;
            duplicate = duplicate.And(x => x.Id != employeeId);
        }
        if (await employees.ExistsAsync(duplicate, ct))
        {
            throw new ConflictException("员工编号已存在");
        }
    }
    private void Apply(Employee e, EmployeeRequest r)
    {
        e.EmployeeNo = r.EmployeeNo.Trim();
        e.SourceResumeId = IdParser.ParseNullable(r.SourceResumeId, "sourceResumeId");
        e.Name = r.Name.Trim();
        e.Gender = r.Gender;
        e.Phone = r.Phone;
        e.Email = r.Email;
        if (!string.IsNullOrWhiteSpace(r.IdCard))
        {
            e.IdCardCiphertext = protector.Protect(r.IdCard);
        }

        e.DepartmentId = IdParser.Parse(r.DepartmentId, "departmentId");
        e.PositionId = IdParser.Parse(r.PositionId, "positionId");
        e.EntryDate = r.EntryDate.Date;
        e.ProbationMonths = r.ProbationMonths;
        e.RegularDate = r.RegularDate?.Date;
        if (!string.IsNullOrWhiteSpace(r.MonthlySalary))
        {
            e.MonthlySalaryCiphertext = protector.Protect(r.MonthlySalary);
        }
    }
    private async Task<Employee> GetRequired(string id, CancellationToken ct)
    {
        var employeeId = IdParser.Parse(id);
        var employee = await employees.FirstAsync(
                employee => employee.Id == employeeId && !employee.IsDeleted,
                ct)
            ?? throw new NotFoundException("员工不存在");

        await dataScope.EnsureCanAccessEmployeeAsync(employeeId, ct);

        return employee;
    }

    private static EmployeeDto ToDto(Employee employee)
    {
        return new EmployeeDto(
            employee.Id.ToString(),
            employee.EmployeeNo,
            employee.SourceResumeId?.ToString(),
            employee.Name,
            employee.Gender,
            employee.Phone,
            employee.Email,
            employee.DepartmentId.ToString(),
            employee.PositionId.ToString(),
            employee.Status,
            employee.EntryDate,
            employee.ProbationMonths,
            employee.RegularDate,
            employee.TerminationDate,
            employee.TerminationReason,
            employee.Version);
    }

    private static string? Mask(string? value)
    {
        return value == null ? null : "**************";
    }
}
