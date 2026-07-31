namespace Tianci.OA.Application.Modules.Workflows;

public interface IWorkflowService
{
    Task<WorkflowInstanceDto> StartAsync(
        StartWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDto> ApproveNodeAsync(
        long instanceId,
        long nodeId,
        ApproveWorkflowNodeRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDto> GetAsync(
        long instanceId,
        CancellationToken cancellationToken = default);
}
