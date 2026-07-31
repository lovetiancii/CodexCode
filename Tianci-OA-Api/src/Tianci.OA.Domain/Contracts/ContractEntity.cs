using Tianci.OA.Domain.Common;

namespace Tianci.OA.Domain.Contracts;

public sealed class EmployeeContract : AuditedEntity
{
    public string ContractNo { get; set; } = "";
    public long EmployeeId { get; set; }
    public ContractType ContractType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ushort ReminderDays { get; set; } = 30;
    public long? AttachmentFileId { get; set; }
    public long? PreviousContractId { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateTime? TerminatedAt { get; set; }
    public string? Remark { get; set; }
    public int Version { get; set; }
}
