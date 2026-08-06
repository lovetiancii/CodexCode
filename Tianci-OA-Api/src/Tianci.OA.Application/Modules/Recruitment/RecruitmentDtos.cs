using System.ComponentModel.DataAnnotations;
using Tianci.OA.Domain.Common;

namespace Tianci.OA.Application.Modules.Recruitment;

public sealed class ResumeRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(64)]
    public string? Education { get; set; }

    public string? WorkExperience { get; set; }

    public string? Skills { get; set; }

    [Required]
    public string AppliedPositionId { get; set; } = string.Empty;

    [StringLength(64)]
    public string? Source { get; set; }

    public string? AttachmentFileId { get; set; }

    public string? OwnerUserId { get; set; }

    [StringLength(1000)]
    public string? Remark { get; set; }
}

public sealed record ResumeDto(
    string Id,
    string CandidateNo,
    string Name,
    Gender Gender,
    string Phone,
    string? Email,
    string? Education,
    string? WorkExperience,
    string? Skills,
    string AppliedPositionId,
    string? Source,
    string? AttachmentFileId,
    string? OwnerUserId,
    ResumeStatus Status,
    byte CurrentRound,
    string? RejectReason,
    string? Remark,
    int Version);

public sealed record ResumeAttachmentRequest(
    string? AttachmentFileId,
    int Version);

public sealed record ResumeQuery(
    string? Keyword,
    string? PositionId,
    ResumeStatus? Status,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record ChangeResumeStatusRequest(
    ResumeStatus TargetStatus,
    string? Reason,
    int Version);

public sealed class ScheduleInterviewRequest
{
    [Range(1, 5)]
    public byte RoundNo { get; set; }

    [Required]
    public string InterviewerUserId { get; set; } = string.Empty;

    public DateTime ScheduledAt { get; set; }

    [StringLength(255)]
    public string? Location { get; set; }

    [StringLength(1000)]
    public string? Remark { get; set; }

    public int ResumeVersion { get; set; }
}

public sealed class CompleteInterviewRequest
{
    [Range(0, 100)]
    public decimal Score { get; set; }

    [Required]
    [StringLength(2000)]
    public string Evaluation { get; set; } = string.Empty;

    public InterviewConclusion Conclusion { get; set; }

    public bool IsFinalRound { get; set; }

    public DateTime? NextScheduledAt { get; set; }

    [StringLength(1000)]
    public string? Remark { get; set; }

    public int ResumeVersion { get; set; }
}

public sealed record InterviewDto(
    string Id,
    string ResumeId,
    byte RoundNo,
    string InterviewerUserId,
    DateTime ScheduledAt,
    string? Location,
    decimal? Score,
    string? Evaluation,
    InterviewConclusion Conclusion,
    DateTime? NextScheduledAt,
    DateTime? CompletedAt,
    string? Remark);

public sealed record InterviewerOptionDto(
    string UserId,
    string EmployeeId,
    string EmployeeNo,
    string Name,
    string DepartmentId,
    string DepartmentName,
    string PositionId,
    string PositionName);

public sealed class ConfirmOfferRequest
{
    public DateTime PlannedEntryDate { get; set; }

    [Required]
    public string DepartmentId { get; set; } = string.Empty;

    [Required]
    public string PositionId { get; set; } = string.Empty;

    [Required]
    public string MonthlySalary { get; set; } = string.Empty;

    [Range(0, 12)]
    public byte ProbationMonths { get; set; } = 3;

    [StringLength(1000)]
    public string? Remark { get; set; }

    public int ResumeVersion { get; set; }
}

public sealed record ConfirmEntryRequest(
    DateTime ActualEntryDate,
    [Required]
    string EmployeeNo,
    int ResumeVersion,
    int EntryVersion);
