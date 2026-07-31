using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Organization;
using Tianci.OA.Domain.Recruitment;

namespace Tianci.OA.Application.Modules.Recruitment;

public sealed class ResumeRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public Gender Gender { get; set; }
    [Required, Phone] public string Phone { get; set; } = "";
    [EmailAddress] public string? Email { get; set; }
    [StringLength(64)] public string? Education { get; set; }
    public string? WorkExperience { get; set; }
    public string? Skills { get; set; }
    [Required] public string AppliedPositionId { get; set; } = "";
    [StringLength(64)] public string? Source { get; set; }
    public string? AttachmentFileId { get; set; }
    public string? OwnerUserId { get; set; }
    [StringLength(1000)] public string? Remark { get; set; }
}
public sealed record ResumeDto(string Id, string CandidateNo, string Name, Gender Gender, string Phone, string? Email, string? Education, string? WorkExperience, string? Skills, string AppliedPositionId, string? AttachmentFileId, ResumeStatus Status, byte CurrentRound, string? RejectReason, string? Remark, int Version);
public sealed record ResumeQuery(string? Keyword, string? PositionId, ResumeStatus? Status, int PageNumber = 1, int PageSize = 20);
public sealed record ChangeResumeStatusRequest(ResumeStatus TargetStatus, string? Reason, int Version);
public sealed class ScheduleInterviewRequest
{
    [Range(1, 5)] public byte RoundNo { get; set; }
    [Required] public string InterviewerUserId { get; set; } = "";
    public DateTime ScheduledAt { get; set; }
    [StringLength(255)] public string? Location { get; set; }
    [StringLength(1000)] public string? Remark { get; set; }
    public int ResumeVersion { get; set; }
}
public sealed class CompleteInterviewRequest
{
    [Range(0, 100)] public decimal Score { get; set; }
    [Required, StringLength(2000)] public string Evaluation { get; set; } = "";
    public InterviewConclusion Conclusion { get; set; }
    public bool IsFinalRound { get; set; }
    public DateTime? NextScheduledAt { get; set; }
    [StringLength(1000)] public string? Remark { get; set; }
    public int ResumeVersion { get; set; }
}
public sealed record InterviewDto(string Id, string ResumeId, byte RoundNo, string InterviewerUserId, DateTime ScheduledAt, string? Location, decimal? Score, string? Evaluation, InterviewConclusion Conclusion, DateTime? NextScheduledAt, DateTime? CompletedAt, string? Remark);
public sealed class ConfirmOfferRequest
{
    public DateTime PlannedEntryDate { get; set; }
    [Required] public string DepartmentId { get; set; } = "";
    [Required] public string PositionId { get; set; } = "";
    [Required] public string MonthlySalary { get; set; } = "";
    [Range(0, 12)] public byte ProbationMonths { get; set; } = 3;
    [StringLength(1000)] public string? Remark { get; set; }
    public int ResumeVersion { get; set; }
}
public sealed record ConfirmEntryRequest(DateTime ActualEntryDate, [Required] string EmployeeNo, int ResumeVersion, int EntryVersion);

public interface IRecruitmentService
{
    Task<PagedResult<ResumeDto>> ListAsync(ResumeQuery query, CancellationToken ct);
    Task<ResumeDto> GetAsync(string id, CancellationToken ct);
    Task<ResumeDto> CreateAsync(ResumeRequest request, CancellationToken ct);
    Task<ResumeDto> UpdateAsync(string id, ResumeRequest request, int version, CancellationToken ct);
    Task ChangeStatusAsync(string id, ChangeResumeStatusRequest request, CancellationToken ct);
    Task<InterviewDto> ScheduleInterviewAsync(string resumeId, ScheduleInterviewRequest request, CancellationToken ct);
    Task<InterviewDto> CompleteInterviewAsync(string resumeId, string interviewId, CompleteInterviewRequest request, CancellationToken ct);
    Task<IReadOnlyList<InterviewDto>> InterviewsAsync(string resumeId, CancellationToken ct);
    Task ConfirmOfferAsync(string resumeId, ConfirmOfferRequest request, CancellationToken ct);
    Task<string> ConfirmEntryAsync(string resumeId, ConfirmEntryRequest request, CancellationToken ct);
}

