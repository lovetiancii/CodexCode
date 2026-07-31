using System.ComponentModel.DataAnnotations;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Contracts;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Files;

namespace Tianci.OA.Application.Modules.Contracts;

public sealed class ContractRequest
{
    [Required, StringLength(64)] public string ContractNo { get; set; } = "";
    [Required] public string EmployeeId { get; set; } = "";
    public ContractType ContractType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [Range(0, 365)] public ushort ReminderDays { get; set; } = 30;
    public string? AttachmentFileId { get; set; }
    public string? PreviousContractId { get; set; }
    [StringLength(1000)] public string? Remark { get; set; }
}
public sealed record ContractDto(string Id, string ContractNo, string EmployeeId, ContractType ContractType, DateTime StartDate, DateTime EndDate, ushort ReminderDays, string? AttachmentFileId, ContractStatus Status, DateTime? TerminatedAt, string? Remark, int Version)
{
    public bool IsExpired => Status == ContractStatus.Active && EndDate.Date < DateTime.UtcNow.Date;
    public bool IsExpiringSoon => Status == ContractStatus.Active && EndDate.Date >= DateTime.UtcNow.Date && EndDate.Date <= DateTime.UtcNow.Date.AddDays(ReminderDays);
}
public sealed record ContractQuery(string? Keyword, string? EmployeeId, ContractStatus? Status, int PageNumber = 1, int PageSize = 20);
public sealed record ContractActionRequest(int Version, string? Reason);

public interface IContractService
{
    Task<PagedResult<ContractDto>> ListAsync(ContractQuery query, CancellationToken ct);
    Task<ContractDto> GetAsync(string id, CancellationToken ct);
    Task<ContractDto> CreateAsync(ContractRequest request, CancellationToken ct);
    Task<ContractDto> UpdateAsync(string id, ContractRequest request, int version, CancellationToken ct);
    Task ActivateAsync(string id, ContractActionRequest request, CancellationToken ct);
    Task TerminateAsync(string id, ContractActionRequest request, CancellationToken ct);
    Task<ContractDto> RenewAsync(string id, ContractRequest request, int version, CancellationToken ct);
    Task ArchiveAsync(string id, ContractActionRequest request, CancellationToken ct);
    Task<IReadOnlyList<ContractDto>> ExpiringAsync(int? withinDays, CancellationToken ct);
}

