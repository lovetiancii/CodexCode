using Tianci.OA.Domain.Common;

namespace Tianci.OA.Domain.Organization;

public sealed class Department : AuditedEntity
{
    public long? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public long? LeaderEmployeeId { get; set; }
    public int SortOrder { get; set; }
    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;
    public string? Remark { get; set; }
}

public sealed class Position : AuditedEntity
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;
    public string? Remark { get; set; }
}
