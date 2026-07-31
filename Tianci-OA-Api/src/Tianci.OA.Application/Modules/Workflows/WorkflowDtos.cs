using System.ComponentModel.DataAnnotations;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Workflows;

namespace Tianci.OA.Application.Modules.Workflows;

public sealed class StartWorkflowRequest
{
    [Required, StringLength(64)]
    public string WorkflowType { get; init; } = "";

    [Required, StringLength(64)]
    public string BusinessType { get; init; } = "";

    [Range(1, long.MaxValue)]
    public long BusinessId { get; init; }

    [Required, StringLength(64)]
    public string RequestId { get; init; } = "";

    [Required, MinLength(1)]
    public IReadOnlyList<WorkflowNodeRequest> Nodes { get; init; } = [];
}

public sealed class WorkflowNodeRequest
{
    [Required, StringLength(64)]
    public string NodeCode { get; init; } = "";

    [Required, StringLength(100)]
    public string NodeName { get; init; } = "";

    [Range(1, int.MaxValue)]
    public int SequenceNo { get; init; }

    public WorkflowApprovalMode ApprovalMode { get; init; } = WorkflowApprovalMode.Single;

    [Range(1, long.MaxValue)]
    public long? AssigneeUserId { get; init; }
}

public enum WorkflowDecision : byte
{
    Pass = 1,
    Reject = 2
}

public sealed class ApproveWorkflowNodeRequest
{
    [Required, StringLength(64)]
    public string RequestId { get; init; } = "";

    public WorkflowDecision Decision { get; init; }

    [StringLength(2000)]
    public string? Opinion { get; init; }
}

public sealed record WorkflowInstanceDto(
    long Id,
    string WorkflowType,
    string BusinessType,
    long BusinessId,
    string? CurrentNodeCode,
    WorkflowStatus Status,
    int Version,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<WorkflowNodeDto> Nodes,
    IReadOnlyList<WorkflowRecordDto> Records);

public sealed record WorkflowNodeDto(
    long Id,
    string NodeCode,
    string NodeName,
    int SequenceNo,
    WorkflowApprovalMode ApprovalMode,
    long? AssigneeUserId,
    WorkflowNodeStatus Status,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public sealed record WorkflowRecordDto(
    long Id,
    long? FromNodeId,
    long? ToNodeId,
    string Action,
    long? OperatorUserId,
    string? Opinion,
    string? RequestId,
    DateTime OperatedAt);
