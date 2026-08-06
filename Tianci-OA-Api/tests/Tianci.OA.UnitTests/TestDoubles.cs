using System.Linq.Expressions;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;

namespace Tianci.OA.UnitTests;

internal sealed class InMemoryRepository<T>(params T[] seed) : IRepository<T> where T : class
{
    public List<T> Items { get; } = [.. seed];
    public int UpdateWhereResult { get; set; } = 1;

    public Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.FirstOrDefault(x => GetId(x) == id));
    }

    public Task<T?> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.FirstOrDefault(predicate.Compile()));
    }

    public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.Any(predicate.Compile()));
    }

    public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<T>>(predicate is null ? [.. Items] : [.. Items.Where(predicate.Compile())]);
    }

    public Task<(IReadOnlyList<T> Items, long Total)> PageAsync(
        Expression<Func<T, bool>> predicate,
        int pageNumber,
        int pageSize,
        Expression<Func<T, object>>? orderBy = null,
        bool descending = true,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<T> query = Items.Where(predicate.Compile());
        if (orderBy is not null)
        {
            query = descending ? query.OrderByDescending(orderBy.Compile()) : query.OrderBy(orderBy.Compile());
        }

        var all = query.ToArray();
        return Task.FromResult<(IReadOnlyList<T>, long)>((all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(), all.LongLength));
    }

    public Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task InsertRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        Items.AddRange(entities);
        return Task.CompletedTask;
    }

    public Task<int> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.Contains(entity) || Items.Any(x => GetId(x) == GetId(entity)) ? 1 : 0);
    }

    // Services mutate tracked entities before supplying optimistic predicates. The
    // in-memory double therefore models a successful atomic database update while
    // concurrency-conflict branches are tested with a purpose-built failing double.
    public Task<int> UpdateWhereAsync(T entity, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Items.Contains(entity) || Items.Any(x => GetId(x) == GetId(entity))
                ? UpdateWhereResult
                : 0);
    }

    public Task<int> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.Remove(entity) ? 1 : 0);
    }

    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var count = Items.RemoveAll(x => predicate.Compile()(x));
        return Task.FromResult(count);
    }

    private static long GetId(T item)
    {
        return item is AuditedEntity entity
        ? entity.Id
        : (long?)typeof(T).GetProperty("Id")?.GetValue(item) ?? 0;
    }
}

internal sealed class StubIds(long first = 1000) : ISnowflakeIdGenerator
{
    private long _value = first - 1;
    public long NextId()
    {
        return Interlocked.Increment(ref _value);
    }
}

internal sealed class StubClock(DateTime? utcNow = null) : IClock
{
    public DateTime UtcNow { get; } = utcNow ?? new DateTime(2026, 7, 31, 2, 0, 0, DateTimeKind.Utc);
}

internal sealed class StubCurrentUser(long? userId = 7) : ICurrentUser
{
    public bool IsAuthenticated => UserId.HasValue;
    public long? UserId { get; } = userId;
    public string? Name => "tester";
    public string? TraceId => "unit-test";
}

internal sealed class StubDataScope(
    DataScopeContext? context = null) : IDataScopeService
{
    public DataScopeContext Context { get; } = context
        ?? new DataScopeContext(
            DataScope.All,
            7,
            null,
            null,
            new HashSet<long>());

    public Task<DataScopeContext> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Context);
    }

    public Task EnsureCanAccessDepartmentAsync(
        long departmentId,
        CancellationToken cancellationToken = default)
    {
        return EnsureAllowed(
            Context.IncludesDepartment(departmentId),
            cancellationToken);
    }

    public Task EnsureCanAccessEmployeeAsync(
        long employeeId,
        CancellationToken cancellationToken = default)
    {
        return EnsureAllowed(
            Context.Scope == DataScope.All || Context.EmployeeId == employeeId,
            cancellationToken);
    }

    public Task EnsureCanAccessResumeAsync(
        long resumeId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task EnsureCanAccessContractAsync(
        long contractId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task EnsureCanAccessEntryAsync(
        long entryId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static Task EnsureAllowed(
        bool allowed,
        CancellationToken cancellationToken)
    {
        if (!allowed)
        {
            throw new ForbiddenException("当前记录超出你的数据权限范围");
        }

        return Task.CompletedTask;
    }
}

internal sealed class TrackingUnitOfWork : IUnitOfWork
{
    public int Begins
    {
        get; private set;
    }
    public int Commits
    {
        get; private set;
    }
    public int Rollbacks
    {
        get; private set;
    }
    public Task BeginAsync()
    {
        Begins++;
        return Task.CompletedTask;
    }
    public Task CommitAsync()
    {
        Commits++;
        return Task.CompletedTask;
    }
    public Task RollbackAsync()
    {
        Rollbacks++;
        return Task.CompletedTask;
    }
}

internal sealed class StubProtector : ISensitiveDataProtector
{
    public string Protect(string plaintext)
    {
        return $"protected:{plaintext}";
    }

    public string Unprotect(string ciphertext)
    {
        return ciphertext["protected:".Length..];
    }
}

internal sealed class StubCache : ICacheService
{
    public Task<string?> GetAsync(string key)
    {
        return Task.FromResult<string?>(null);
    }

    public Task SetAsync(string key, string value, TimeSpan ttl)
    {
        return Task.CompletedTask;
    }

    public Task RemoveAsync(params string[] keys)
    {
        return Task.CompletedTask;
    }
}

internal sealed class TrackingFileStorage : IFileStorage
{
    public int SaveCalls
    {
        get; private set;
    }
    public Task<string> SaveAsync(Stream stream, string extension, CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        return Task.FromResult($"safe/file{extension}");
    }
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
