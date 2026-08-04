using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.Infrastructure.Persistence;

public sealed class SnowflakeIdGenerator : ISnowflakeIdGenerator
{
    private const long Epoch = 1704067200000L;
    private readonly long _node;
    private readonly Lock _sync = new();
    private long _last = -1;
    private long _sequence;
    public SnowflakeIdGenerator(int nodeId)
    {
        if (nodeId is < 0 or > 1023)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId));
        }

        _node = nodeId;
    }
    public long NextId()
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now < _last)
            {
                throw new InvalidOperationException("系统时钟回拨，雪花 ID 生成被拒绝");
            }

            if (now == _last)
            {
                _sequence = (_sequence + 1) & 4095;
                if (_sequence == 0)
                {
                    do
                    {
                        now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    } while (now <= _last);
                }
            }
            else
            {
                _sequence = 0;
            }

            _last = now;
            return ((now - Epoch) << 22) | (_node << 12) | _sequence;
        }
    }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
