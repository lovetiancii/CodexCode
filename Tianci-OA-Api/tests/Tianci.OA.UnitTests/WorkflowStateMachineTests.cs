using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Workflows;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Workflows;

namespace Tianci.OA.UnitTests;

public sealed class WorkflowStateMachineTests
{
    [Fact]
    public async Task Start_activates_first_node_and_records_idempotency_key()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.StartAsync(Request());

        Assert.Equal(WorkflowStatus.Running, result.Status);
        Assert.Equal("hr-review", result.CurrentNodeCode);
        Assert.Equal(WorkflowNodeStatus.Active, result.Nodes[0].Status);
        Assert.Equal(WorkflowNodeStatus.Pending, result.Nodes[1].Status);
        Assert.Equal("start-001", Assert.Single(result.Records).RequestId);
        Assert.Equal(1, fixture.UnitOfWork.Commits);
        Assert.Equal(0, fixture.UnitOfWork.Rollbacks);
    }

    [Fact]
    public async Task Passing_all_nodes_completes_workflow()
    {
        var fixture = new Fixture();
        var started = await fixture.Service.StartAsync(Request());

        var afterFirst = await fixture.Service.ApproveNodeAsync(
            started.Id,
            started.Nodes[0].Id,
            new ApproveWorkflowNodeRequest { RequestId = "approve-001", Decision = WorkflowDecision.Pass });
        var completed = await fixture.Service.ApproveNodeAsync(
            afterFirst.Id,
            afterFirst.Nodes[1].Id,
            new ApproveWorkflowNodeRequest { RequestId = "approve-002", Decision = WorkflowDecision.Pass });

        Assert.Equal("manager-review", afterFirst.CurrentNodeCode);
        Assert.Equal(WorkflowNodeStatus.Active, afterFirst.Nodes[1].Status);
        Assert.Equal(WorkflowStatus.Completed, completed.Status);
        Assert.Null(completed.CurrentNodeCode);
        Assert.NotNull(completed.CompletedAt);
        Assert.All(completed.Nodes, node => Assert.Equal(WorkflowNodeStatus.Passed, node.Status));
    }

    [Fact]
    public async Task Rejecting_current_node_rejects_workflow_and_cancels_remaining_nodes()
    {
        var fixture = new Fixture();
        var started = await fixture.Service.StartAsync(Request());

        var result = await fixture.Service.ApproveNodeAsync(
            started.Id,
            started.Nodes[0].Id,
            new ApproveWorkflowNodeRequest { RequestId = "reject-001", Decision = WorkflowDecision.Reject, Opinion = "资料不完整" });

        Assert.Equal(WorkflowStatus.Rejected, result.Status);
        Assert.Equal(WorkflowNodeStatus.Rejected, result.Nodes[0].Status);
        Assert.Equal(WorkflowNodeStatus.Cancelled, result.Nodes[1].Status);
        Assert.Equal("资料不完整", result.Records.Last().Opinion);
    }

    [Fact]
    public async Task Duplicate_node_sequence_is_rejected_before_transaction()
    {
        var fixture = new Fixture();
        var request = Request().WithNodes(
        [
            new() { NodeCode = "a", NodeName = "A", SequenceNo = 1 },
            new() { NodeCode = "b", NodeName = "B", SequenceNo = 1 }
        ]);

        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.StartAsync(request));

        Assert.Equal("DUPLICATE_NODE_SEQUENCE", error.Code);
        Assert.Equal(0, fixture.UnitOfWork.Begins);
    }

    private static StartWorkflowRequest Request()
    {
        return new()
        {
            WorkflowType = "employee-entry",
            BusinessType = "resume",
            BusinessId = 88,
            RequestId = "start-001",
            Nodes =
        [
            new() { NodeCode = "hr-review", NodeName = "HR 审核", SequenceNo = 1, AssigneeUserId = 7 },
            new() { NodeCode = "manager-review", NodeName = "负责人审核", SequenceNo = 2, AssigneeUserId = 7 }
        ]
        };
    }

    private sealed class Fixture
    {
        public TrackingUnitOfWork UnitOfWork { get; } = new();
        public WorkflowService Service
        {
            get;
        }

        public Fixture()
        {
            Service = new WorkflowService(
                new InMemoryRepository<WorkflowInstance>(),
                new InMemoryRepository<WorkflowNode>(),
                new InMemoryRepository<WorkflowRecord>(),
                UnitOfWork,
                new StubIds(),
                new StubClock(),
                new StubCurrentUser());
        }
    }
}

file static class WorkflowRequestExtensions
{
    public static StartWorkflowRequest WithNodes(
        this StartWorkflowRequest request,
        IReadOnlyList<WorkflowNodeRequest> nodes)
    {
        return new()
        {
            WorkflowType = request.WorkflowType,
            BusinessType = request.BusinessType,
            BusinessId = request.BusinessId,
            RequestId = request.RequestId,
            Nodes = nodes
        };
    }
}
