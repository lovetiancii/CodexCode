using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Recruitment;
using Tianci.OA.Domain.Common;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController, Authorize, Route("api/v1/resumes")]
public sealed class ResumesController(IRecruitmentService service) : ControllerBase
{
    [HttpGet, Permission("resume:view")]
    public Task<PagedResult<ResumeDto>> List([FromQuery] string? keyword, [FromQuery] string? positionId, [FromQuery] ResumeStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync(new(keyword, positionId, status, pageNumber, pageSize), ct);
    [HttpGet("{id}"), Permission("resume:view")] public Task<ResumeDto> Get(string id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost, Permission("resume:create")] public Task<ResumeDto> Create(ResumeRequest request, CancellationToken ct) => service.CreateAsync(request, ct);
    [HttpPut("{id}"), Permission("resume:edit")] public Task<ResumeDto> Update(string id, ResumeRequest request, [FromQuery] int version, CancellationToken ct) => service.UpdateAsync(id, request, version, ct);
    [HttpPost("{id}/status"), Permission("resume:manage")] public async Task<IActionResult> Status(string id, ChangeResumeStatusRequest request, CancellationToken ct) { await service.ChangeStatusAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id}/interviews"), Permission("resume:interview")] public Task<InterviewDto> Schedule(string id, ScheduleInterviewRequest request, CancellationToken ct) => service.ScheduleInterviewAsync(id, request, ct);
    [HttpGet("{id}/interviews"), Permission("resume:view")] public Task<IReadOnlyList<InterviewDto>> Interviews(string id, CancellationToken ct) => service.InterviewsAsync(id, ct);
    [HttpPost("{id}/interviews/{interviewId}/complete"), Permission("resume:interview")] public Task<InterviewDto> Complete(string id, string interviewId, CompleteInterviewRequest request, CancellationToken ct) => service.CompleteInterviewAsync(id, interviewId, request, ct);
    [HttpPost("{id}/confirm-offer"), Permission("resume:hire")] public async Task<IActionResult> Offer(string id, ConfirmOfferRequest request, CancellationToken ct) { await service.ConfirmOfferAsync(id, request, ct); return NoContent(); }
    [HttpPost("{id}/confirm-entry"), Permission("resume:hire")] public async Task<object> Entry(string id, ConfirmEntryRequest request, CancellationToken ct) => new { employeeId = await service.ConfirmEntryAsync(id, request, ct) };
}
