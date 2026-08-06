namespace Tianci.OA.UnitTests;

public sealed class PermissionSeedTests
{
    private static readonly string[] RequiredPermissions =
    [
        "dashboard:view",
        "organization:view",
        "organization:manage",
        "employee:view",
        "employee:create",
        "employee:edit",
        "employee:sensitive",
        "employee:terminate",
        "employee:archive",
        "resume:view",
        "resume:create",
        "resume:edit",
        "resume:manage",
        "resume:schedule",
        "resume:evaluate",
        "resume:hire",
        "resume:attachment",
        "contract:view",
        "contract:manage",
        "file:upload",
        "file:download",
        "file:delete",
        "audit:view",
        "workflow:manage",
        "system:user",
        "system:role",
        "system:menu"
    ];

    [Fact]
    public void Initialization_script_contains_complete_business_permission_tree()
    {
        var initSql = File.ReadAllText(GetInitSqlPath());

        Assert.All(RequiredPermissions, permission =>
            Assert.Contains($"'{permission}'", initSql, StringComparison.Ordinal));
    }

    [Fact]
    public void Interviewer_role_can_evaluate_but_cannot_schedule_interviews()
    {
        var initSql = File.ReadAllText(GetInitSqlPath());

        Assert.Contains(
            "900000000000000103,900000000000000053",
            initSql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "900000000000000103,900000000000000045",
            initSql,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("HR_MANAGER", 1)]
    [InlineData("DEPARTMENT_MANAGER", 2)]
    [InlineData("INTERVIEWER", 3)]
    [InlineData("EMPLOYEE", 3)]
    public void Initialization_script_seeds_role_data_scope(
        string roleCode,
        int dataScope)
    {
        var initSql = File.ReadAllText(GetInitSqlPath());

        Assert.Contains(
            $"'{roleCode}',{dataScope},1,1",
            initSql,
            StringComparison.Ordinal);
    }

    private static string GetInitSqlPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "database",
            "init.sql"));
    }
}
