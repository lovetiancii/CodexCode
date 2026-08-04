using StackExchange.Redis;
using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService, IAsyncDisposable
{
    private readonly string? _connectionString;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private ConnectionMultiplexer? _connection;

    public RedisCacheService(string? connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var database = await GetDatabaseAsync();

            return database == null
                ? null
                : (string?)await database.StringGetAsync(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan ttl)
    {
        try
        {
            var database = await GetDatabaseAsync();

            if (database != null)
            {
                await database.StringSetAsync(key, value, ttl);
            }
        }
        catch (Exception)
        {
            // 缓存不可用时降级，不阻断主业务。
        }
    }

    public async Task RemoveAsync(params string[] keys)
    {
        try
        {
            var database = await GetDatabaseAsync();

            if (database != null)
            {
                var redisKeys = keys.Select(key => (RedisKey)key).ToArray();
                await database.KeyDeleteAsync(redisKeys);
            }
        }
        catch (Exception)
        {
            // 缓存不可用时降级，不阻断主业务。
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }

    private async Task<IDatabase?> GetDatabaseAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        try
        {
            if (_connection is { IsConnected: true })
            {
                return _connection.GetDatabase();
            }

            await _connectionLock.WaitAsync();

            try
            {
                _connection ??= await ConnectionMultiplexer.ConnectAsync(
                    _connectionString);

                return _connection.GetDatabase();
            }
            finally
            {
                _connectionLock.Release();
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}
