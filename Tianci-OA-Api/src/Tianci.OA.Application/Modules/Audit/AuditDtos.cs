namespace Tianci.OA.Application.Modules.Audit;

public sealed record AuditLogDto(
    string Id,
    string? TraceId,
    string? OperatorUserId,
    string? OperatorName,
    string Module,
    string Action,
    string? BusinessType,
    string? BusinessId,
    string? RequestMethod,
    string? RequestPath,
    string? ClientIp,
    bool Succeeded,
    string? ErrorCode,
    uint? DurationMs,
    DateTime CreatedAt);
