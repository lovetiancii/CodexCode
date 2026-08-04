using System.ComponentModel.DataAnnotations;
using Tianci.OA.Domain.Common;

namespace Tianci.OA.Application.Modules.Organization;

public sealed class DepartmentRequest
{
    public string? ParentId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    public string? LeaderEmployeeId { get; set; }

    public int SortOrder { get; set; }

    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;

    [StringLength(500)]
    public string? Remark { get; set; }
}

public sealed record DepartmentDto(
    string Id,
    string? ParentId,
    string Name,
    string Code,
    int SortOrder,
    EnabledStatus Status,
    string? Remark);

public sealed class PositionRequest
{
    [Required]
    public string DepartmentId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    public EnabledStatus Status { get; set; } = EnabledStatus.Enabled;

    [StringLength(500)]
    public string? Remark { get; set; }
}

public sealed record PositionDto(
    string Id,
    string DepartmentId,
    string Name,
    string Code,
    EnabledStatus Status,
    string? Remark);
