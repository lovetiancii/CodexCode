using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Audit;

namespace Tianci.OA.Application.Modules.Audit;

public sealed record AuditLogDto(string Id, string? TraceId, string? OperatorUserId, string? OperatorName, string Module, string Action, string? BusinessType, string? BusinessId, string? RequestMethod, string? RequestPath, string? ClientIp, bool Succeeded, string? ErrorCode, uint? DurationMs, DateTime CreatedAt);
public interface IAuditService { Task<PagedResult<AuditLogDto>> ListAsync(string? module, string? operatorUserId, int pageNumber, int pageSize, CancellationToken ct); }
public sealed class AuditService(IRepository<OperationLog> logs) : IAuditService
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(string? module, string? operatorUserId, int pageNumber, int pageSize, CancellationToken ct)
    {
        var uid = IdParser.ParseNullable(operatorUserId, "operatorUserId"); var page = new PageRequest(pageNumber, pageSize);
        var result = await logs.PageAsync(x => (string.IsNullOrEmpty(module) || x.Module == module) && (!uid.HasValue || x.OperatorUserId == uid), page.SafePageNumber, page.SafePageSize, x => x.CreatedAt, true, ct);
        return new(result.Items.Select(x => new AuditLogDto(x.Id.ToString(), x.TraceId, x.OperatorUserId?.ToString(), x.OperatorName, x.Module, x.Action, x.BusinessType, x.BusinessId?.ToString(), x.RequestMethod, x.RequestPath, x.ClientIp, x.Result == 1, x.ErrorCode, x.DurationMs, x.CreatedAt)).ToArray(), page.SafePageNumber, page.SafePageSize, result.Total);
    }
}