public sealed class RecruitmentService(
    IRepository<Resume> resumes, IRepository<InterviewRecord> interviews, IRepository<EmployeeEntry> entries, IRepository<Employee> employees,
    IRepository<Department> departments, IRepository<Position> positions, IRepository<Tianci.OA.Domain.Identity.SysUser> users,
    ISensitiveDataProtector protector, ISnowflakeIdGenerator ids, IClock clock, ICurrentUser currentUser, IUnitOfWork uow) : IRecruitmentService
{
    public async Task<PagedResult<ResumeDto>> ListAsync(ResumeQuery q, CancellationToken ct)
    {
        var keyword = q.Keyword?.Trim() ?? ""; var pid = IdParser.ParseNullable(q.PositionId, "positionId"); var page = new PageRequest(q.PageNumber, q.PageSize);
        Expression<Func<Resume, bool>> predicate = x => !x.IsDeleted &&
            (keyword == "" || x.Name.Contains(keyword) || x.Phone.Contains(keyword) || (x.Email != null && x.Email.Contains(keyword)));
        if (pid.HasValue) { var positionId = pid.Value; predicate = predicate.And(x => x.AppliedPositionId == positionId); }
        if (q.Status.HasValue) { var status = q.Status.Value; predicate = predicate.And(x => x.Status == status); }
        var result = await resumes.PageAsync(predicate, page.SafePageNumber, page.SafePageSize, x => x.UpdatedAt, true, ct);
        return new(result.Items.Select(ToDto).ToArray(), page.SafePageNumber, page.SafePageSize, result.Total);
    }
    public async Task<ResumeDto> GetAsync(string id, CancellationToken ct) => ToDto(await RequiredResume(id, ct));
    public async Task<ResumeDto> CreateAsync(ResumeRequest r, CancellationToken ct)
    {
        await ValidateResumeAsync(r, ct); var next = ids.NextId();
        var e = new Resume { CandidateNo = $"CV{clock.UtcNow:yyyyMMdd}{next % 100000000:00000000}", Status = ResumeStatus.Submitted };
        Apply(e, r); EntityAudit.Create(e, new FixedIdGenerator(next), clock, currentUser); await resumes.InsertAsync(e, ct); return ToDto(e);
    }
    public async Task<ResumeDto> UpdateAsync(string id, ResumeRequest r, int version, CancellationToken ct)
    {
        var e = await RequiredResume(id, ct); if (e.Status is ResumeStatus.Hired) throw new ConflictException("已入职简历不可编辑"); if (e.Version != version) throw new ConflictException("数据已被其他用户修改");
        await ValidateResumeAsync(r, ct); Apply(e, r); var old = e.Version; e.Version++; EntityAudit.Update(e, clock, currentUser);
        await EnsureOptimisticAsync(resumes.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct)); return ToDto(e);
    }
    public async Task ChangeStatusAsync(string id, ChangeResumeStatusRequest r, CancellationToken ct)
    {
        var e = await RequiredResume(id, ct); if (e.Version != r.Version) throw new ConflictException("数据已被其他用户修改");
        var allowed = (e.Status, r.TargetStatus) switch
        {
            (ResumeStatus.Submitted, ResumeStatus.Screening) => true,
            (ResumeStatus.Submitted or ResumeStatus.Screening, ResumeStatus.InterviewPending) => true,
            (ResumeStatus.Submitted or ResumeStatus.Screening or ResumeStatus.InterviewPending, ResumeStatus.Rejected) => !string.IsNullOrWhiteSpace(r.Reason),
            (ResumeStatus.Rejected, ResumeStatus.Submitted) => true,
            (ResumeStatus.OfferPending, ResumeStatus.OfferDeclined) => !string.IsNullOrWhiteSpace(r.Reason),
            (ResumeStatus.EntryPending, ResumeStatus.OfferDeclined) => !string.IsNullOrWhiteSpace(r.Reason),
            _ => false
        };
        if (!allowed) throw new ConflictException("不允许的招聘状态流转", "INVALID_STATE_TRANSITION");
        var old = e.Version; e.Status = r.TargetStatus; e.RejectReason = r.TargetStatus is ResumeStatus.Rejected or ResumeStatus.OfferDeclined ? r.Reason!.Trim() : null; e.Version++; EntityAudit.Update(e, clock, currentUser);
        await EnsureOptimisticAsync(resumes.UpdateWhereAsync(e, x => x.Id == e.Id && x.Version == old, ct));
    }
    public async Task<InterviewDto> ScheduleInterviewAsync(string resumeId, ScheduleInterviewRequest r, CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct);
        if (resume.Status is not (ResumeStatus.InterviewPending or ResumeStatus.Screening)) throw new ConflictException("当前状态不可安排面试", "INVALID_STATE_TRANSITION");
        if (resume.Version != r.ResumeVersion) throw new ConflictException("数据已被其他用户修改");
        if (r.ScheduledAt.ToUniversalTime() <= clock.UtcNow) throw new BusinessException("面试时间必须晚于当前时间");
        var interviewerId = IdParser.Parse(r.InterviewerUserId, "interviewerUserId");
        if (!await users.ExistsAsync(x => x.Id == interviewerId && !x.IsDeleted && x.Status == UserStatus.Enabled, ct)) throw new NotFoundException("面试官不存在或未启用");
        if (await interviews.ExistsAsync(x => x.ResumeId == resume.Id && x.RoundNo == r.RoundNo && !x.IsDeleted, ct)) throw new ConflictException("该轮面试已存在");
        var interview = new InterviewRecord { ResumeId = resume.Id, RoundNo = r.RoundNo, InterviewerUserId = interviewerId, ScheduledAt = r.ScheduledAt.ToUniversalTime(), Location = r.Location, Remark = r.Remark, Conclusion = InterviewConclusion.Pending };
        EntityAudit.Create(interview, ids, clock, currentUser);
        await uow.BeginAsync();
        try
        {
            await interviews.InsertAsync(interview, ct); var old = resume.Version; resume.Status = ResumeStatus.Interviewing; resume.CurrentRound = r.RoundNo; resume.Version++; EntityAudit.Update(resume, clock, currentUser);
            await EnsureOptimisticAsync(resumes.UpdateWhereAsync(resume, x => x.Id == resume.Id && x.Version == old, ct)); await uow.CommitAsync();
        }
        catch { await uow.RollbackAsync(); throw; }
        return ToDto(interview);
    }
    public async Task<InterviewDto> CompleteInterviewAsync(string resumeId, string interviewId, CompleteInterviewRequest r, CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct); if (resume.Status != ResumeStatus.Interviewing || resume.Version != r.ResumeVersion) throw new ConflictException("简历状态或版本不允许提交评价");
        var interview = await interviews.FirstAsync(x => x.Id == IdParser.Parse(interviewId) && x.ResumeId == resume.Id && !x.IsDeleted, ct) ?? throw new NotFoundException("面试记录不存在");
        if (interview.Conclusion != InterviewConclusion.Pending && interview.Conclusion != InterviewConclusion.Hold) throw new ConflictException("本轮面试已完成");
        if (r.Conclusion == InterviewConclusion.Pending || r.Conclusion == InterviewConclusion.Cancelled) throw new BusinessException("请提交明确的面试结论");
        interview.Score = r.Score; interview.Evaluation = r.Evaluation.Trim(); interview.Conclusion = r.Conclusion; interview.NextScheduledAt = r.NextScheduledAt?.ToUniversalTime(); interview.Remark = r.Remark; interview.CompletedAt = clock.UtcNow; EntityAudit.Update(interview, clock, currentUser);
        var target = r.Conclusion switch { InterviewConclusion.Fail => ResumeStatus.Rejected, InterviewConclusion.Hold => ResumeStatus.Interviewing, InterviewConclusion.Pass when r.IsFinalRound => ResumeStatus.OfferPending, InterviewConclusion.Pass => ResumeStatus.InterviewPending, _ => resume.Status };
        await uow.BeginAsync();
        try
        {
            await interviews.UpdateAsync(interview, ct); var old = resume.Version; resume.Status = target; if (target == ResumeStatus.Rejected) resume.RejectReason = "面试未通过"; resume.Version++; EntityAudit.Update(resume, clock, currentUser);
            await EnsureOptimisticAsync(resumes.UpdateWhereAsync(resume, x => x.Id == resume.Id && x.Version == old, ct)); await uow.CommitAsync();
        }
        catch { await uow.RollbackAsync(); throw; }
        return ToDto(interview);
    }
    public async Task<IReadOnlyList<InterviewDto>> InterviewsAsync(string resumeId, CancellationToken ct)
    {
        var id = IdParser.Parse(resumeId); _ = await RequiredResume(resumeId, ct); return (await interviews.ListAsync(x => x.ResumeId == id && !x.IsDeleted, ct)).OrderBy(x => x.RoundNo).Select(ToDto).ToArray();
    }
    public async Task ConfirmOfferAsync(string resumeId, ConfirmOfferRequest r, CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct); if (resume.Status != ResumeStatus.OfferPending || resume.Version != r.ResumeVersion) throw new ConflictException("仅待录用候选人可确认录用");
        var did = IdParser.Parse(r.DepartmentId, "departmentId"); var pid = IdParser.Parse(r.PositionId, "positionId");
        if (!await departments.ExistsAsync(x => x.Id == did && !x.IsDeleted, ct) || !await positions.ExistsAsync(x => x.Id == pid && x.DepartmentId == did && !x.IsDeleted, ct)) throw new NotFoundException("部门或岗位不存在");
        if (await entries.ExistsAsync(x => x.ResumeId == resume.Id && !x.IsDeleted, ct)) throw new ConflictException("该候选人已存在录用记录");
        var entry = new EmployeeEntry { ResumeId = resume.Id, PlannedEntryDate = r.PlannedEntryDate.Date, DepartmentId = did, PositionId = pid, MonthlySalaryCiphertext = protector.Protect(r.MonthlySalary), ProbationMonths = r.ProbationMonths, Status = EntryStatus.EntryPending, Remark = r.Remark };
        EntityAudit.Create(entry, ids, clock, currentUser);
        await uow.BeginAsync();
        try { await entries.InsertAsync(entry, ct); var old = resume.Version; resume.Status = ResumeStatus.EntryPending; resume.Version++; EntityAudit.Update(resume, clock, currentUser); await EnsureOptimisticAsync(resumes.UpdateWhereAsync(resume, x => x.Id == resume.Id && x.Version == old, ct)); await uow.CommitAsync(); }
        catch { await uow.RollbackAsync(); throw; }
    }
    public async Task<string> ConfirmEntryAsync(string resumeId, ConfirmEntryRequest r, CancellationToken ct)
    {
        var resume = await RequiredResume(resumeId, ct); if (resume.Status != ResumeStatus.EntryPending || resume.Version != r.ResumeVersion) throw new ConflictException("仅待入职候选人可确认到岗");
        var entry = await entries.FirstAsync(x => x.ResumeId == resume.Id && !x.IsDeleted, ct) ?? throw new NotFoundException("录用记录不存在");
        if (entry.Status != EntryStatus.EntryPending || entry.Version != r.EntryVersion || entry.EmployeeId.HasValue) throw new ConflictException("入职记录状态或版本冲突");
        if (await employees.ExistsAsync(x => (x.SourceResumeId == resume.Id || x.EmployeeNo == r.EmployeeNo) && !x.IsDeleted, ct)) throw new ConflictException("候选人已生成员工或员工编号重复");
        var employee = new Employee { EmployeeNo = r.EmployeeNo.Trim(), SourceResumeId = resume.Id, Name = resume.Name, Gender = resume.Gender, Phone = resume.Phone, Email = resume.Email, DepartmentId = entry.DepartmentId, PositionId = entry.PositionId, Status = entry.ProbationMonths > 0 ? EmployeeStatus.Probation : EmployeeStatus.Active, EntryDate = r.ActualEntryDate.Date, ProbationMonths = entry.ProbationMonths, MonthlySalaryCiphertext = entry.MonthlySalaryCiphertext };
        EntityAudit.Create(employee, ids, clock, currentUser);
        await uow.BeginAsync();
        try
        {
            await employees.InsertAsync(employee, ct);
            var entryOld = entry.Version; entry.EmployeeId = employee.Id; entry.ActualEntryDate = r.ActualEntryDate.Date; entry.Status = EntryStatus.Entered; entry.Version++; EntityAudit.Update(entry, clock, currentUser);
            await EnsureOptimisticAsync(entries.UpdateWhereAsync(entry, x => x.Id == entry.Id && x.Version == entryOld, ct));
            var resumeOld = resume.Version; resume.Status = ResumeStatus.Hired; resume.Version++; EntityAudit.Update(resume, clock, currentUser);
            await EnsureOptimisticAsync(resumes.UpdateWhereAsync(resume, x => x.Id == resume.Id && x.Version == resumeOld, ct)); await uow.CommitAsync();
        }
        catch { await uow.RollbackAsync(); throw; }
        return employee.Id.ToString();
    }
    private async Task ValidateResumeAsync(ResumeRequest r, CancellationToken ct)
    {
        var pid = IdParser.Parse(r.AppliedPositionId, "appliedPositionId"); if (!await positions.ExistsAsync(x => x.Id == pid && !x.IsDeleted && x.Status == EnabledStatus.Enabled, ct)) throw new NotFoundException("应聘岗位不存在或未启用");
    }
    private static void Apply(Resume e, ResumeRequest r)
    {
        e.Name = r.Name.Trim(); e.Gender = r.Gender; e.Phone = r.Phone; e.Email = r.Email; e.Education = r.Education; e.WorkExperience = r.WorkExperience; e.Skills = r.Skills;
        e.AppliedPositionId = IdParser.Parse(r.AppliedPositionId, "appliedPositionId"); e.Source = r.Source; e.AttachmentFileId = IdParser.ParseNullable(r.AttachmentFileId, "attachmentFileId"); e.OwnerUserId = IdParser.ParseNullable(r.OwnerUserId, "ownerUserId"); e.Remark = r.Remark;
    }
    private async Task<Resume> RequiredResume(string id, CancellationToken ct) => await resumes.FirstAsync(x => x.Id == IdParser.Parse(id) && !x.IsDeleted, ct) ?? throw new NotFoundException("简历不存在");
    private static ResumeDto ToDto(Resume e) => new(e.Id.ToString(), e.CandidateNo, e.Name, e.Gender, e.Phone, e.Email, e.Education, e.WorkExperience, e.Skills, e.AppliedPositionId.ToString(), e.AttachmentFileId?.ToString(), e.Status, e.CurrentRound, e.RejectReason, e.Remark, e.Version);
    private static InterviewDto ToDto(InterviewRecord e) => new(e.Id.ToString(), e.ResumeId.ToString(), e.RoundNo, e.InterviewerUserId.ToString(), e.ScheduledAt, e.Location, e.Score, e.Evaluation, e.Conclusion, e.NextScheduledAt, e.CompletedAt, e.Remark);
    private static async Task EnsureOptimisticAsync(Task<int> affected) { if (await affected == 0) throw new ConflictException("数据已被其他用户修改"); }
    private sealed class FixedIdGenerator(long value) : ISnowflakeIdGenerator { public long NextId() => value; }
}
