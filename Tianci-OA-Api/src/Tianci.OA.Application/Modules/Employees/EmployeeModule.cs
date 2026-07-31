using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Organization;

namespace Tianci.OA.Application.Modules.Employees;

public sealed class EmployeeRequest
{
    [Required, StringLength(32)] public string EmployeeNo { get; set; } = "";
    public string? SourceResumeId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public Gender Gender { get; set; }
    [Required, Phone] public string Phone { get; set; } = "";
    [EmailAddress] public string? Email { get; set; }
    public string? IdCard { get; set; }
    [Required] public string DepartmentId { get; set; } = "";
    [Required] public string PositionId { get; set; } = "";
    public DateTime EntryDate { get; set; }
    [Range(0, 12)] public byte ProbationMonths { get; set; }
    public DateTime? RegularDate { get; set; }
    public string? MonthlySalary { get; set; }
}
public sealed record EmployeeDto(string Id, string EmployeeNo, string? SourceResumeId, string Name, Gender Gender, string Phone, string? Email, string DepartmentId, string PositionId, EmployeeStatus Status, DateTime EntryDate, byte ProbationMonths, DateTime? RegularDate, DateTime? TerminationDate, string? TerminationReason, int Version);
public sealed record EmployeeDetailDto(EmployeeDto Employee, string? IdCard, string? MonthlySalary);
public sealed record TerminateEmployeeRequest(DateTime TerminationDate, [Required, StringLength(500)] string Reason, int Version);
public sealed record EmployeeQuery(string? Keyword, string? DepartmentId, string? PositionId, EmployeeStatus? Status, int PageNumber = 1, int PageSize = 20);

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(EmployeeQuery query, CancellationToken ct);
    Task<EmployeeDetailDto> GetAsync(string id, bool includeSensitive, CancellationToken ct);
    Task<EmployeeDto> CreateAsync(EmployeeRequest request, CancellationToken ct);
    Task<EmployeeDto> UpdateAsync(string id, EmployeeRequest request, int version, CancellationToken ct);
    Task TerminateAsync(string id, TerminateEmployeeRequest request, CancellationToken ct);
    Task ArchiveAsync(string id, int version, CancellationToken ct);
}

