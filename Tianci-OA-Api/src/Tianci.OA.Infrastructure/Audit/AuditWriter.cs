using SqlSugar;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Domain.Audit;

namespace Tianci.OA.Infrastructure.Audit;

public sealed class AuditWriter(ISqlSugarClient db) : IAuditWriter
{
    public async Task WriteAsync(OperationLog log, CancellationToken cancellationToken = default) => _ = await db.Insertable(log).ExecuteCommandAsync();
}
