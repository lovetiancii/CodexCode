using Microsoft.Extensions.Options;
using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public string RootPath { get; set; } = "App_Data/files";
}

public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);
    public async Task<string> SaveAsync(Stream stream, string extension, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root); var date = DateTime.UtcNow.ToString("yyyy/MM/dd"); var directory = Path.Combine(_root, date); Directory.CreateDirectory(directory);
        var key = $"{date}/{Guid.NewGuid():N}{extension}".Replace('\\', '/'); var path = Resolve(key);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous); await stream.CopyToAsync(output, cancellationToken); return key;
    }
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storageKey); if (!File.Exists(path)) throw new FileNotFoundException("存储文件不存在");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));
    }
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { var path = Resolve(storageKey); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
    private string Resolve(string key)
    {
        if (key.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(key)) throw new InvalidOperationException("无效存储键");
        var path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("检测到路径穿越");
        return path;
    }
}
