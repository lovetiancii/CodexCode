using Tianci.OA.Application.Common;

namespace Tianci.OA.Application.Modules.Recruitment;

public interface IRecruitmentService
{
    Task<PagedResult<ResumeDto>> ListAsync(
        ResumeQuery query,
        CancellationToken cancellationToken);

    Task<ResumeDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<ResumeDto> CreateAsync(
        ResumeRequest request,
        CancellationToken cancellationToken);

    Task<ResumeDto> UpdateAsync(
        string id,
        ResumeRequest request,
        int version,
        CancellationToken cancellationToken);

    Task<ResumeDto> SetAttachmentAsync(
        string id,
        ResumeAttachmentRequest request,
        CancellationToken cancellationToken);

    Task ChangeStatusAsync(
        string id,
        ChangeResumeStatusRequest request,
        CancellationToken cancellationToken);

    Task<InterviewDto> ScheduleInterviewAsync(
        string resumeId,
        ScheduleInterviewRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InterviewerOptionDto>> InterviewerOptionsAsync(
        string resumeId,
        string? keyword,
        bool sameDepartmentOnly,
        CancellationToken cancellationToken);

    Task<InterviewDto> CompleteInterviewAsync(
        string resumeId,
        string interviewId,
        CompleteInterviewRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InterviewDto>> InterviewsAsync(
        string resumeId,
        CancellationToken cancellationToken);

    Task ConfirmOfferAsync(
        string resumeId,
        ConfirmOfferRequest request,
        CancellationToken cancellationToken);

    Task<string> ConfirmEntryAsync(
        string resumeId,
        ConfirmEntryRequest request,
        CancellationToken cancellationToken);
}
