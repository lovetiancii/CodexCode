using Microsoft.Extensions.Diagnostics.HealthChecks;
using SqlSugar;

namespace Tianci.OA.Infrastructure.Health;

public sealed class MySqlHealthCheck(
    ISqlSugarClient db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await db.Ado.GetIntAsync("SELECT 1");

            return result == 1
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("MySQL query failed");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "MySQL unavailable",
                exception);
        }
    }
}
