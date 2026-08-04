using System.Linq.Expressions;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Audit;

namespace Tianci.OA.Application.Modules.Audit;

public sealed class AuditService(IRepository<OperationLog> logs) : IAuditService
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(
        string? module,
        string? operatorUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var operatorId = IdParser.ParseNullable(operatorUserId, "operatorUserId");
        var page = new PageRequest(pageNumber, pageSize);
        Expression<Func<OperationLog, bool>> predicate = x => true;

        if (!string.IsNullOrEmpty(module))
        {
            var moduleName = module;
            predicate = predicate.And(x => x.Module == moduleName);
        }

        if (operatorId.HasValue)
        {
            var userId = operatorId.Value;
            predicate = predicate.And(x => x.OperatorUserId == userId);
        }

        var (items, total) = await logs.PageAsync(
            predicate,
            page.SafePageNumber,
            page.SafePageSize,
            log => log.CreatedAt,
            true,
            cancellationToken);

        var result = items.Select(log => new AuditLogDto(
            log.Id.ToString(),
            log.TraceId,
            log.OperatorUserId?.ToString(),
            log.OperatorName,
            log.Module,
            log.Action,
            log.BusinessType,
            log.BusinessId?.ToString(),
            log.RequestMethod,
            log.RequestPath,
            log.ClientIp,
            log.Result == 1,
            log.ErrorCode,
            log.DurationMs,
            log.CreatedAt));

        return new PagedResult<AuditLogDto>(
            [.. result],
            page.SafePageNumber,
            page.SafePageSize,
            total);
    }
}