public sealed class EmployeeService(IRepository<Employee> employees, IRepository<Department> departments, IRepository<Position> positions, ISensitiveDataProtector protector, ISnowflakeIdGenerator ids, IClock clock, ICurrentUser user) : IEmployeeService
{
    public async Task<PagedResult<EmployeeDto>> ListAsync(EmployeeQuery q, CancellationToken ct)
    {
        var keyword = q.Keyword?.Trim() ?? ""; var did = IdParser.ParseNullable(q.DepartmentId, "departmentId"); var pid = IdParser.ParseNullable(q.PositionId, "positionId");
        var page = new PageRequest(q.PageNumber, q.PageSize);
        Expression<Func<Employee, bool>> predicate = x => !x.IsDeleted &&
            (keyword == "" || x.Name.Contains(keyword) || x.EmployeeNo.Contains(keyword) || x.Phone.Contains(keyword));
        if (did.HasValue) { var departmentId = did.Value; predicate = predicate.And(x => x.DepartmentId == departmentId); }
        if (pid.HasValue) { var positionId = pid.Value; predicate = predicate.And(x => x.PositionId == positionId); }
        if (q.Status.HasValue) { var status = q.Status.Value; predicate = predicate.And(x => x.Status == status); }
        var result = await employees.PageAsync(predicate, page.SafePageNumber, page.SafePageSize, x => x.UpdatedAt, true, ct);
        return new(result.Items.Select(ToDto).ToArray(), page.SafePageNumber, page.SafePageSize, result.Total);
    }
    public async Task<EmployeeDetailDto> GetAsync(string id, bool includeSensitive, CancellationToken ct)
    {
        var e = await GetRequired(id, ct);
        return new(ToDto(e), includeSensitive && e.IdCardCiphertext != null ? protector.Unprotect(e.IdCardCiphertext) : Mask(e.IdCardCiphertext),
            includeSensitive && e.MonthlySalaryCiphertext != null ? protector.Unprotect(e.MonthlySalaryCiphertext) : (e.MonthlySalaryCiphertext == null ? null : "******"));
    }
    public async Task<EmployeeDto> CreateAsync(EmployeeRequest r, CancellationToken ct)
    {
        await ValidateAsync(r, null, ct);
        var e = new Employee { Status = r.ProbationMonths > 0 ? EmployeeStatus.Probation : EmployeeStatus.Active };
        Apply(e, r); EntityAudit.Create(e, ids, clock, user); await employees.InsertAsync(e, ct); return ToDto(e);
    }
    public async Task<EmployeeDto> UpdateAsync(string id, EmployeeRequest r, int version, CancellationToken ct)
    {
        var e = await GetRequired(id, ct); if (e.Status is EmployeeStatus.Terminated or EmployeeStatus.Archived) throw new ConflictException("离职或归档员工不可编辑", "INVALID_STATE_TRANSITION");
        if (e.Version != version) throw new ConflictException("数据已被其他用户修改");
        await ValidateAsync(r, e.Id, ct); Apply(e, r); var old = e.Version; e.Version++; EntityAudit.Update(e, clock, user);
        if (await employees.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0) throw new ConflictException("数据已被其他用户修改");
        return ToDto(e);
    }
    public async Task TerminateAsync(string id, TerminateEmployeeRequest r, CancellationToken ct)
    {
        var e = await GetRequired(id, ct); if (e.Status is not (EmployeeStatus.Probation or EmployeeStatus.Active)) throw new ConflictException("仅试用或在职员工可办理离职", "INVALID_STATE_TRANSITION");
        if (r.TerminationDate.Date < e.EntryDate.Date || r.TerminationDate.Date > clock.UtcNow.Date) throw new BusinessException("离职日期必须介于入职日期与今天之间");
        if (e.Version != r.Version) throw new ConflictException("数据已被其他用户修改");
        var old = e.Version; e.Status = EmployeeStatus.Terminated; e.TerminationDate = r.TerminationDate.Date; e.TerminationReason = r.Reason.Trim(); e.Version++; EntityAudit.Update(e, clock, user);
        if (await employees.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0) throw new ConflictException("数据已被其他用户修改");
    }
    public async Task ArchiveAsync(string id, int version, CancellationToken ct)
    {
        var e = await GetRequired(id, ct); if (e.Status != EmployeeStatus.Terminated) throw new ConflictException("仅离职员工可归档", "INVALID_STATE_TRANSITION"); if (e.Version != version) throw new ConflictException("数据已被其他用户修改");
        var old = e.Version; e.Status = EmployeeStatus.Archived; e.Version++; EntityAudit.Update(e, clock, user);
        if (await employees.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0) throw new ConflictException("数据已被其他用户修改");
    }
    private async Task ValidateAsync(EmployeeRequest r, long? currentId, CancellationToken ct)
    {
        var did = IdParser.Parse(r.DepartmentId, "departmentId"); var pid = IdParser.Parse(r.PositionId, "positionId");
        if (!await departments.ExistsAsync(x => x.Id == did && !x.IsDeleted && x.Status == EnabledStatus.Enabled, ct)) throw new NotFoundException("部门不存在或未启用");
        if (!await positions.ExistsAsync(x => x.Id == pid && x.DepartmentId == did && !x.IsDeleted && x.Status == EnabledStatus.Enabled, ct)) throw new NotFoundException("岗位不存在、未启用或不属于该部门");
        Expression<Func<Employee, bool>> duplicate = x => x.EmployeeNo == r.EmployeeNo && !x.IsDeleted;
        if (currentId.HasValue) { var employeeId = currentId.Value; duplicate = duplicate.And(x => x.Id != employeeId); }
        if (await employees.ExistsAsync(duplicate, ct)) throw new ConflictException("员工编号已存在");
    }
    private void Apply(Employee e, EmployeeRequest r)
    {
        e.EmployeeNo = r.EmployeeNo.Trim(); e.SourceResumeId = IdParser.ParseNullable(r.SourceResumeId, "sourceResumeId"); e.Name = r.Name.Trim(); e.Gender = r.Gender; e.Phone = r.Phone; e.Email = r.Email;
        if (!string.IsNullOrWhiteSpace(r.IdCard)) e.IdCardCiphertext = protector.Protect(r.IdCard); e.DepartmentId = IdParser.Parse(r.DepartmentId, "departmentId"); e.PositionId = IdParser.Parse(r.PositionId, "positionId");
        e.EntryDate = r.EntryDate.Date; e.ProbationMonths = r.ProbationMonths; e.RegularDate = r.RegularDate?.Date; if (!string.IsNullOrWhiteSpace(r.MonthlySalary)) e.MonthlySalaryCiphertext = protector.Protect(r.MonthlySalary);
    }
    private async Task<Employee> GetRequired(string id, CancellationToken ct) => await employees.FirstAsync(x => x.Id == IdParser.Parse(id) && !x.IsDeleted, ct) ?? throw new NotFoundException("员工不存在");
    private static EmployeeDto ToDto(Employee e) => new(e.Id.ToString(), e.EmployeeNo, e.SourceResumeId?.ToString(), e.Name, e.Gender, e.Phone, e.Email, e.DepartmentId.ToString(), e.PositionId.ToString(), e.Status, e.EntryDate, e.ProbationMonths, e.RegularDate, e.TerminationDate, e.TerminationReason, e.Version);
    private static string? Mask(string? value) => value == null ? null : "**************";
}
