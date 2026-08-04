using System.Diagnostics;
using System.Text.Json;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Audit;

namespace Tianci.OA.WebApi.Middleware;

public sealed class AuditMiddleware(
    RequestDelegate next,
    ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAuditWriter audit,
        ISnowflakeIdGenerator ids,
        ICurrentUser user,
        IClock clock)
    {
        var started = Stopwatch.GetTimestamp();
        Exception? error = null;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            error = exception;
            throw;
        }
        finally
        {
            if (ShouldAudit(context.Request))
            {
                await TryWriteAuditLogAsync(
                    context,
                    audit,
                    ids,
                    user,
                    clock,
                    started,
                    error);
            }
        }
    }

    private static bool ShouldAudit(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api")
            && request.Method is "POST" or "PUT" or "PATCH" or "DELETE";
    }

    private async Task TryWriteAuditLogAsync(
        HttpContext context,
        IAuditWriter audit,
        ISnowflakeIdGenerator ids,
        ICurrentUser user,
        IClock clock,
        long started,
        Exception? error)
    {
        try
        {
            var segments = context.Request.Path.Value?.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries) ?? [];
            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            await audit.WriteAsync(new OperationLog
            {
                Id = ids.NextId(),
                TraceId = context.TraceIdentifier,
                OperatorUserId = user.UserId,
                OperatorName = user.Name,
                Module = segments.Length > 2 ? segments[2] : "api",
                Action = context.Request.Method,
                RequestMethod = context.Request.Method,
                RequestPath = context.Request.Path.Value,
                ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                Result = (byte)(error == null && context.Response.StatusCode < 400 ? 1 : 0),
                ErrorCode = error is BusinessException businessException
                    ? businessException.Code
                    : error?.GetType().Name,
                DurationMs = (uint)Math.Min((long)duration, uint.MaxValue),
                CreatedAt = clock.UtcNow,
                ChangeSummary = JsonSerializer.Serialize(new
                {
                    queryKeys = context.Request.Query.Keys.ToArray()
                })
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to append audit log. TraceId={TraceId}",
                context.TraceIdentifier);
        }
    }
}
