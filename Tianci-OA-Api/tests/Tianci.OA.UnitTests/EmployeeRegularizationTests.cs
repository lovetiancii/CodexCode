using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Employees;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Organization;

namespace Tianci.OA.UnitTests;

public sealed class EmployeeRegularizationTests
{
    private static readonly DateTime Today = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EntryDate = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(EmployeeStatus.Active)]
    [InlineData(EmployeeStatus.Terminated)]
    [InlineData(EmployeeStatus.Archived)]
    public async Task Only_probation_employee_can_be_regularized(EmployeeStatus status)
    {
        var fixture = new Fixture(Employee(status: status));

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.RegularizeAsync("100", new(Today, 3), default));

        Assert.Equal("INVALID_STATE_TRANSITION", error.Code);
        Assert.Equal(status, fixture.Employee.Status);
        Assert.Null(fixture.Employee.RegularDate);
    }

    [Fact]
    public async Task Stale_request_version_is_rejected_without_mutation()
    {
        var fixture = new Fixture(Employee(version: 4));

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.RegularizeAsync("100", new(Today, 3), default));

        Assert.Equal("CONFLICT", error.Code);
        Assert.Equal(EmployeeStatus.Probation, fixture.Employee.Status);
        Assert.Null(fixture.Employee.RegularDate);
        Assert.Equal(4, fixture.Employee.Version);
    }

    [Fact]
    public async Task Concurrent_database_update_is_reported_as_conflict()
    {
        var fixture = new Fixture(Employee());
        fixture.Employees.UpdateWhereResult = 0;

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.RegularizeAsync("100", new(Today, 3), default));

        Assert.Equal("CONFLICT", error.Code);
        Assert.Contains("其他用户修改", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Entry_date_and_today_are_inclusive_boundaries(bool useToday)
    {
        var fixture = new Fixture(Employee());
        var regularDate = useToday ? Today : EntryDate;

        var result = await fixture.Service.RegularizeAsync(
            "100",
            new RegularizeEmployeeRequest(regularDate.AddHours(12), 3),
            default);

        Assert.Equal(EmployeeStatus.Active, result.Status);
        Assert.Equal(regularDate.Date, result.RegularDate);
        Assert.Equal(4, result.Version);
        Assert.Equal(EmployeeStatus.Active, fixture.Employee.Status);
        Assert.Equal(regularDate.Date, fixture.Employee.RegularDate);
        Assert.Equal(Today, fixture.Employee.UpdatedAt);
        Assert.Equal(7, fixture.Employee.UpdatedBy);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(92)]
    public async Task Date_outside_entry_date_and_today_is_rejected(int offsetFromEntry)
    {
        var fixture = new Fixture(Employee());
        var invalidDate = EntryDate.AddDays(offsetFromEntry);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.RegularizeAsync("100", new(invalidDate, 3), default));

        Assert.Equal("BUSINESS_ERROR", error.Code);
        Assert.Equal(EmployeeStatus.Probation, fixture.Employee.Status);
        Assert.Null(fixture.Employee.RegularDate);
        Assert.Equal(3, fixture.Employee.Version);
    }

    private static Employee Employee(
        EmployeeStatus status = EmployeeStatus.Probation,
        int version = 3)
    {
        return new()
        {
            Id = 100,
            EmployeeNo = "E000100",
            Name = "测试员工",
            Phone = "13800000000",
            DepartmentId = 10,
            PositionId = 20,
            Status = status,
            EntryDate = EntryDate,
            ProbationMonths = 3,
            Version = version
        };
    }

    private sealed class Fixture
    {
        public Employee Employee
        {
            get;
        }
        public InMemoryRepository<Employee> Employees
        {
            get;
        }
        public EmployeeService Service
        {
            get;
        }

        public Fixture(Employee employee)
        {
            Employee = employee;
            Employees = new InMemoryRepository<Employee>(employee);
            Service = new EmployeeService(
                Employees,
                new InMemoryRepository<Department>(),
                new InMemoryRepository<Position>(),
                new StubProtector(),
                new StubDataScope(),
                new StubIds(),
                new StubClock(Today),
                new StubCurrentUser());
        }
    }
}
