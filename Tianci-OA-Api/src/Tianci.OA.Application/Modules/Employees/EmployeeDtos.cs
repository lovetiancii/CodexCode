using System.ComponentModel.DataAnnotations;
using Tianci.OA.Domain.Common;

namespace Tianci.OA.Application.Modules.Employees;

public sealed class EmployeeRequest
{
    [Required]
    [StringLength(32)]
    public string EmployeeNo { get; set; } = string.Empty;

    public string? SourceResumeId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public string? IdCard { get; set; }

    [Required]
    public string DepartmentId { get; set; } = string.Empty;

    [Required]
    public string PositionId { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; }

    [Range(0, 12)]
    public byte ProbationMonths { get; set; }

    public DateTime? RegularDate { get; set; }

    public string? MonthlySalary { get; set; }
}

public sealed record EmployeeDto(
    string Id,
    string EmployeeNo,
    string? SourceResumeId,
    string Name,
    Gender Gender,
    string Phone,
    string? Email,
    string DepartmentId,
    string PositionId,
    EmployeeStatus Status,
    DateTime EntryDate,
    byte ProbationMonths,
    DateTime? RegularDate,
    DateTime? TerminationDate,
    string? TerminationReason,
    int Version);

public sealed record EmployeeDetailDto(
    EmployeeDto Employee,
    string? IdCard,
    string? MonthlySalary);

public sealed record RegularizeEmployeeRequest(
    DateTime RegularDate,
    int Version);

public sealed record TerminateEmployeeRequest(
    DateTime TerminationDate,
    [Required]
    [StringLength(500)]
    string Reason,
    int Version);

public sealed record EmployeeQuery(
    string? Keyword,
    string? DepartmentId,
    string? PositionId,
    EmployeeStatus? Status,
    int PageNumber = 1,
    int PageSize = 20);
