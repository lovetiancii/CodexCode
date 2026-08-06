using System.Linq.Expressions;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Contracts;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Files;

namespace Tianci.OA.Application.Modules.Contracts;

public sealed class ContractService(
    IRepository<EmployeeContract> contracts,
    IRepository<Employee> employees,
    IRepository<SysFile> files,
    IDataScopeService dataScope,
    ISnowflakeIdGenerator ids,
    IClock clock,
    ICurrentUser user,
    IUnitOfWork uow) : IContractService
{
    public async Task<PagedResult<ContractDto>> ListAsync(ContractQuery q, CancellationToken ct)
    {
        var keyword = q.Keyword?.Trim() ?? "";
        var employeeId = IdParser.ParseNullable(q.EmployeeId, "employeeId");
        var page = new PageRequest(q.PageNumber, q.PageSize);
        Expression<Func<EmployeeContract, bool>> predicate =
            x => !x.IsDeleted && (keyword == "" || x.ContractNo.Contains(keyword));

        predicate = await ApplyDataScopeAsync(predicate, ct);

        if (employeeId.HasValue)
        {
            var id = employeeId.Value;
            predicate = predicate.And(x => x.EmployeeId == id);
        }
        if (q.Status.HasValue)
        {
            var status = q.Status.Value;
            predicate = predicate.And(x => x.Status == status);
        }
        var (items, total) = await contracts.PageAsync(
            predicate,
            page.SafePageNumber,
            page.SafePageSize,
            contract => contract.UpdatedAt,
            true,
            ct);

        return new PagedResult<ContractDto>(
            [.. items.Select(ToDto)],
            page.SafePageNumber,
            page.SafePageSize,
            total);
    }
    public async Task<ContractDto> GetAsync(string id, CancellationToken ct)
    {
        return ToDto(await Required(id, ct));
    }

    public async Task<ContractDto> CreateAsync(ContractRequest r, CancellationToken ct)
    {
        await ValidateAsync(r, null, ct);
        var e = new EmployeeContract { Status = ContractStatus.Draft };
        Apply(e, r);
        EntityAudit.Create(e, ids, clock, user);
        await contracts.InsertAsync(e, ct);
        return ToDto(e);
    }
    public async Task<ContractDto> UpdateAsync(string id, ContractRequest r, int version, CancellationToken ct)
    {
        var e = await Required(id, ct);
        if (e.Status != ContractStatus.Draft)
        {
            throw new ConflictException("仅草稿合同可编辑", "INVALID_STATE_TRANSITION");
        }

        if (e.Version != version)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        await ValidateAsync(r, e.Id, ct);
        Apply(e, r);
        await SaveVersioned(e, ct);
        return ToDto(e);
    }
    public async Task ActivateAsync(string id, ContractActionRequest r, CancellationToken ct)
    {
        var e = await Required(id, ct);
        Check(e, r.Version, ContractStatus.Draft);
        if (!e.AttachmentFileId.HasValue)
        {
            throw new BusinessException("合同生效前必须上传附件");
        }

        var attachmentFileId = e.AttachmentFileId.Value;
        if (!await files.ExistsAsync(x => x.Id == attachmentFileId && !x.IsDeleted && x.Status == FileStatus.Active, ct))
        {
            throw new NotFoundException("合同附件不存在或不可用");
        }

        e.Status = ContractStatus.Active;
        await SaveVersioned(e, ct);
    }
    public async Task TerminateAsync(string id, ContractActionRequest r, CancellationToken ct)
    {
        var e = await Required(id, ct);
        Check(e, r.Version, ContractStatus.Active);
        if (string.IsNullOrWhiteSpace(r.Reason))
        {
            throw new BusinessException("终止合同必须填写原因");
        }

        e.Status = ContractStatus.Terminated;
        e.TerminatedAt = clock.UtcNow;
        e.Remark = $"{e.Remark}\n终止原因：{r.Reason}".Trim();
        await SaveVersioned(e, ct);
    }
    public async Task<ContractDto> RenewAsync(string id, ContractRequest r, int version, CancellationToken ct)
    {
        var old = await Required(id, ct);
        if (old.Version != version || old.Status is not (ContractStatus.Active or ContractStatus.Terminated))
        {
            throw new ConflictException("合同状态或版本不允许续签");
        }

        await ValidateAsync(r, null, ct);
        var renewal = new EmployeeContract
        {
            Status = ContractStatus.Draft,
            PreviousContractId = old.Id
        };
        Apply(renewal, r);
        renewal.PreviousContractId = old.Id;
        EntityAudit.Create(renewal, ids, clock, user);
        await uow.BeginAsync();
        try
        {
            await contracts.InsertAsync(renewal, ct);
            old.Status = ContractStatus.Renewed;
            await SaveVersioned(old, ct);
            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
        return ToDto(renewal);
    }
    public async Task ArchiveAsync(string id, ContractActionRequest r, CancellationToken ct)
    {
        var e = await Required(id, ct);
        if (e.Version != r.Version || e.Status is not (ContractStatus.Terminated or ContractStatus.Renewed))
        {
            throw new ConflictException("仅终止或已续签合同可归档");
        }

        e.Status = ContractStatus.Archived;
        await SaveVersioned(e, ct);
    }
    public async Task<IReadOnlyList<ContractDto>> ExpiringAsync(int? withinDays, CancellationToken ct)
    {
        var today = clock.UtcNow.Date;
        var max = withinDays.HasValue ? today.AddDays(Math.Clamp(withinDays.Value, 0, 365)) : today.AddDays(365);
        Expression<Func<EmployeeContract, bool>> predicate = contract =>
            !contract.IsDeleted
            && contract.Status == ContractStatus.Active
            && contract.EndDate >= today
            && contract.EndDate <= max;
        predicate = await ApplyDataScopeAsync(predicate, ct);

        var list = await contracts.ListAsync(predicate, ct);

        return
        [
            .. list
                .Where(contract => contract.EndDate.Date <= today.AddDays(
                    withinDays ?? contract.ReminderDays))
                .OrderBy(contract => contract.EndDate)
                .Select(ToDto)
        ];
    }
    private async Task ValidateAsync(ContractRequest r, long? currentId, CancellationToken ct)
    {
        if (r.StartDate.Date > r.EndDate.Date)
        {
            throw new BusinessException("合同开始日期不得晚于结束日期");
        }

        var employeeId = IdParser.Parse(r.EmployeeId, "employeeId");
        await dataScope.EnsureCanAccessEmployeeAsync(employeeId, ct);

        if (!await employees.ExistsAsync(x => x.Id == employeeId && !x.IsDeleted, ct))
        {
            throw new NotFoundException("员工不存在");
        }

        Expression<Func<EmployeeContract, bool>> duplicate = x => x.ContractNo == r.ContractNo && !x.IsDeleted;
        if (currentId.HasValue)
        {
            var contractId = currentId.Value;
            duplicate = duplicate.And(x => x.Id != contractId);
        }
        if (await contracts.ExistsAsync(duplicate, ct))
        {
            throw new ConflictException("合同编号已存在");
        }

        var fileId = IdParser.ParseNullable(r.AttachmentFileId, "attachmentFileId");
        if (fileId.HasValue)
        {
            var attachmentId = fileId.Value;
            if (!await files.ExistsAsync(x => x.Id == attachmentId && !x.IsDeleted, ct))
            {
                throw new NotFoundException("附件不存在");
            }
        }
    }
    private static void Apply(EmployeeContract e, ContractRequest r)
    {
        e.ContractNo = r.ContractNo.Trim();
        e.EmployeeId = IdParser.Parse(r.EmployeeId, "employeeId");
        e.ContractType = r.ContractType;
        e.StartDate = r.StartDate.Date;
        e.EndDate = r.EndDate.Date;
        e.ReminderDays = r.ReminderDays;
        e.AttachmentFileId = IdParser.ParseNullable(r.AttachmentFileId, "attachmentFileId");
        e.PreviousContractId = IdParser.ParseNullable(r.PreviousContractId, "previousContractId");
        e.Remark = r.Remark;
    }
    private async Task SaveVersioned(EmployeeContract e, CancellationToken ct)
    {
        var old = e.Version;
        e.Version++;
        EntityAudit.Update(e, clock, user);
        if (await contracts.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct) == 0)
        {
            throw new ConflictException("数据已被其他用户修改");
        }
    }
    private static void Check(EmployeeContract e, int version, ContractStatus required)
    {
        if (e.Version != version || e.Status != required)
        {
            throw new ConflictException("合同状态或版本冲突", "INVALID_STATE_TRANSITION");
        }
    }
    private async Task<EmployeeContract> Required(string id, CancellationToken ct)
    {
        var contractId = IdParser.Parse(id);
        var contract = await contracts.FirstAsync(
                contract => contract.Id == contractId && !contract.IsDeleted,
                ct)
            ?? throw new NotFoundException("合同不存在");

        await dataScope.EnsureCanAccessContractAsync(contractId, ct);

        return contract;
    }

    private async Task<Expression<Func<EmployeeContract, bool>>> ApplyDataScopeAsync(
        Expression<Func<EmployeeContract, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var scope = await dataScope.GetCurrentAsync(cancellationToken);
        if (scope.Scope == DataScope.All)
        {
            return predicate;
        }

        if (scope.Scope == DataScope.Self)
        {
            var employeeId = scope.EmployeeId ?? -1;
            return predicate.And(contract => contract.EmployeeId == employeeId);
        }

        var departmentIds = scope.DepartmentIds.ToArray();
        var allowedEmployees = await employees.ListAsync(
            employee => !employee.IsDeleted
                && departmentIds.Contains(employee.DepartmentId),
            cancellationToken);
        var employeeIds = allowedEmployees
            .Select(employee => employee.Id)
            .ToArray();

        return predicate.And(contract => employeeIds.Contains(contract.EmployeeId));
    }

    private static ContractDto ToDto(EmployeeContract contract)
    {
        return new ContractDto(
            contract.Id.ToString(),
            contract.ContractNo,
            contract.EmployeeId.ToString(),
            contract.ContractType,
            contract.StartDate,
            contract.EndDate,
            contract.ReminderDays,
            contract.AttachmentFileId?.ToString(),
            contract.Status,
            contract.TerminatedAt,
            contract.Remark,
            contract.Version);
    }
}
