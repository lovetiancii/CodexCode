using System.Linq.Expressions;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Organization;
using Tianci.OA.Domain.Recruitment;

namespace Tianci.OA.Application.Modules.Recruitment;

public sealed class RecruitmentService(
    IRepository<Resume> resumes,
    IRepository<InterviewRecord> interviews,
    IRepository<EmployeeEntry> entries,
    IRepository<Employee> employees,
    IRepository<Department> departments,
    IRepository<Position> positions,
    IInterviewerQuery interviewerQuery,
    ISensitiveDataProtector protector,
    ISnowflakeIdGenerator ids,
    IClock clock,
    ICurrentUser currentUser,
    IUnitOfWork uow) : IRecruitmentService
{
    public async Task<PagedResult<ResumeDto>> ListAsync(ResumeQuery q, CancellationToken ct)
    {
        var keyword = q.Keyword?.Trim() ?? "";
        var pid = IdParser.ParseNullable(q.PositionId, "positionId");
        var page = new PageRequest(q.PageNumber, q.PageSize);
        Expression<Func<Resume, bool>> predicate = resume =>
            !resume.IsDeleted
            && (keyword == ""
                || resume.Name.Contains(keyword)
                || resume.Phone.Contains(keyword)
                || (resume.Email != null && resume.Email.Contains(keyword)));

        if (pid.HasValue)
        {
            var positionId = pid.Value;
            predicate = predicate.And(x => x.AppliedPositionId == positionId);
        }
        if (q.Status.HasValue)
        {
            var status = q.Status.Value;
            predicate = predicate.And(x => x.Status == status);
        }
        var (items, total) = await resumes.PageAsync(
            predicate,
            page.SafePageNumber,
            page.SafePageSize,
            resume => resume.UpdatedAt,
            true,
            ct);

        return new PagedResult<ResumeDto>(
            [.. items.Select(ToDto)],
            page.SafePageNumber,
            page.SafePageSize,
            total);
    }

    public async Task<ResumeDto> GetAsync(string id, CancellationToken ct)
    {
        return ToDto(await RequiredResume(id, ct));
    }

    public async Task<ResumeDto> CreateAsync(ResumeRequest r, CancellationToken ct)
    {
        await ValidateResumeAsync(r, ct);
        var next = ids.NextId();
        var e = new Resume
        {
            CandidateNo = $"CV{clock.UtcNow:yyyyMMdd}{next % 100000000:00000000}",
            Status = ResumeStatus.Submitted
        };
        Apply(e, r);
        EntityAudit.Create(e, new FixedIdGenerator(next), clock, currentUser);
        await resumes.InsertAsync(e, ct);
        return ToDto(e);
    }
    public async Task<ResumeDto> UpdateAsync(string id, ResumeRequest r, int version, CancellationToken ct)
    {
        var e = await RequiredResume(id, ct);
        if (e.Status is ResumeStatus.Hired)
        {
            throw new ConflictException("已入职简历不可编辑");
        }

        if (e.Version != version)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        await ValidateResumeAsync(r, ct);
        Apply(e, r);
        var old = e.Version;
        e.Version++;
        EntityAudit.Update(e, clock, currentUser);
        await EnsureOptimisticAsync(resumes.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct));
        return ToDto(e);
    }
    public async Task ChangeStatusAsync(string id, ChangeResumeStatusRequest r, CancellationToken ct)
    {
        var e = await RequiredResume(id, ct);
        if (e.Version != r.Version)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        var allowed = (e.Status, r.TargetStatus) switch
        {
            (ResumeStatus.Submitted, ResumeStatus.Screening) => true,
            (ResumeStatus.Submitted or ResumeStatus.Screening, ResumeStatus.InterviewPending) => true,
            (ResumeStatus.Submitted
                or ResumeStatus.Screening
                or ResumeStatus.InterviewPending,
                ResumeStatus.Rejected) => !string.IsNullOrWhiteSpace(r.Reason),
            (ResumeStatus.Rejected, ResumeStatus.Submitted) => true,
            (ResumeStatus.OfferPending, ResumeStatus.OfferDeclined) => !string.IsNullOrWhiteSpace(r.Reason),
            (ResumeStatus.EntryPending, ResumeStatus.OfferDeclined) => !string.IsNullOrWhiteSpace(r.Reason),
            _ => false
        };
        if (!allowed)
        {
            throw new ConflictException("不允许的招聘状态流转", "INVALID_STATE_TRANSITION");
        }

        var old = e.Version;
        e.Status = r.TargetStatus;
        e.RejectReason = r.TargetStatus is ResumeStatus.Rejected or ResumeStatus.OfferDeclined
            ? r.Reason!.Trim()
            : null;
        e.Version++;
        EntityAudit.Update(e, clock, currentUser);
        await EnsureOptimisticAsync(resumes.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct));
    }
    public async Task<InterviewDto> ScheduleInterviewAsync(string resumeId, ScheduleInterviewRequest r, CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct);
        if (resume.Status is not (ResumeStatus.InterviewPending or ResumeStatus.Screening))
        {
            throw new ConflictException("当前状态不可安排面试", "INVALID_STATE_TRANSITION");
        }

        if (resume.Version != r.ResumeVersion)
        {
            throw new ConflictException("数据已被其他用户修改");
        }

        if (r.ScheduledAt.ToUniversalTime() <= clock.UtcNow)
        {
            throw new BusinessException("面试时间必须晚于当前时间");
        }

        var interviewerId = IdParser.Parse(r.InterviewerUserId, "interviewerUserId");
        if (!await interviewerQuery.IsEligibleAsync(interviewerId, ct))
        {
            throw new NotFoundException("面试官不存在、未绑定在职员工或账号未启用");
        }

        if (await interviews.ExistsAsync(
            interview => interview.ResumeId == resume.Id
                && interview.RoundNo == r.RoundNo
                && !interview.IsDeleted,
            ct))
        {
            throw new ConflictException("该轮面试已存在");
        }

        var interview = new InterviewRecord
        {
            ResumeId = resume.Id,
            RoundNo = r.RoundNo,
            InterviewerUserId = interviewerId,
            ScheduledAt = r.ScheduledAt.ToUniversalTime(),
            Location = r.Location,
            Remark = r.Remark,
            Conclusion = InterviewConclusion.Pending
        };
        EntityAudit.Create(interview, ids, clock, currentUser);
        await uow.BeginAsync();
        try
        {
            await interviews.InsertAsync(interview, ct);
            var old = resume.Version;
            resume.Status = ResumeStatus.Interviewing;
            resume.CurrentRound = r.RoundNo;
            resume.Version++;
            EntityAudit.Update(resume, clock, currentUser);
            await EnsureOptimisticAsync(resumes.UpdateWhereAsync(
                resume,
                entity => entity.Id == resume.Id && entity.Version == old,
                ct));
            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
        return ToDto(interview);
    }
    public async Task<IReadOnlyList<InterviewerOptionDto>> InterviewerOptionsAsync(
        string resumeId,
        string? keyword,
        bool sameDepartmentOnly,
        CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct);
        var appliedPosition = await positions.FirstAsync(
                position => position.Id == resume.AppliedPositionId
                    && !position.IsDeleted,
                ct)
            ?? throw new NotFoundException("应聘岗位不存在");

        return await interviewerQuery.SearchAsync(
            appliedPosition.DepartmentId,
            keyword,
            sameDepartmentOnly,
            50,
            ct);
    }

    public async Task<InterviewDto> CompleteInterviewAsync(
        string resumeId,
        string interviewId,
        CompleteInterviewRequest r,
        CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct);
        if (resume.Status != ResumeStatus.Interviewing || resume.Version != r.ResumeVersion)
        {
            throw new ConflictException("简历状态或版本不允许提交评价");
        }

        var parsedInterviewId = IdParser.Parse(interviewId);
        var interview = await interviews.FirstAsync(
                entity => entity.Id == parsedInterviewId
                    && entity.ResumeId == resume.Id
                    && !entity.IsDeleted,
                ct)
            ?? throw new NotFoundException("面试记录不存在");
        if (interview.Conclusion != InterviewConclusion.Pending && interview.Conclusion != InterviewConclusion.Hold)
        {
            throw new ConflictException("本轮面试已完成");
        }

        if (r.Conclusion == InterviewConclusion.Pending || r.Conclusion == InterviewConclusion.Cancelled)
        {
            throw new BusinessException("请提交明确的面试结论");
        }

        interview.Score = r.Score;
        interview.Evaluation = r.Evaluation.Trim();
        interview.Conclusion = r.Conclusion;
        interview.NextScheduledAt = r.NextScheduledAt?.ToUniversalTime();
        interview.Remark = r.Remark;
        interview.CompletedAt = clock.UtcNow;
        EntityAudit.Update(interview, clock, currentUser);
        var target = r.Conclusion switch
        {
            InterviewConclusion.Fail => ResumeStatus.Rejected,
            InterviewConclusion.Hold => ResumeStatus.Interviewing,
            InterviewConclusion.Pass when r.IsFinalRound => ResumeStatus.OfferPending,
            InterviewConclusion.Pass => ResumeStatus.InterviewPending,
            _ => resume.Status
        };
        await uow.BeginAsync();
        try
        {
            await interviews.UpdateAsync(interview, ct);
            var old = resume.Version;
            resume.Status = target;
            if (target == ResumeStatus.Rejected)
            {
                resume.RejectReason = "面试未通过";
            }

            resume.Version++;
            EntityAudit.Update(resume, clock, currentUser);
            await EnsureOptimisticAsync(resumes.UpdateWhereAsync(
                resume,
                entity => entity.Id == resume.Id && entity.Version == old,
                ct));
            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
        return ToDto(interview);
    }
    public async Task<IReadOnlyList<InterviewDto>> InterviewsAsync(string resumeId, CancellationToken ct)
    {
        var id = IdParser.Parse(resumeId);
        _ = await RequiredResume(resumeId, ct);
        var records = await interviews.ListAsync(
            interview => interview.ResumeId == id && !interview.IsDeleted,
            ct);

        return [.. records.OrderBy(interview => interview.RoundNo).Select(ToDto)];
    }
    public async Task ConfirmOfferAsync(string resumeId, ConfirmOfferRequest r, CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct);
        if (resume.Status != ResumeStatus.OfferPending || resume.Version != r.ResumeVersion)
        {
            throw new ConflictException("仅待录用候选人可确认录用");
        }

        var did = IdParser.Parse(r.DepartmentId, "departmentId");
        var pid = IdParser.Parse(r.PositionId, "positionId");
        var departmentExists = await departments.ExistsAsync(
            department => department.Id == did && !department.IsDeleted,
            ct);
        var positionExists = await positions.ExistsAsync(
            position => position.Id == pid
                && position.DepartmentId == did
                && !position.IsDeleted,
            ct);

        if (!departmentExists || !positionExists)
        {
            throw new NotFoundException("部门或岗位不存在");
        }

        if (await entries.ExistsAsync(x => x.ResumeId == resume.Id && !x.IsDeleted, ct))
        {
            throw new ConflictException("该候选人已存在录用记录");
        }

        var entry = new EmployeeEntry
        {
            ResumeId = resume.Id,
            PlannedEntryDate = r.PlannedEntryDate.Date,
            DepartmentId = did,
            PositionId = pid,
            MonthlySalaryCiphertext = protector.Protect(r.MonthlySalary),
            ProbationMonths = r.ProbationMonths,
            Status = EntryStatus.EntryPending,
            Remark = r.Remark
        };
        EntityAudit.Create(entry, ids, clock, currentUser);
        await uow.BeginAsync();
        try
        {
            await entries.InsertAsync(entry, ct);
            var old = resume.Version;
            resume.Status = ResumeStatus.EntryPending;
            resume.Version++;
            EntityAudit.Update(resume, clock, currentUser);
            await EnsureOptimisticAsync(resumes.UpdateWhereAsync(
                resume,
                entity => entity.Id == resume.Id && entity.Version == old,
                ct));
            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }
    public async Task<string> ConfirmEntryAsync(string resumeId, ConfirmEntryRequest r, CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct);
        if (resume.Status != ResumeStatus.EntryPending || resume.Version != r.ResumeVersion)
        {
            throw new ConflictException("仅待入职候选人可确认到岗");
        }

        var entry = await entries.FirstAsync(
                entity => entity.ResumeId == resume.Id && !entity.IsDeleted,
                ct)
            ?? throw new NotFoundException("录用记录不存在");
        if (entry.Status != EntryStatus.EntryPending || entry.Version != r.EntryVersion || entry.EmployeeId.HasValue)
        {
            throw new ConflictException("入职记录状态或版本冲突");
        }

        if (await employees.ExistsAsync(
            employee => (employee.SourceResumeId == resume.Id
                    || employee.EmployeeNo == r.EmployeeNo)
                && !employee.IsDeleted,
            ct))
        {
            throw new ConflictException("候选人已生成员工或员工编号重复");
        }

        var employee = new Employee
        {
            EmployeeNo = r.EmployeeNo.Trim(),
            SourceResumeId = resume.Id,
            Name = resume.Name,
            Gender = resume.Gender,
            Phone = resume.Phone,
            Email = resume.Email,
            DepartmentId = entry.DepartmentId,
            PositionId = entry.PositionId,
            Status = entry.ProbationMonths > 0
                ? EmployeeStatus.Probation
                : EmployeeStatus.Active,
            EntryDate = r.ActualEntryDate.Date,
            ProbationMonths = entry.ProbationMonths,
            MonthlySalaryCiphertext = entry.MonthlySalaryCiphertext
        };
        EntityAudit.Create(employee, ids, clock, currentUser);
        await uow.BeginAsync();
        try
        {
            await employees.InsertAsync(employee, ct);
            var entryOld = entry.Version;
            entry.EmployeeId = employee.Id;
            entry.ActualEntryDate = r.ActualEntryDate.Date;
            entry.Status = EntryStatus.Entered;
            entry.Version++;
            EntityAudit.Update(entry, clock, currentUser);
            await EnsureOptimisticAsync(entries.UpdateWhereAsync(
                entry,
                entity => entity.Id == entry.Id && entity.Version == entryOld,
                ct));
            var resumeOld = resume.Version;
            resume.Status = ResumeStatus.Hired;
            resume.Version++;
            EntityAudit.Update(resume, clock, currentUser);
            await EnsureOptimisticAsync(resumes.UpdateWhereAsync(
                resume,
                entity => entity.Id == resume.Id && entity.Version == resumeOld,
                ct));
            await uow.CommitAsync();
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
        return employee.Id.ToString();
    }
    private async Task ValidateResumeAsync(ResumeRequest r, CancellationToken ct)
    {
        var pid = IdParser.Parse(r.AppliedPositionId, "appliedPositionId");
        if (!await positions.ExistsAsync(x => x.Id == pid && !x.IsDeleted && x.Status == EnabledStatus.Enabled, ct))
        {
            throw new NotFoundException("应聘岗位不存在或未启用");
        }
    }
    private static void Apply(Resume e, ResumeRequest r)
    {
        e.Name = r.Name.Trim();
        e.Gender = r.Gender;
        e.Phone = r.Phone;
        e.Email = r.Email;
        e.Education = r.Education;
        e.WorkExperience = r.WorkExperience;
        e.Skills = r.Skills;
        e.AppliedPositionId = IdParser.Parse(r.AppliedPositionId, "appliedPositionId");
        e.Source = r.Source;
        e.AttachmentFileId = IdParser.ParseNullable(r.AttachmentFileId, "attachmentFileId");
        e.OwnerUserId = IdParser.ParseNullable(r.OwnerUserId, "ownerUserId");
        e.Remark = r.Remark;
    }
    private async Task<Resume> RequiredResume(string id, CancellationToken ct)
    {
        var resumeId = IdParser.Parse(id);
        return await resumes.FirstAsync(
                resume => resume.Id == resumeId && !resume.IsDeleted,
                ct)
            ?? throw new NotFoundException("简历不存在");
    }

    private static ResumeDto ToDto(Resume resume)
    {
        return new ResumeDto(
            resume.Id.ToString(),
            resume.CandidateNo,
            resume.Name,
            resume.Gender,
            resume.Phone,
            resume.Email,
            resume.Education,
            resume.WorkExperience,
            resume.Skills,
            resume.AppliedPositionId.ToString(),
            resume.AttachmentFileId?.ToString(),
            resume.Status,
            resume.CurrentRound,
            resume.RejectReason,
            resume.Remark,
            resume.Version);
    }

    private static InterviewDto ToDto(InterviewRecord interview)
    {
        return new InterviewDto(
            interview.Id.ToString(),
            interview.ResumeId.ToString(),
            interview.RoundNo,
            interview.InterviewerUserId.ToString(),
            interview.ScheduledAt,
            interview.Location,
            interview.Score,
            interview.Evaluation,
            interview.Conclusion,
            interview.NextScheduledAt,
            interview.CompletedAt,
            interview.Remark);
    }

    private static async Task EnsureOptimisticAsync(Task<int> affected)
    {
        if (await affected == 0)
        {
            throw new ConflictException("数据已被其他用户修改");
        }
    }
    private sealed class FixedIdGenerator(long value) : ISnowflakeIdGenerator
    {
        public long NextId()
        {
            return value;
        }
    }
}
