namespace Tianci.OA.Application.Common;

public sealed record PageRequest(int PageNumber = 1, int PageSize = 20)
{
    public int SafePageNumber => Math.Max(PageNumber, 1);
    public int SafePageSize => Math.Clamp(PageSize, 1, 100);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, long Total);

public class BusinessException(string message, string code = "BUSINESS_ERROR", int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string message) : BusinessException(message, "NOT_FOUND", 404);
public sealed class ForbiddenException(string message = "无权执行此操作") : BusinessException(message, "FORBIDDEN", 403);
public sealed class ConflictException(string message, string code = "CONFLICT") : BusinessException(message, code, 409);

public static class IdParser
{
    public static long Parse(string value, string field = "id")
    {
        return long.TryParse(value, out var id) && id > 0 ? id : throw new BusinessException($"{field} 格式无效", "VALIDATION_ERROR");
    }

    public static long? ParseNullable(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Parse(value, field);
    }
}
