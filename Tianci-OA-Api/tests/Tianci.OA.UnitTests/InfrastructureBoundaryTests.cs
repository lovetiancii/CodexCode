using System.Text.Json;
using Microsoft.Extensions.Options;
using Tianci.OA.Infrastructure.Files;
using Tianci.OA.Infrastructure.Persistence;
using Tianci.OA.WebApi.Json;

namespace Tianci.OA.UnitTests;

public sealed class InfrastructureBoundaryTests
{
    [Fact]
    public void Snowflake_ids_are_positive_unique_and_monotonic()
    {
        var generator = new SnowflakeIdGenerator(23);
        var ids = Enumerable.Range(0, 5000).Select(_ => generator.NextId()).ToArray();

        Assert.All(ids, id => Assert.True(id > 0));
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.True(ids.SequenceEqual(ids.Order()));
        Assert.All(ids, id => Assert.Equal(23, (id >> 12) & 1023));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1024)]
    public void Snowflake_rejects_invalid_node(int nodeId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeIdGenerator(nodeId));
    }

    [Fact]
    public void Json_writes_long_ids_as_strings_and_accepts_string_ids()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LongAsStringConverter());
        options.Converters.Add(new NullableLongAsStringConverter());

        var json = JsonSerializer.Serialize(new IdEnvelope(9_007_199_254_740_993, null), options);
        var roundTrip = JsonSerializer.Deserialize<IdEnvelope>(
            """{"Id":"9007199254740993","ParentId":"42"}""",
            options);

        Assert.Equal("""{"Id":"9007199254740993","ParentId":null}""", json);
        Assert.Equal(9_007_199_254_740_993, roundTrip!.Id);
        Assert.Equal(42, roundTrip.ParentId);
    }

    [Fact]
    public async Task Local_storage_rejects_path_traversal_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tianci-oa-test-{Guid.NewGuid():N}");
        var storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = root }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.OpenReadAsync("../secret.txt"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.DeleteAsync(Path.GetFullPath("secret.txt")));
    }

    private sealed record IdEnvelope(long Id, long? ParentId);
}
