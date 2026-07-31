using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Organization;

namespace Tianci.OA.Application.Modules.Organization;

public sealed class DepartmentRequest
{
    public string? ParentId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(64)] public string Code { get; set; } = "";
    public string? LeaderEmployeeId { get; set; }
    public int SortOrder { get; set; }
    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;
    [StringLength(500)] public string? Remark { get; set; }
}
public sealed record DepartmentDto(string Id, string? ParentId, string Name, string Code, int SortOrder, EnabledStatus Status, string? Remark);

public sealed class PositionRequest
{
    [Required] public string DepartmentId { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(64)] public string Code { get; set; } = "";
    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;
    [StringLength(500)] public string? Remark { get; set; }
}
public sealed record PositionDto(string Id, string DepartmentId, string Name, string Code, EnabledStatus Status, string? Remark);

public interface IOrganizationService
{
    Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken ct);
    Task<DepartmentDto> CreateDepartmentAsync(DepartmentRequest request, CancellationToken ct);
    Task<DepartmentDto> UpdateDepartmentAsync(string id, DepartmentRequest request, CancellationToken ct);
    Task DeleteDepartmentAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<PositionDto>> PositionsAsync(string? departmentId, CancellationToken ct);
    Task<PositionDto> CreatePositionAsync(PositionRequest request, CancellationToken ct);
    Task<PositionDto> UpdatePositionAsync(string id, PositionRequest request, CancellationToken ct);
    Task DeletePositionAsync(string id, CancellationToken ct);
}

public sealed class OrganizationService(IRepository<Department> departments, IRepository<Position> positions, IRepository<Tianci.OA.Domain.Employees.Employee> employees, ISnowflakeIdGenerator ids, IClock clock, ICurrentUser user, IMapper mapper) : IOrganizationService
{
    public async Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken ct) => mapper.Map<IReadOnlyList<DepartmentDto>>(await departments.ListAsync(x => !x.IsDeleted, ct));
    public async Task<DepartmentDto> CreateDepartmentAsync(DepartmentRequest r, CancellationToken ct)
    {
        if (await departments.ExistsAsync(x => x.Code == r.Code && !x.IsDeleted, ct)) throw new ConflictException("部门编码已存在");
        var e = new Department(); Apply(e, r); EntityAudit.Create(e, ids, clock, user); await departments.InsertAsync(e, ct); return mapper.Map<DepartmentDto>(e);
    }
    public async Task<DepartmentDto> UpdateDepartmentAsync(string id, DepartmentRequest r, CancellationToken ct)
    {
        var e = await departments.GetByIdAsync(IdParser.Parse(id), ct) ?? throw new NotFoundException("部门不存在");
        if (r.ParentId == id) throw new BusinessException("部门不能以自身为上级");
        Apply(e, r); EntityAudit.Update(e, clock, user); await departments.UpdateAsync(e, ct); return mapper.Map<DepartmentDto>(e);
    }
    public async Task DeleteDepartmentAsync(string id, CancellationToken ct)
    {
        var e = await departments.GetByIdAsync(IdParser.Parse(id), ct) ?? throw new NotFoundException("部门不存在");
        if (await departments.ExistsAsync(x => x.ParentId == e.Id && !x.IsDeleted, ct) || await employees.ExistsAsync(x => x.DepartmentId == e.Id && !x.IsDeleted, ct)) throw new ConflictException("部门已被引用，只能停用");
        e.IsDeleted = true; e.DeletedAt = clock.UtcNow; e.DeletedBy = user.UserId; await departments.UpdateAsync(e, ct);
    }
    public async Task<IReadOnlyList<PositionDto>> PositionsAsync(string? departmentId, CancellationToken ct)
    {
        var did = IdParser.ParseNullable(departmentId, "departmentId");
        if (!did.HasValue)
            return mapper.Map<IReadOnlyList<PositionDto>>(await positions.ListAsync(x => !x.IsDeleted, ct));
        var id = did.Value;
        return mapper.Map<IReadOnlyList<PositionDto>>(await positions.ListAsync(x => !x.IsDeleted && x.DepartmentId == id, ct));
    }
    public async Task<PositionDto> CreatePositionAsync(PositionRequest r, CancellationToken ct)
    {
        var did = IdParser.Parse(r.DepartmentId, "departmentId");
        if (!await departments.ExistsAsync(x => x.Id == did && !x.IsDeleted, ct)) throw new NotFoundException("部门不存在");
        if (await positions.ExistsAsync(x => x.Code == r.Code && !x.IsDeleted, ct)) throw new ConflictException("岗位编码已存在");
        var e = new Position { DepartmentId = did, Name = r.Name.Trim(), Code = r.Code.Trim(), Status = r.Status, Remark = r.Remark };
        EntityAudit.Create(e, ids, clock, user); await positions.InsertAsync(e, ct); return mapper.Map<PositionDto>(e);
    }
    public async Task<PositionDto> UpdatePositionAsync(string id, PositionRequest r, CancellationToken ct)
    {
        var e = await positions.GetByIdAsync(IdParser.Parse(id), ct) ?? throw new NotFoundException("岗位不存在");
        e.DepartmentId = IdParser.Parse(r.DepartmentId, "departmentId"); e.Name = r.Name.Trim(); e.Code = r.Code.Trim(); e.Status = r.Status; e.Remark = r.Remark;
        EntityAudit.Update(e, clock, user); await positions.UpdateAsync(e, ct); return mapper.Map<PositionDto>(e);
    }
    public async Task DeletePositionAsync(string id, CancellationToken ct)
    {
        var e = await positions.GetByIdAsync(IdParser.Parse(id), ct) ?? throw new NotFoundException("岗位不存在");
        if (await employees.ExistsAsync(x => x.PositionId == e.Id && !x.IsDeleted, ct)) throw new ConflictException("岗位已被引用，只能停用");
        e.IsDeleted = true; e.DeletedAt = clock.UtcNow; e.DeletedBy = user.UserId; await positions.UpdateAsync(e, ct);
    }
    private static void Apply(Department e, DepartmentRequest r)
    {
        e.ParentId = IdParser.ParseNullable(r.ParentId, "parentId"); e.Name = r.Name.Trim(); e.Code = r.Code.Trim();
        e.LeaderEmployeeId = IdParser.ParseNullable(r.LeaderEmployeeId, "leaderEmployeeId"); e.SortOrder = r.SortOrder; e.Status = r.Status; e.Remark = r.Remark;
    }
}
