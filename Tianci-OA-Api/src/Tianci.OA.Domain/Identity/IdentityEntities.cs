using Tianci.OA.Domain.Common;

namespace Tianci.OA.Domain.Identity;

public sealed class SysUser : AuditedEntity
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public long? EmployeeId { get; set; }
    public long? DepartmentId { get; set; }
    public UserStatus Status { get; set; }
    public bool RequiresInitialization { get; set; } = true;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? LastLoginAt { get; set; }
}

public sealed class SysRole : AuditedEntity
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public DataScope DataScope { get; set; } = DataScope.Self;
    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;
    public bool IsSystem { get; set; }
    public string? Remark { get; set; }
}

public sealed class SysMenu : AuditedEntity
{
    public long? ParentId { get; set; }
    public MenuType Type { get; set; }
    public string Name { get; set; } = "";
    public string? RoutePath { get; set; }
    public string? Component { get; set; }
    public string? PermissionCode { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool Visible { get; set; } = true;
    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;
}

public sealed class SysUserRole { public long Id { get; set; } public long UserId { get; set; } public long RoleId { get; set; } public DateTime CreatedAt { get; set; } public long? CreatedBy { get; set; } }
public sealed class SysRoleMenu { public long Id { get; set; } public long RoleId { get; set; } public long MenuId { get; set; } public DateTime CreatedAt { get; set; } public long? CreatedBy { get; set; } }
