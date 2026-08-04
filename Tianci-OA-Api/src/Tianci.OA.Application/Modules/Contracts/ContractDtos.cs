using System.ComponentModel.DataAnnotations;
using Tianci.OA.Domain.Common;

namespace Tianci.OA.Application.Modules.Contracts;

public sealed class ContractRequest
{
    [Required]
    [StringLength(64)]
    public string ContractNo { get; set; } = string.Empty;

    [Required]
    public string EmployeeId { get; set; } = string.Empty;

    public ContractType ContractType { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Range(0, 365)]
    public ushort ReminderDays { get; set; } = 30;

    public string? AttachmentFileId { get; set; }

    public string? PreviousContractId { get; set; }

    [StringLength(1000)]
    public string? Remark { get; set; }
}

public sealed record ContractDto(
    string Id,
    string ContractNo,
    string EmployeeId,
    ContractType ContractType,
    DateTime StartDate,
    DateTime EndDate,
    ushort ReminderDays,
    string? AttachmentFileId,
    ContractStatus Status,
    DateTime? TerminatedAt,
    string? Remark,
    int Version)
{
    public bool IsExpired =>
        Status == ContractStatus.Active
        && EndDate.Date < DateTime.UtcNow.Date;

    public bool IsExpiringSoon =>
        Status == ContractStatus.Active
        && EndDate.Date >= DateTime.UtcNow.Date
        && EndDate.Date <= DateTime.UtcNow.Date.AddDays(ReminderDays);
}

public sealed record ContractQuery(
    string? Keyword,
    string? EmployeeId,
    ContractStatus? Status,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record ContractActionRequest(
    int Version,
    string? Reason);
