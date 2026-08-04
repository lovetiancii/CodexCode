using System.Diagnostics;

namespace Tianci.OA.WebApi.Middleware;

public sealed class TraceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incomingTraceId = context.Request.Headers["X-Trace-Id"]
            .FirstOrDefault();
        var generatedTraceId = Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        context.TraceIdentifier = string.IsNullOrWhiteSpace(incomingTraceId)
            || incomingTraceId.Length > 64
                ? generatedTraceId
                : incomingTraceId;
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;

        await next(context);
    }
}
