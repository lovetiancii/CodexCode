using Tianci.OA.Domain.Common;

namespace Tianci.OA.Domain.Recruitment;

public sealed class Resume : AuditedEntity
{
    public string CandidateNo { get; set; } = "";
    public string Name { get; set; } = "";
    public Gender Gender { get; set; }
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string? Education { get; set; }
    public string? WorkExperience { get; set; }
    public string? Skills { get; set; }
    public long AppliedPositionId { get; set; }
    public string? Source { get; set; }
    public long? AttachmentFileId { get; set; }
    public ResumeStatus Status { get; set; } = ResumeStatus.Submitted;
    public byte CurrentRound { get; set; }
    public long? OwnerUserId { get; set; }
    public string? RejectReason { get; set; }
    public string? Remark { get; set; }
    public int Version { get; set; }
}

public sealed class InterviewRecord : AuditedEntity
{
    public long ResumeId { get; set; }
    public byte RoundNo { get; set; }
    public long InterviewerUserId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Location { get; set; }
    public decimal? Score { get; set; }
    public string? Evaluation { get; set; }
    public InterviewConclusion Conclusion { get; set; }
    public DateTime? NextScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Remark { get; set; }
}
