namespace Tianci.OA.Domain.Common;

public abstract class AuditedEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long? DeletedBy { get; set; }
}

public sealed class DomainException(string message, string code = "BUSINESS_ERROR") : Exception(message)
{
    public string Code { get; } = code;
}

public enum EnabledStatus : byte
{
    Disabled = 0, Enabled = 1
}
public enum UserStatus : byte
{
    Disabled = 0, Enabled = 1, Locked = 2
}
public enum DataScope : byte
{
    All = 1, DepartmentAndChildren = 2, Self = 3
}
public enum MenuType : byte
{
    Directory = 1, Menu = 2, Action = 3
}
public enum Gender : byte
{
    Unknown = 0, Male = 1, Female = 2
}
public enum EmployeeStatus : byte
{
    Probation = 1, Active = 2, Terminated = 3, Archived = 4
}
public enum ResumeStatus : byte
{
    Submitted = 1,
    Screening = 2,
    InterviewPending = 3,
    Interviewing = 4,
    OfferPending = 5,
    EntryPending = 6,
    Hired = 7,
    Rejected = 8,
    OfferDeclined = 9
}
public enum InterviewConclusion : byte
{
    Pending = 0, Pass = 1, Fail = 2, Hold = 3, Cancelled = 4
}
public enum EntryStatus : byte
{
    OfferConfirmed = 1, EntryPending = 2, Entered = 3, Declined = 4, Cancelled = 5
}
public enum ContractType : byte
{
    Labor = 1, Internship = 2, Confidentiality = 3, Other = 4
}
public enum ContractStatus : byte
{
    Draft = 1, Active = 2, Terminated = 3, Renewed = 4, Archived = 5
}
public enum FileStatus : byte
{
    Temporary = 0, Active = 1, Quarantined = 2
}
public enum WorkflowStatus : byte
{
    Running = 1, Completed = 2, Rejected = 3, Cancelled = 4
}
public enum WorkflowNodeStatus : byte
{
    Pending = 0, Active = 1, Passed = 2, Rejected = 3, Skipped = 4, Cancelled = 5
}
