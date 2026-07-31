using StackExchange.Redis;
using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.Infrastructure.Caching;

public sealed class RedisCacheService(string? connectionString) : ICacheService, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ConnectionMultiplexer? _connection;
    private async Task<IDatabase?> DatabaseAsync()
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            if (_connection is { IsConnected: true }) return _connection.GetDatabase();
            await _gate.WaitAsync();
            try { _connection ??= await ConnectionMultiplexer.ConnectAsync(connectionString); return _connection.GetDatabase(); }
            finally { _gate.Release(); }
        }
        catch { return null; }
    }
    public async Task<string?> GetAsync(string key) { try { var db = await DatabaseAsync(); return db == null ? null : (string?)await db.StringGetAsync(key); } catch { return null; } }
    public async Task SetAsync(string key, string value, TimeSpan ttl) { try { var db = await DatabaseAsync(); if (db != null) await db.StringSetAsync(key, value, ttl); } catch { } }
    public async Task RemoveAsync(params string[] keys) { try { var db = await DatabaseAsync(); if (db != null) await db.KeyDeleteAsync(keys.Select(x => (RedisKey)x).ToArray()); } catch { } }
    public async ValueTask DisposeAsync() { if (_connection != null) await _connection.DisposeAsync(); _gate.Dispose(); }
}
