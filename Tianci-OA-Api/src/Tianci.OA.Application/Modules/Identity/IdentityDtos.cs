using System.ComponentModel.DataAnnotations;
using Tianci.OA.Domain.Common;

namespace Tianci.OA.Application.Modules.Identity;

public sealed record LoginRequest(
    [Required]
    string Username,
    [Required]
    string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserDto User,
    IReadOnlySet<string> Permissions);

public sealed record InitializeAdminRequest(
    [Required]
    [MinLength(12)]
    string Password);

public sealed class UserCreateRequest
{
    [Required]
    [StringLength(64)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? EmployeeId { get; set; }

    public string? DepartmentId { get; set; }

    public IReadOnlyList<string> RoleIds { get; set; } = [];
}

public sealed class UserUpdateRequest
{
    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? EmployeeId { get; set; }

    public string? DepartmentId { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Enabled;
}

public sealed record ResetPasswordRequest(
    [Required]
    [MinLength(8)]
    string NewPassword);

public sealed record AssignIdsRequest(
    [Required]
    IReadOnlyList<string> Ids);

public sealed record UserDto(
    string Id,
    string Username,
    string DisplayName,
    string? Phone,
    string? Email,
    string? EmployeeId,
    string? DepartmentId,
    UserStatus Status,
    bool RequiresInitialization);

public sealed class RoleUpsertRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[A-Z][A-Z0-9_]{1,63}$")]
    public string Code { get; set; } = string.Empty;

    public DataScope DataScope { get; set; } = DataScope.Self;

    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;

    [StringLength(500)]
    public string? Remark { get; set; }
}

public sealed record RoleDto(
    string Id,
    string Name,
    string Code,
    DataScope DataScope,
    EnabledStatus Status,
    bool IsSystem,
    string? Remark);

public sealed class MenuUpsertRequest
{
    public string? ParentId { get; set; }

    public MenuType Type { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(255)]
    public string? RoutePath { get; set; }

    [StringLength(255)]
    public string? Component { get; set; }

    [StringLength(128)]
    public string? PermissionCode { get; set; }

    [StringLength(64)]
    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public bool Visible { get; set; } = true;

    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;
}

public sealed record MenuDto(
    string Id,
    string? ParentId,
    MenuType Type,
    string Name,
    string? RoutePath,
    string? Component,
    string? PermissionCode,
    string? Icon,
    int SortOrder,
    bool Visible,
    EnabledStatus Status);
