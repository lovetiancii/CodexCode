using System.Linq.Expressions;
using Tianci.OA.Domain.Audit;
using Tianci.OA.Domain.Common;

namespace Tianci.OA.Application.Abstractions;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<T?> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<T> Items, long Total)> PageAsync(Expression<Func<T, bool>> predicate, int pageNumber, int pageSize, Expression<Func<T, object>>? orderBy = null, bool descending = true, CancellationToken cancellationToken = default);
    Task InsertAsync(T entity, CancellationToken cancellationToken = default);
    Task InsertRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task<int> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task<int> UpdateWhereAsync(T entity, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task BeginAsync();
    Task CommitAsync();
    Task RollbackAsync();
}

public interface ISnowflakeIdGenerator { long NextId(); }
public interface IClock { DateTime UtcNow { get; } }
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    long? UserId { get; }
    string? Name { get; }
    string? TraceId { get; }
}

public interface IAuditWriter
{
    Task WriteAsync(OperationLog log, CancellationToken cancellationToken = default);
}

public interface ICacheService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan ttl);
    Task RemoveAsync(params string[] keys);
}

public interface ISensitiveDataProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public interface IFileStorage
{
    Task<string> SaveAsync(Stream stream, string extension, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public sealed record TokenResult(string AccessToken, DateTime ExpiresAtUtc);
public interface ITokenIssuer { TokenResult Issue(long userId, string username, string displayName, string securityStamp); }
public interface IPasswordService
{
    string Hash(string username, string password);
    bool Verify(string username, string hash, string password);
}

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(long userId, string permissionCode);
    Task<IReadOnlySet<string>> GetPermissionsAsync(long userId);
}

public static class EntityAudit
{
    public static void Create(AuditedEntity entity, ISnowflakeIdGenerator ids, IClock clock, ICurrentUser user)
    {
        entity.Id = ids.NextId();
        entity.CreatedAt = entity.UpdatedAt = clock.UtcNow;
        entity.CreatedBy = entity.UpdatedBy = user.UserId;
    }

    public static void Update(AuditedEntity entity, IClock clock, ICurrentUser user)
    {
        entity.UpdatedAt = clock.UtcNow;
        entity.UpdatedBy = user.UserId;
    }
}
