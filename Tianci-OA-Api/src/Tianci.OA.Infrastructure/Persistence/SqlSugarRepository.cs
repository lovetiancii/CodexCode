using System.Linq.Expressions;
using SqlSugar;
using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.Infrastructure.Persistence;

public sealed class SqlSugarRepository<T>(ISqlSugarClient db) : IRepository<T> where T : class, new()
{
    public async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => await db.Queryable<T>().InSingleAsync(id);
    public async Task<T?> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => await db.Queryable<T>().Where(predicate).FirstAsync();
    public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => db.Queryable<T>().Where(predicate).AnyAsync();
    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = db.Queryable<T>(); if (predicate != null) query = query.Where(predicate); return await query.ToListAsync();
    }
    public async Task<(IReadOnlyList<T> Items, long Total)> PageAsync(Expression<Func<T, bool>> predicate, int pageNumber, int pageSize, Expression<Func<T, object>>? orderBy = null, bool descending = true, CancellationToken cancellationToken = default)
    {
        var query = db.Queryable<T>().Where(predicate); if (orderBy != null) query = query.OrderBy(orderBy, descending ? OrderByType.Desc : OrderByType.Asc);
        var total = await query.CountAsync(); var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(); return (items, total);
    }
    public async Task InsertAsync(T entity, CancellationToken cancellationToken = default) => _ = await db.Insertable(entity).ExecuteCommandAsync();
    public async Task InsertRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var array = entities.ToArray(); if (array.Length > 0) _ = await db.Insertable(array).ExecuteCommandAsync();
    }
    public Task<int> UpdateAsync(T entity, CancellationToken cancellationToken = default) => db.Updateable(entity).ExecuteCommandAsync();
    public Task<int> UpdateWhereAsync(T entity, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => db.Updateable(entity).Where(predicate).ExecuteCommandAsync();
    public Task<int> DeleteAsync(T entity, CancellationToken cancellationToken = default) => db.Deleteable(entity).ExecuteCommandAsync();
    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => db.Deleteable<T>().Where(predicate).ExecuteCommandAsync();
}

public sealed class SqlSugarUnitOfWork(ISqlSugarClient db) : IUnitOfWork
{
    public Task BeginAsync() { db.Ado.BeginTran(); return Task.CompletedTask; }
    public Task CommitAsync() { db.Ado.CommitTran(); return Task.CompletedTask; }
    public Task RollbackAsync() { db.Ado.RollbackTran(); return Task.CompletedTask; }
}
