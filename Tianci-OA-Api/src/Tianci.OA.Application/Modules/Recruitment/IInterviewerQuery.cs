namespace Tianci.OA.Application.Modules.Recruitment;

public interface IInterviewerQuery
{
    Task<IReadOnlyList<InterviewerOptionDto>> SearchAsync(
        long departmentId,
        string? keyword,
        bool sameDepartmentOnly,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> IsEligibleAsync(
        long userId,
        CancellationToken cancellationToken);
}
