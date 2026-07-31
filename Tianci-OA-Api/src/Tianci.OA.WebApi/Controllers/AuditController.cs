using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Audit;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController, Authorize, Permission("audit:view"), Route("api/v1/audit-logs")]
public sealed class AuditController(IAuditService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditLogDto>> List([FromQuery] string? module, [FromQuery] string? operatorUserId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        service.ListAsync(module, operatorUserId, pageNumber, pageSize, ct);
}
