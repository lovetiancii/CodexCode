using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Workflows;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Permission("workflow:manage")]
[Route("api/v1/workflows")]
public sealed class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowsController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    /// <summary>启动工作流并激活首个审批节点。</summary>
    [HttpPost]
    [ProducesResponseType<WorkflowInstanceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowInstanceDto>> Start(
        [FromBody] StartWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.StartAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new
            {
                instanceId = result.Id
            },
            result);
    }

    /// <summary>通过或拒绝当前活动节点；相同 requestId 可安全重试。</summary>
    [HttpPost("{instanceId:long}/nodes/{nodeId:long}/decision")]
    [ProducesResponseType<WorkflowInstanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowInstanceDto>> Decide(
        long instanceId,
        long nodeId,
        [FromBody] ApproveWorkflowNodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.ApproveNodeAsync(
            instanceId,
            nodeId,
            request,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>查询工作流实例、节点快照与审批记录。</summary>
    [HttpGet("{instanceId:long}")]
    [ProducesResponseType<WorkflowInstanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowInstanceDto>> Get(
        long instanceId,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.GetAsync(
            instanceId,
            cancellationToken);

        return Ok(result);
    }
}
