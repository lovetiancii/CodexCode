using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Workflows;

namespace Tianci.OA.Application.Modules.Workflows;

public sealed class WorkflowService(
    IRepository<WorkflowInstance> instances,
    IRepository<WorkflowNode> nodes,
    IRepository<WorkflowRecord> records,
    IUnitOfWork unitOfWork,
    ISnowflakeIdGenerator ids,
    IClock clock,
    ICurrentUser currentUser) : IWorkflowService
{
    private const string StartAction = "start";
    private const string ApproveAction = "approve";
    private const string RejectAction = "reject";

    public async Task<WorkflowInstanceDto> StartAsync(
        StartWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var workflowType = RequiredTrimmed(request.WorkflowType, nameof(request.WorkflowType), 64);
        var businessType = RequiredTrimmed(request.BusinessType, nameof(request.BusinessType), 64);
        var requestId = RequiredTrimmed(request.RequestId, nameof(request.RequestId), 64);
        ValidateStartRequest(request);

        var existing = await instances.FirstAsync(
            x => !x.IsDeleted
                 && x.WorkflowType == workflowType
                 && x.BusinessType == businessType
                 && x.BusinessId == request.BusinessId,
            cancellationToken);
        if (existing is not null)
        {
            var replay = await records.FirstAsync(
                x => x.InstanceId == existing.Id && x.RequestId == requestId,
                cancellationToken);
            if (replay?.Action == StartAction)
                return await GetAsync(existing.Id, cancellationToken);

            throw new ConflictException("该业务已启动同类型流程。", "WORKFLOW_ALREADY_EXISTS");
        }

        await unitOfWork.BeginAsync();
        var committed = false;
        long createdInstanceId;
        try
        {
            var now = clock.UtcNow;
            var instance = new WorkflowInstance
            {
                WorkflowType = workflowType,
                BusinessType = businessType,
                BusinessId = request.BusinessId,
                Status = WorkflowStatus.Running,
                StartedAt = now
            };
            EntityAudit.Create(instance, ids, clock, currentUser);

            var orderedRequests = request.Nodes.OrderBy(x => x.SequenceNo).ToArray();
            var workflowNodes = orderedRequests.Select((node, index) =>
            {
                var entity = new WorkflowNode
                {
                    InstanceId = instance.Id,
                    NodeCode = node.NodeCode.Trim(),
                    NodeName = node.NodeName.Trim(),
                    SequenceNo = node.SequenceNo,
                    ApprovalMode = node.ApprovalMode,
                    AssigneeUserId = node.AssigneeUserId,
                    Status = index == 0 ? WorkflowNodeStatus.Active : WorkflowNodeStatus.Pending,
                    StartedAt = index == 0 ? now : null
                };
                EntityAudit.Create(entity, ids, clock, currentUser);
                return entity;
            }).ToArray();

            instance.CurrentNodeCode = workflowNodes[0].NodeCode;
            await instances.InsertAsync(instance, cancellationToken);
            await nodes.InsertRangeAsync(workflowNodes, cancellationToken);
            await records.InsertAsync(new WorkflowRecord
            {
                Id = ids.NextId(),
                InstanceId = instance.Id,
                ToNodeId = workflowNodes[0].Id,
                Action = StartAction,
                OperatorUserId = userId,
                RequestId = requestId,
                OperatedAt = now,
                CreatedAt = now,
                CreatedBy = userId
            }, cancellationToken);

            await unitOfWork.CommitAsync();
            committed = true;
            createdInstanceId = instance.Id;
        }
        catch
        {
            if (!committed)
                await unitOfWork.RollbackAsync();

            // A concurrent starter can win the unique business/request constraints.
            // Treat that case as the same successful idempotent request.
            var racedInstance = await instances.FirstAsync(
                x => !x.IsDeleted
                     && x.WorkflowType == workflowType
                     && x.BusinessType == businessType
                     && x.BusinessId == request.BusinessId,
                cancellationToken);
            if (racedInstance is not null)
            {
                var racedRecord = await records.FirstAsync(
                    x => x.InstanceId == racedInstance.Id
                         && x.RequestId == requestId
                         && x.Action == StartAction,
                    cancellationToken);
                if (racedRecord is not null)
                    return await GetAsync(racedInstance.Id, cancellationToken);
            }

            throw;
        }

        return await GetAsync(createdInstanceId, cancellationToken);
    }

    public async Task<WorkflowInstanceDto> ApproveNodeAsync(
        long instanceId,
        long nodeId,
        ApproveWorkflowNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var requestId = RequiredTrimmed(request.RequestId, nameof(request.RequestId), 64);
        var action = request.Decision switch
        {
            WorkflowDecision.Pass => ApproveAction,
            WorkflowDecision.Reject => RejectAction,
            _ => throw new BusinessException("审批决定无效。", "INVALID_WORKFLOW_DECISION")
        };

        var replay = await records.FirstAsync(
            x => x.InstanceId == instanceId && x.RequestId == requestId,
            cancellationToken);
        if (replay is not null)
        {
            if (replay.FromNodeId == nodeId && replay.Action == action)
                return await GetAsync(instanceId, cancellationToken);

            throw new ConflictException("requestId 已被其他工作流操作使用。", "IDEMPOTENCY_KEY_REUSED");
        }

        var instance = await instances.FirstAsync(
            x => x.Id == instanceId && !x.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("工作流实例不存在。");
        var currentNode = await nodes.FirstAsync(
            x => x.Id == nodeId && x.InstanceId == instanceId && !x.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("工作流节点不存在。");

        EnsureCanApprove(instance, currentNode, userId);

        await unitOfWork.BeginAsync();
        var committed = false;
        var idempotentReplay = false;
        try
        {
            // The second check closes the normal retry window after entering the transaction.
            replay = await records.FirstAsync(
                x => x.InstanceId == instanceId && x.RequestId == requestId,
                cancellationToken);
            if (replay is not null)
            {
                if (replay.FromNodeId == nodeId && replay.Action == action)
                {
                    await unitOfWork.CommitAsync();
                    committed = true;
                    idempotentReplay = true;
                }
                else
                {
                    throw new ConflictException(
                        "requestId 已被其他工作流操作使用。",
                        "IDEMPOTENCY_KEY_REUSED");
                }
            }

            if (!idempotentReplay)
            {
                var now = clock.UtcNow;
                var expectedVersion = instance.Version;
                currentNode.Status = request.Decision == WorkflowDecision.Pass
                    ? WorkflowNodeStatus.Passed
                    : WorkflowNodeStatus.Rejected;
                currentNode.CompletedAt = now;
                EntityAudit.Update(currentNode, clock, currentUser);
                var updated = await nodes.UpdateWhereAsync(
                    currentNode,
                    x => x.Id == nodeId
                         && x.InstanceId == instanceId
                         && x.Status == WorkflowNodeStatus.Active,
                    cancellationToken);
                if (updated != 1)
                    throw new ConflictException(
                        "该节点已被处理，请刷新后重试。",
                        "WORKFLOW_NODE_ALREADY_HANDLED");

                long? toNodeId = null;
                if (request.Decision == WorkflowDecision.Reject)
                {
                    await CancelRemainingNodesAsync(instanceId, nodeId, now, cancellationToken);
                    instance.Status = WorkflowStatus.Rejected;
                    instance.CurrentNodeCode = null;
                    instance.CompletedAt = now;
                }
                else
                {
                    var allNodes = await nodes.ListAsync(
                        x => x.InstanceId == instanceId && !x.IsDeleted,
                        cancellationToken);
                    var nextNode = allNodes
                        .Where(x => x.Status == WorkflowNodeStatus.Pending
                                    && x.SequenceNo > currentNode.SequenceNo)
                        .OrderBy(x => x.SequenceNo)
                        .FirstOrDefault();
                    if (nextNode is null)
                    {
                        instance.Status = WorkflowStatus.Completed;
                        instance.CurrentNodeCode = null;
                        instance.CompletedAt = now;
                    }
                    else
                    {
                        nextNode.Status = WorkflowNodeStatus.Active;
                        nextNode.StartedAt = now;
                        EntityAudit.Update(nextNode, clock, currentUser);
                        var activated = await nodes.UpdateWhereAsync(
                            nextNode,
                            x => x.Id == nextNode.Id && x.Status == WorkflowNodeStatus.Pending,
                            cancellationToken);
                        if (activated != 1)
                            throw new ConflictException(
                                "下一审批节点状态已变化，请刷新后重试。",
                                "WORKFLOW_CONCURRENT_UPDATE");

                        instance.CurrentNodeCode = nextNode.NodeCode;
                        toNodeId = nextNode.Id;
                    }
                }

                instance.Version = expectedVersion + 1;
                EntityAudit.Update(instance, clock, currentUser);
                var instanceUpdated = await instances.UpdateWhereAsync(
                    instance,
                    x => x.Id == instanceId
                         && x.Status == WorkflowStatus.Running
                         && x.Version == expectedVersion
                         && x.CurrentNodeCode == currentNode.NodeCode,
                    cancellationToken);
                if (instanceUpdated != 1)
                    throw new ConflictException(
                        "流程状态已变化，请刷新后重试。",
                        "WORKFLOW_CONCURRENT_UPDATE");

                await records.InsertAsync(new WorkflowRecord
                {
                    Id = ids.NextId(),
                    InstanceId = instanceId,
                    FromNodeId = currentNode.Id,
                    ToNodeId = toNodeId,
                    Action = action,
                    OperatorUserId = userId,
                    Opinion = NullIfWhiteSpace(request.Opinion),
                    RequestId = requestId,
                    OperatedAt = now,
                    CreatedAt = now,
                    CreatedBy = userId
                }, cancellationToken);

                await unitOfWork.CommitAsync();
                committed = true;
            }
        }
        catch
        {
            if (!committed)
                await unitOfWork.RollbackAsync();

            // If another copy of the same request completed while this transaction
            // was waiting on a row lock, return its result instead of a conflict.
            var concurrentReplay = await records.FirstAsync(
                x => x.InstanceId == instanceId
                     && x.RequestId == requestId
                     && x.FromNodeId == nodeId
                     && x.Action == action,
                cancellationToken);
            if (concurrentReplay is not null)
                return await GetAsync(instanceId, cancellationToken);

            throw;
        }

        return await GetAsync(instanceId, cancellationToken);
    }

    public async Task<WorkflowInstanceDto> GetAsync(
        long instanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = await instances.FirstAsync(
            x => x.Id == instanceId && !x.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("工作流实例不存在。");
        var workflowNodes = await nodes.ListAsync(
            x => x.InstanceId == instanceId && !x.IsDeleted,
            cancellationToken);
        var workflowRecords = await records.ListAsync(
            x => x.InstanceId == instanceId,
            cancellationToken);

        return new WorkflowInstanceDto(
            instance.Id,
            instance.WorkflowType,
            instance.BusinessType,
            instance.BusinessId,
            instance.CurrentNodeCode,
            instance.Status,
            instance.Version,
            instance.StartedAt,
            instance.CompletedAt,
            workflowNodes.OrderBy(x => x.SequenceNo).Select(MapNode).ToArray(),
            workflowRecords.OrderBy(x => x.OperatedAt).ThenBy(x => x.Id).Select(MapRecord).ToArray());
    }

    private async Task CancelRemainingNodesAsync(
        long instanceId,
        long rejectedNodeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var remaining = await nodes.ListAsync(
            x => x.InstanceId == instanceId
                 && x.Id != rejectedNodeId
                 && !x.IsDeleted
                 && (x.Status == WorkflowNodeStatus.Pending || x.Status == WorkflowNodeStatus.Active),
            cancellationToken);
        foreach (var node in remaining)
        {
            node.Status = WorkflowNodeStatus.Cancelled;
            node.CompletedAt = now;
            EntityAudit.Update(node, clock, currentUser);
            await nodes.UpdateAsync(node, cancellationToken);
        }
    }

    private static void EnsureCanApprove(
        WorkflowInstance instance,
        WorkflowNode node,
        long userId)
    {
        if (instance.Status != WorkflowStatus.Running)
            throw new ConflictException("该流程已结束，不能继续审批。", "WORKFLOW_NOT_RUNNING");
        if (node.Status != WorkflowNodeStatus.Active || instance.CurrentNodeCode != node.NodeCode)
            throw new ConflictException("该节点不是当前活动审批节点。", "WORKFLOW_NODE_NOT_ACTIVE");
        if (node.AssigneeUserId.HasValue && node.AssigneeUserId.Value != userId)
            throw new ForbiddenException("当前用户不是该节点的审批人。");
    }

    private static void ValidateStartRequest(StartWorkflowRequest request)
    {
        if (request.BusinessId <= 0)
            throw new BusinessException("业务 ID 必须大于 0。", "INVALID_BUSINESS_ID");
        if (request.Nodes is null || request.Nodes.Count == 0)
            throw new BusinessException("工作流至少需要一个审批节点。", "WORKFLOW_NODES_REQUIRED");
        if (request.Nodes.Any(x => x.SequenceNo <= 0))
            throw new BusinessException("节点顺序必须大于 0。", "INVALID_NODE_SEQUENCE");
        if (request.Nodes.Any(x => string.IsNullOrWhiteSpace(x.NodeCode) || x.NodeCode.Trim().Length > 64))
            throw new BusinessException("节点编码不能为空且不能超过 64 个字符。", "INVALID_NODE_CODE");
        if (request.Nodes.Any(x => string.IsNullOrWhiteSpace(x.NodeName) || x.NodeName.Trim().Length > 100))
            throw new BusinessException("节点名称不能为空且不能超过 100 个字符。", "INVALID_NODE_NAME");
        if (request.Nodes.Select(x => x.SequenceNo).Distinct().Count() != request.Nodes.Count)
            throw new BusinessException("节点顺序不能重复。", "DUPLICATE_NODE_SEQUENCE");
        if (request.Nodes.Select(x => x.NodeCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != request.Nodes.Count)
            throw new BusinessException("节点编码不能重复。", "DUPLICATE_NODE_CODE");
        if (request.Nodes.Any(x => !Enum.IsDefined(x.ApprovalMode)))
            throw new BusinessException("节点审批模式无效。", "INVALID_APPROVAL_MODE");
        if (request.Nodes.Any(x => x.AssigneeUserId <= 0))
            throw new BusinessException("审批人 ID 必须大于 0。", "INVALID_ASSIGNEE");
    }

    private long RequireUser()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not long userId || userId <= 0)
            throw new ForbiddenException("请登录后执行工作流操作。");
        return userId;
    }

    private static string RequiredTrimmed(string value, string fieldName, int maxLength)
    {
        var result = value?.Trim() ?? "";
        if (result.Length == 0 || result.Length > maxLength)
            throw new BusinessException($"{fieldName} 不能为空且不能超过 {maxLength} 个字符。", "INVALID_ARGUMENT");
        return result;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var result = value.Trim();
        if (result.Length > 2000)
            throw new BusinessException("审批意见不能超过 2000 个字符。", "INVALID_OPINION");
        return result;
    }

    private static WorkflowNodeDto MapNode(WorkflowNode node) =>
        new(
            node.Id,
            node.NodeCode,
            node.NodeName,
            node.SequenceNo,
            node.ApprovalMode,
            node.AssigneeUserId,
            node.Status,
            node.StartedAt,
            node.CompletedAt);

    private static WorkflowRecordDto MapRecord(WorkflowRecord record) =>
        new(
            record.Id,
            record.FromNodeId,
            record.ToNodeId,
            record.Action,
            record.OperatorUserId,
            record.Opinion,
            record.RequestId,
            record.OperatedAt);
}
