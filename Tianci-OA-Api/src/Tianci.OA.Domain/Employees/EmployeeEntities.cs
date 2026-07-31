using Tianci.OA.Domain.Common;

namespace Tianci.OA.Domain.Employees;

public sealed class Employee : AuditedEntity
{
    public string EmployeeNo { get; set; } = "";
    public long? SourceResumeId { get; set; }
    public string Name { get; set; } = "";
    public Gender Gender { get; set; }
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string? IdCardCiphertext { get; set; }
    public long DepartmentId { get; set; }
    public long PositionId { get; set; }
    public EmployeeStatus Status { get; set; }
    public DateTime EntryDate { get; set; }
    public byte ProbationMonths { get; set; }
    public DateTime? RegularDate { get; set; }
    public string? MonthlySalaryCiphertext { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public int Version { get; set; }
}

public sealed class EmployeeEntry : AuditedEntity
{
    public long ResumeId { get; set; }
    public long? EmployeeId { get; set; }
    public DateTime PlannedEntryDate { get; set; }
    public DateTime? ActualEntryDate { get; set; }
    public long DepartmentId { get; set; }
    public long PositionId { get; set; }
    public string? MonthlySalaryCiphertext { get; set; }
    public byte ProbationMonths { get; set; } = 3;
    public EntryStatus Status { get; set; }
    public string? DeclineReason { get; set; }
    public string? Remark { get; set; }
    public int Version { get; set; }
}
