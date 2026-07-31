using Tianci.OA.Infrastructure.Security;

namespace Tianci.OA.UnitTests;

public sealed class DefaultAdminCredentialTests
{
    private const string SeedHash =
        "AQAAAAIAAYagAAAAEEMg8XvCJHljSimZF++uifAeyygQdjY+q+ogAd0SycYdj0X/EHjbIjyPnRuCHhtWaA==";

    [Fact]
    public void Development_seed_hash_matches_documented_default_password()
    {
        var passwords = new PasswordService();
        var initSql = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "init.sql")));

        Assert.True(passwords.Verify("admin", SeedHash, "Tianci@OA2026!"));
        Assert.False(passwords.Verify("admin", SeedHash, "wrong-password"));
        Assert.Contains(SeedHash, initSql, StringComparison.Ordinal);
    }
}
