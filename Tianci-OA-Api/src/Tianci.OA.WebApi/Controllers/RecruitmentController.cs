using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Recruitment;
using Tianci.OA.Domain.Common;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/resumes")]
public sealed class ResumesController : ControllerBase
{
    private readonly IRecruitmentService _recruitmentService;

    public ResumesController(IRecruitmentService recruitmentService)
    {
        _recruitmentService = recruitmentService;
    }

    [HttpGet]
    [Permission("resume:view")]
    public Task<PagedResult<ResumeDto>> List(
        [FromQuery] string? keyword,
        [FromQuery] string? positionId,
        [FromQuery] ResumeStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ResumeQuery(
            keyword,
            positionId,
            status,
            pageNumber,
            pageSize);

        return _recruitmentService.ListAsync(query, cancellationToken);
    }

    [HttpGet("{id}")]
    [Permission("resume:view")]
    public Task<ResumeDto> Get(
        string id,
        CancellationToken cancellationToken)
    {
        return _recruitmentService.GetAsync(id, cancellationToken);
    }

    [HttpPost]
    [Permission("resume:create")]
    public Task<ResumeDto> Create(
        ResumeRequest request,
        CancellationToken cancellationToken)
    {
        return _recruitmentService.CreateAsync(request, cancellationToken);
    }

    [HttpPut("{id}")]
    [Permission("resume:edit")]
    public Task<ResumeDto> Update(
        string id,
        ResumeRequest request,
        [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        return _recruitmentService.UpdateAsync(
            id,
            request,
            version,
            cancellationToken);
    }

    [HttpPost("{id}/status")]
    [Permission("resume:manage")]
    public async Task<IActionResult> ChangeStatus(
        string id,
        ChangeResumeStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _recruitmentService.ChangeStatusAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpGet("{id}/interviewers")]
    [Permission("resume:interview")]
    public Task<IReadOnlyList<InterviewerOptionDto>> Interviewers(
        string id,
        [FromQuery] string? keyword,
        [FromQuery] bool sameDepartmentOnly = true,
        CancellationToken cancellationToken = default)
    {
        return _recruitmentService.InterviewerOptionsAsync(
            id,
            keyword,
            sameDepartmentOnly,
            cancellationToken);
    }

    [HttpPost("{id}/interviews")]
    [Permission("resume:interview")]
    public Task<InterviewDto> ScheduleInterview(
        string id,
        ScheduleInterviewRequest request,
        CancellationToken cancellationToken)
    {
        return _recruitmentService.ScheduleInterviewAsync(
            id,
            request,
            cancellationToken);
    }

    [HttpGet("{id}/interviews")]
    [Permission("resume:view")]
    public Task<IReadOnlyList<InterviewDto>> Interviews(
        string id,
        CancellationToken cancellationToken)
    {
        return _recruitmentService.InterviewsAsync(id, cancellationToken);
    }

    [HttpPost("{id}/interviews/{interviewId}/complete")]
    [Permission("resume:interview")]
    public Task<InterviewDto> CompleteInterview(
        string id,
        string interviewId,
        CompleteInterviewRequest request,
        CancellationToken cancellationToken)
    {
        return _recruitmentService.CompleteInterviewAsync(
            id,
            interviewId,
            request,
            cancellationToken);
    }

    [HttpPost("{id}/confirm-offer")]
    [Permission("resume:hire")]
    public async Task<IActionResult> ConfirmOffer(
        string id,
        ConfirmOfferRequest request,
        CancellationToken cancellationToken)
    {
        await _recruitmentService.ConfirmOfferAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id}/confirm-entry")]
    [Permission("resume:hire")]
    public async Task<object> ConfirmEntry(
        string id,
        ConfirmEntryRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = await _recruitmentService.ConfirmEntryAsync(
            id,
            request,
            cancellationToken);

        return new
        {
            employeeId
        };
    }
}
