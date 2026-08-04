using SqlSugar;
using Tianci.OA.Application.Modules.Recruitment;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Identity;
using Tianci.OA.Domain.Organization;

namespace Tianci.OA.Infrastructure.Persistence;

public sealed class InterviewerQuery(ISqlSugarClient db) : IInterviewerQuery
{
    public async Task<IReadOnlyList<InterviewerOptionDto>> SearchAsync(
        long departmentId,
        string? keyword,
        bool sameDepartmentOnly,
        int limit,
        CancellationToken ct)
    {
        keyword = keyword?.Trim() ?? "";
        var query = db.Queryable<SysUser, Employee, Department, Position>((user, employee, department, position) =>
                new JoinQueryInfos(
                    JoinType.Inner, user.EmployeeId == employee.Id,
                    JoinType.Inner, employee.DepartmentId == department.Id,
                    JoinType.Inner, employee.PositionId == position.Id))
            .Where((user, employee, department, position) =>
                !user.IsDeleted &&
                user.Status == UserStatus.Enabled &&
                !employee.IsDeleted &&
                (employee.Status == EmployeeStatus.Probation || employee.Status == EmployeeStatus.Active) &&
                !department.IsDeleted &&
                department.Status == EnabledStatus.Enabled &&
                !position.IsDeleted &&
                position.Status == EnabledStatus.Enabled);

        if (sameDepartmentOnly)
        {
            query = query.Where((user, employee, department, position) => employee.DepartmentId == departmentId);
        }

        if (keyword.Length > 0)
        {
            var value = keyword;
            query = query.Where((user, employee, department, position) =>
                employee.Name.Contains(value) ||
                employee.EmployeeNo.Contains(value) ||
                user.Username.Contains(value) ||
                position.Name.Contains(value));
        }

        var rows = await query
            .OrderBy((user, employee, department, position) => employee.Name)
            .Take(Math.Clamp(limit, 1, 100))
            .Select((user, employee, department, position) => new InterviewerOptionRow
            {
                UserId = user.Id,
                EmployeeId = employee.Id,
                EmployeeNo = employee.EmployeeNo,
                Name = employee.Name,
                DepartmentId = department.Id,
                DepartmentName = department.Name,
                PositionId = position.Id,
                PositionName = position.Name
            })
            .ToListAsync(ct);

        return [.. rows.Select(row => new InterviewerOptionDto(
            row.UserId.ToString(),
            row.EmployeeId.ToString(),
            row.EmployeeNo,
            row.Name,
            row.DepartmentId.ToString(),
            row.DepartmentName,
            row.PositionId.ToString(),
            row.PositionName))];
    }

    public Task<bool> IsEligibleAsync(long userId, CancellationToken ct)
    {
        return db.Queryable<SysUser, Employee>((user, employee) =>
                new JoinQueryInfos(JoinType.Inner, user.EmployeeId == employee.Id))
            .Where((user, employee) =>
                user.Id == userId &&
                !user.IsDeleted &&
                user.Status == UserStatus.Enabled &&
                !employee.IsDeleted &&
                (employee.Status == EmployeeStatus.Probation || employee.Status == EmployeeStatus.Active))
            .AnyAsync();
    }

    private sealed class InterviewerOptionRow
    {
        public long UserId { get; set; }
        public long EmployeeId { get; set; }
        public string EmployeeNo { get; set; } = "";
        public string Name { get; set; } = "";
        public long DepartmentId { get; set; }
        public string DepartmentName { get; set; } = "";
        public long PositionId { get; set; }
        public string PositionName { get; set; } = "";
    }
}