public sealed class ContractService(IRepository<EmployeeContract> contracts, IRepository<Employee> employees, IRepository<SysFile> files, ISnowflakeIdGenerator ids, IClock clock, ICurrentUser user, IUnitOfWork uow) : IContractService
{
    public async Task<PagedResult<ContractDto>> ListAsync(ContractQuery q, CancellationToken ct)
    {
        var keyword = q.Keyword?.Trim() ?? ""; var employeeId = IdParser.ParseNullable(q.EmployeeId, "employeeId"); var page = new PageRequest(q.PageNumber, q.PageSize);
        var result = await contracts.PageAsync(x => !x.IsDeleted && (keyword == "" || x.ContractNo.Contains(keyword)) && (!employeeId.HasValue || x.EmployeeId == employeeId) && (!q.Status.HasValue || x.Status == q.Status), page.SafePageNumber, page.SafePageSize, x => x.UpdatedAt, true, ct);
        return new(result.Items.Select(ToDto).ToArray(), page.SafePageNumber, page.SafePageSize, result.Total);
    }
    public async Task<ContractDto> GetAsync(string id, CancellationToken ct) => ToDto(await Required(id, ct));
    public async Task<ContractDto> CreateAsync(ContractRequest r, CancellationToken ct)
    {
        await ValidateAsync(r, null, ct); var e = new EmployeeContract { Status = ContractStatus.Draft }; Apply(e, r); EntityAudit.Create(e, ids, clock, user); await contracts.InsertAsync(e, ct); return ToDto(e);
    }
    public async Task<ContractDto> UpdateAsync(string id, ContractRequest r, int version, CancellationToken ct)
    {
        var e = await Required(id, ct); if (e.Status != ContractStatus.Draft) throw new ConflictException("仅草稿合同可编辑", "INVALID_STATE_TRANSITION"); if (e.Version != version) throw new ConflictException("数据已被其他用户修改");
        await ValidateAsync(r, e.Id, ct); Apply(e, r); await SaveVersioned(e, ct); return ToDto(e);
    }
    public async Task ActivateAsync(string id, ContractActionRequest r, CancellationToken ct)
    {
        var e = await Required(id, ct); Check(e, r.Version, ContractStatus.Draft); if (!e.AttachmentFileId.HasValue) throw new BusinessException("合同生效前必须上传附件");
        if (!await files.ExistsAsync(x => x.Id == e.AttachmentFileId && !x.IsDeleted && x.Status == FileStatus.Active, ct)) throw new NotFoundException("合同附件不存在或不可用");
        e.Status = ContractStatus.Active; await SaveVersioned(e, ct);
    }
    public async Task TerminateAsync(string id, ContractActionRequest r, CancellationToken ct)
    {
        var e = await Required(id, ct); Check(e, r.Version, ContractStatus.Active); if (string.IsNullOrWhiteSpace(r.Reason)) throw new BusinessException("终止合同必须填写原因");
        e.Status = ContractStatus.Terminated; e.TerminatedAt = clock.UtcNow; e.Remark = $"{e.Remark}\n终止原因：{r.Reason}".Trim(); await SaveVersioned(e, ct);
    }
    public async Task<ContractDto> RenewAsync(string id, ContractRequest r, int version, CancellationToken ct)
    {
        var old = await Required(id, ct); if (old.Version != version || old.Status is not (ContractStatus.Active or ContractStatus.Terminated)) throw new ConflictException("合同状态或版本不允许续签");
        await ValidateAsync(r, null, ct); var renewal = new EmployeeContract { Status = ContractStatus.Draft, PreviousContractId = old.Id }; Apply(renewal, r); renewal.PreviousContractId = old.Id; EntityAudit.Create(renewal, ids, clock, user);
        await uow.BeginAsync();
        try { await contracts.InsertAsync(renewal, ct); old.Status = ContractStatus.Renewed; await SaveVersioned(old, ct); await uow.CommitAsync(); }
        catch { await uow.RollbackAsync(); throw; }
        return ToDto(renewal);
    }
    public async Task ArchiveAsync(string id, ContractActionRequest r, CancellationToken ct)
    {
        var e = await Required(id, ct); if (e.Version != r.Version || e.Status is not (ContractStatus.Terminated or ContractStatus.Renewed)) throw new ConflictException("仅终止或已续签合同可归档");
        e.Status = ContractStatus.Archived; await SaveVersioned(e, ct);
    }
    public async Task<IReadOnlyList<ContractDto>> ExpiringAsync(int? withinDays, CancellationToken ct)
    {
        var today = clock.UtcNow.Date; var max = withinDays.HasValue ? today.AddDays(Math.Clamp(withinDays.Value, 0, 365)) : today.AddDays(365);
        var list = await contracts.ListAsync(x => !x.IsDeleted && x.Status == ContractStatus.Active && x.EndDate >= today && x.EndDate <= max, ct);
        return list.Where(x => x.EndDate.Date <= today.AddDays(withinDays ?? x.ReminderDays)).OrderBy(x => x.EndDate).Select(ToDto).ToArray();
    }
    private async Task ValidateAsync(ContractRequest r, long? currentId, CancellationToken ct)
    {
        if (r.StartDate.Date > r.EndDate.Date) throw new BusinessException("合同开始日期不得晚于结束日期");
        var employeeId = IdParser.Parse(r.EmployeeId, "employeeId"); if (!await employees.ExistsAsync(x => x.Id == employeeId && !x.IsDeleted, ct)) throw new NotFoundException("员工不存在");
        if (await contracts.ExistsAsync(x => x.ContractNo == r.ContractNo && !x.IsDeleted && (!currentId.HasValue || x.Id != currentId), ct)) throw new ConflictException("合同编号已存在");
        var fileId = IdParser.ParseNullable(r.AttachmentFileId, "attachmentFileId"); if (fileId.HasValue && !await files.ExistsAsync(x => x.Id == fileId && !x.IsDeleted, ct)) throw new NotFoundException("附件不存在");
    }
    private static void Apply(EmployeeContract e, ContractRequest r)
    {
        e.ContractNo = r.ContractNo.Trim(); e.EmployeeId = IdParser.Parse(r.EmployeeId, "employeeId"); e.ContractType = r.ContractType; e.StartDate = r.StartDate.Date; e.EndDate = r.EndDate.Date;
        e.ReminderDays = r.ReminderDays; e.AttachmentFileId = IdParser.ParseNullable(r.AttachmentFileId, "attachmentFileId"); e.PreviousContractId = IdParser.ParseNullable(r.PreviousContractId, "previousContractId"); e.Remark = r.Remark;
    }
    private async Task SaveVersioned(EmployeeContract e, CancellationToken ct)
    {
        var old = e.Version; e.Version++; EntityAudit.Update(e, clock, user); if (await contracts.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0) throw new ConflictException("数据已被其他用户修改");
    }
    private static void Check(EmployeeContract e, int version, ContractStatus required) { if (e.Version != version || e.Status != required) throw new ConflictException("合同状态或版本冲突", "INVALID_STATE_TRANSITION"); }
    private async Task<EmployeeContract> Required(string id, CancellationToken ct) => await contracts.FirstAsync(x => x.Id == IdParser.Parse(id) && !x.IsDeleted, ct) ?? throw new NotFoundException("合同不存在");
    private static ContractDto ToDto(EmployeeContract e) => new(e.Id.ToString(), e.ContractNo, e.EmployeeId.ToString(), e.ContractType, e.StartDate, e.EndDate, e.ReminderDays, e.AttachmentFileId?.ToString(), e.Status, e.TerminatedAt, e.Remark, e.Version);
}
