using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Employees;
using Tianci.OA.Application.Modules.Recruitment;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Files;
using Tianci.OA.Domain.Organization;
using Tianci.OA.Domain.Recruitment;

namespace Tianci.OA.UnitTests;

public sealed class DataScopeFilteringTests
{
    [Fact]
    public async Task Employee_self_scope_returns_only_linked_employee()
    {
        var employees = new InMemoryRepository<Employee>(
            Employee(101, 10),
            Employee(102, 10),
            Employee(103, 20));
        var scope = new StubDataScope(new DataScopeContext(
            DataScope.Self,
            7,
            102,
            10,
            new HashSet<long>()));
        var service = CreateEmployeeService(employees, scope);

        var result = await service.ListAsync(
            new EmployeeQuery(null, null, null, null, 1, 20),
            default);

        var employee = Assert.Single(result.Items);
        Assert.Equal("102", employee.Id);
    }

    [Fact]
    public async Task Employee_department_scope_includes_configured_child_departments()
    {
        var employees = new InMemoryRepository<Employee>(
            Employee(101, 10),
            Employee(102, 11),
            Employee(103, 20));
        var scope = new StubDataScope(new DataScopeContext(
            DataScope.DepartmentAndChildren,
            7,
            101,
            10,
            new HashSet<long> { 10, 11 }));
        var service = CreateEmployeeService(employees, scope);

        var result = await service.ListAsync(
            new EmployeeQuery(null, null, null, null, 1, 20),
            default);

        Assert.Equal(["101", "102"], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Resume_self_scope_includes_owned_and_assigned_interview_records()
    {
        var resumes = new InMemoryRepository<Resume>(
            Resume(201, 7),
            Resume(202, 8),
            Resume(203, 8));
        var interviews = new InMemoryRepository<InterviewRecord>(new InterviewRecord
        {
            Id = 301,
            ResumeId = 202,
            InterviewerUserId = 7
        });
        var scope = new StubDataScope(new DataScopeContext(
            DataScope.Self,
            7,
            101,
            10,
            new HashSet<long>()));
        var service = new RecruitmentService(
            resumes,
            interviews,
            new InMemoryRepository<EmployeeEntry>(),
            new InMemoryRepository<Employee>(),
            new InMemoryRepository<SysFile>(),
            new InMemoryRepository<Department>(),
            new InMemoryRepository<Position>(),
            null!,
            new StubProtector(),
            scope,
            new StubIds(),
            new StubClock(),
            new StubCurrentUser(),
            new TrackingUnitOfWork());

        var result = await service.ListAsync(
            new ResumeQuery(null, null, null, 1, 20),
            default);

        Assert.Equal(["201", "202"], result.Items.Select(item => item.Id));
    }

    private static EmployeeService CreateEmployeeService(
        InMemoryRepository<Employee> employees,
        IDataScopeService scope)
    {
        return new EmployeeService(
            employees,
            new InMemoryRepository<Department>(),
            new InMemoryRepository<Position>(),
            new StubProtector(),
            scope,
            new StubIds(),
            new StubClock(),
            new StubCurrentUser());
    }

    private static Employee Employee(long id, long departmentId)
    {
        return new Employee
        {
            Id = id,
            EmployeeNo = $"E{id}",
            Name = $"员工{id}",
            Phone = "13800000000",
            DepartmentId = departmentId,
            PositionId = 1,
            Status = EmployeeStatus.Active,
            EntryDate = new DateTime(2026, 1, 1)
        };
    }

    private static Resume Resume(long id, long ownerUserId)
    {
        return new Resume
        {
            Id = id,
            CandidateNo = $"CV{id}",
            Name = $"候选人{id}",
            Phone = "13800000000",
            AppliedPositionId = 1,
            OwnerUserId = ownerUserId,
            Status = ResumeStatus.Submitted
        };
    }
}
