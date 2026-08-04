using Tianci.OA.Application.Common;

namespace Tianci.OA.Application.Modules.Audit;

public interface IAuditService
{
    Task<PagedResult<AuditLogDto>> ListAsync(
        string? module,
        string? operatorUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}
