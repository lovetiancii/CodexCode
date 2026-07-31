using Tianci.OA.Domain.Common;

namespace Tianci.OA.Domain.Workflows;

public enum WorkflowApprovalMode : byte
{
    Single = 1,
    Any = 2,
    All = 3
}

public sealed class WorkflowInstance : AuditedEntity
{
    public string WorkflowType { get; set; } = "";
    public string BusinessType { get; set; } = "";
    public long BusinessId { get; set; }
    public string? CurrentNodeCode { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;
    public int Version { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class WorkflowNode : AuditedEntity
{
    public long InstanceId { get; set; }
    public string NodeCode { get; set; } = "";
    public string NodeName { get; set; } = "";
    public int SequenceNo { get; set; }
    public WorkflowApprovalMode ApprovalMode { get; set; } = WorkflowApprovalMode.Single;
    public long? AssigneeUserId { get; set; }
    public WorkflowNodeStatus Status { get; set; } = WorkflowNodeStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class WorkflowRecord
{
    public long Id { get; set; }
    public long InstanceId { get; set; }
    public long? FromNodeId { get; set; }
    public long? ToNodeId { get; set; }
    public string Action { get; set; } = "";
    public long? OperatorUserId { get; set; }
    public string? Opinion { get; set; }
    public string? RequestId { get; set; }
    public DateTime OperatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
}
