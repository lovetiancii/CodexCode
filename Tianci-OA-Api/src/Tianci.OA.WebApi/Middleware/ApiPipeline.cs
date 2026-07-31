using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Audit;

namespace Tianci.OA.WebApi.Middleware;

public sealed record ApiResponse<T>(bool Success, string Code, string Message, T? Data, string TraceId);

public sealed class ApiResultFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult objectResult && objectResult.Value is not ApiResponse<object> && objectResult.Value is not ProblemDetails)
            context.Result = new ObjectResult(new ApiResponse<object?>(true, "OK", "操作成功", objectResult.Value, context.HttpContext.TraceIdentifier)) { StatusCode = objectResult.StatusCode };
        else if (context.Result is EmptyResult or NoContentResult)
            context.Result = new ObjectResult(new ApiResponse<object?>(true, "OK", "操作成功", null, context.HttpContext.TraceIdentifier));
        await next();
    }
}

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            var (status, code, message) = exception switch
            {
                BusinessException e => (e.StatusCode, e.Code, e.Message),
                UnauthorizedAccessException => (401, "UNAUTHORIZED", "未登录或会话已失效"),
                _ => (500, "INTERNAL_ERROR", "服务器内部错误")
            };
            if (status == 500) logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", context.TraceIdentifier);
            else logger.LogWarning(exception, "Business request failed. Code={Code}, TraceId={TraceId}", code, context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.StatusCode = status; context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new ApiResponse<object?>(false, code, message, null, context.TraceIdentifier));
        }
    }
}

public sealed class TraceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers["X-Trace-Id"].FirstOrDefault();
        context.TraceIdentifier = string.IsNullOrWhiteSpace(incoming) || incoming.Length > 64 ? Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N") : incoming;
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier; await next(context);
    }
}

public sealed class AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAuditWriter audit, ISnowflakeIdGenerator ids, ICurrentUser user, IClock clock)
    {
        var started = Stopwatch.GetTimestamp(); Exception? error = null;
        try { await next(context); }
        catch (Exception ex) { error = ex; throw; }
        finally
        {
            if (context.Request.Path.StartsWithSegments("/api") && context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
            {
                try
                {
                    var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
                    await audit.WriteAsync(new OperationLog
                    {
                        Id = ids.NextId(), TraceId = context.TraceIdentifier, OperatorUserId = user.UserId, OperatorName = user.Name, Module = segments.Length > 2 ? segments[2] : "api",
                        Action = context.Request.Method, RequestMethod = context.Request.Method, RequestPath = context.Request.Path.Value, ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                        Result = (byte)(error == null && context.Response.StatusCode < 400 ? 1 : 0), ErrorCode = error is BusinessException be ? be.Code : error?.GetType().Name,
                        DurationMs = (uint)Math.Min((long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, uint.MaxValue), CreatedAt = clock.UtcNow,
                        ChangeSummary = JsonSerializer.Serialize(new { queryKeys = context.Request.Query.Keys.ToArray() })
                    });
                }
                catch (Exception ex) { logger.LogError(ex, "Failed to append audit log. TraceId={TraceId}", context.TraceIdentifier); }
            }
        }
    }
}
