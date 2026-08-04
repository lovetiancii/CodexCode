namespace Tianci.OA.WebApi.Middleware;

public sealed record ApiResponse<T>(
    bool Success,
    string Code,
    string Message,
    T? Data,
    string TraceId);
