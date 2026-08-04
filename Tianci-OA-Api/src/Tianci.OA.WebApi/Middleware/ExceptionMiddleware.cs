using Tianci.OA.Application.Common;

namespace Tianci.OA.WebApi.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var (status, code, message) = exception switch
            {
                BusinessException businessException =>
                    (businessException.StatusCode,
                    businessException.Code,
                    businessException.Message),
                UnauthorizedAccessException =>
                    (401, "UNAUTHORIZED", "未登录或会话已失效"),
                _ =>
                    (500, "INTERNAL_ERROR", "服务器内部错误")
            };

            if (status == 500)
            {
                logger.LogError(
                    exception,
                    "Unhandled exception. TraceId={TraceId}",
                    context.TraceIdentifier);
            }
            else
            {
                logger.LogWarning(
                    exception,
                    "Business request failed. Code={Code}, TraceId={TraceId}",
                    code,
                    context.TraceIdentifier);
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            var response = new ApiResponse<object?>(
                false,
                code,
                message,
                null,
                context.TraceIdentifier);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
