using System.Security.Cryptography;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Contracts;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Files;
using Tianci.OA.Domain.Recruitment;

namespace Tianci.OA.Application.Modules.Files;

public sealed record UploadFileRequest(string BusinessType, string BusinessId, string Category, string OriginalName, string ContentType, long Length, Stream Content);
public sealed record FileDto(string Id, string BusinessType, string BusinessId, string Category, string OriginalName, string ContentType, string Extension, long SizeBytes, DateTime CreatedAt);
public sealed record FileDownload(FileDto Metadata, Stream Content);

public interface IFileService
{
    Task<FileDto> UploadAsync(UploadFileRequest request, CancellationToken ct);
    Task<FileDownload> DownloadAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<FileDto>> ListAsync(string businessType, string businessId, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
}

public sealed class FileService(
    IRepository<SysFile> files, IRepository<Resume> resumes, IRepository<Employee> employees, IRepository<EmployeeEntry> entries, IRepository<EmployeeContract> contracts,
    IFileStorage storage, ISnowflakeIdGenerator ids, IClock clock, ICurrentUser user) : IFileService
{
    private const long MaxSize = 20 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"], [".doc"] = ["application/msword", "application/octet-stream"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip", "application/octet-stream"],
        [".jpg"] = ["image/jpeg"], [".jpeg"] = ["image/jpeg"], [".png"] = ["image/png"]
    };

    public async Task<FileDto> UploadAsync(UploadFileRequest r, CancellationToken ct)
    {
        if (r.Length <= 0 || r.Length > MaxSize) throw new BusinessException("文件大小必须在 1 字节至 20 MB 之间", "FILE_SIZE_INVALID", r.Length > MaxSize ? 413 : 400);
        var businessId = IdParser.Parse(r.BusinessId, "businessId"); await EnsureBusinessExists(r.BusinessType, businessId, ct);
        var safeName = Path.GetFileName(r.OriginalName); if (string.IsNullOrWhiteSpace(safeName) || safeName != r.OriginalName) throw new BusinessException("文件名无效");
        var extension = Path.GetExtension(safeName).ToLowerInvariant(); if (!Allowed.TryGetValue(extension, out var contentTypes) || !contentTypes.Contains(r.ContentType, StringComparer.OrdinalIgnoreCase)) throw new BusinessException("不支持的文件类型", "FILE_TYPE_INVALID");
        await using var memory = new MemoryStream(); await r.Content.CopyToAsync(memory, ct); if (memory.Length != r.Length) throw new BusinessException("文件长度校验失败");
        if (!MatchesSignature(extension, memory.GetBuffer().AsSpan(0, (int)Math.Min(memory.Length, 16)))) throw new BusinessException("文件内容与扩展名不匹配", "FILE_SIGNATURE_INVALID");
        memory.Position = 0; var hash = Convert.ToHexString(SHA256.HashData(memory)).ToLowerInvariant(); memory.Position = 0;
        var storageKey = await storage.SaveAsync(memory, extension, ct);
        var entity = new SysFile { BusinessType = r.BusinessType.ToLowerInvariant(), BusinessId = businessId, Category = r.Category.Trim(), OriginalName = safeName, StorageKey = storageKey, ContentType = r.ContentType, Extension = extension.TrimStart('.'), SizeBytes = r.Length, Sha256 = hash, Status = FileStatus.Active };
        EntityAudit.Create(entity, ids, clock, user);
        try { await files.InsertAsync(entity, ct); }
        catch { await storage.DeleteAsync(storageKey, ct); throw; }
        return ToDto(entity);
    }
    public async Task<FileDownload> DownloadAsync(string id, CancellationToken ct)
    {
        var entity = await Required(id, ct); await EnsureBusinessExists(entity.BusinessType, entity.BusinessId, ct); return new(ToDto(entity), await storage.OpenReadAsync(entity.StorageKey, ct));
    }
    public async Task<IReadOnlyList<FileDto>> ListAsync(string businessType, string businessId, CancellationToken ct)
    {
        var bid = IdParser.Parse(businessId, "businessId"); await EnsureBusinessExists(businessType, bid, ct);
        return (await files.ListAsync(x => x.BusinessType == businessType.ToLower() && x.BusinessId == bid && !x.IsDeleted && x.Status == FileStatus.Active, ct)).Select(ToDto).ToArray();
    }
    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        var entity = await Required(id, ct); await EnsureBusinessExists(entity.BusinessType, entity.BusinessId, ct);
        entity.IsDeleted = true; entity.DeletedAt = clock.UtcNow; entity.DeletedBy = user.UserId; entity.Status = FileStatus.Quarantined; await files.UpdateAsync(entity, ct);
    }
    private async Task EnsureBusinessExists(string type, long id, CancellationToken ct)
    {
        var exists = type.ToLowerInvariant() switch
        {
            "resume" => await resumes.ExistsAsync(x => x.Id == id && !x.IsDeleted, ct),
            "employee" => await employees.ExistsAsync(x => x.Id == id && !x.IsDeleted, ct),
            "entry" => await entries.ExistsAsync(x => x.Id == id && !x.IsDeleted, ct),
            "contract" => await contracts.ExistsAsync(x => x.Id == id && !x.IsDeleted, ct),
            _ => throw new BusinessException("不支持的业务附件类型")
        };
        if (!exists) throw new NotFoundException("附件关联的业务记录不存在");
    }
    private async Task<SysFile> Required(string id, CancellationToken ct) => await files.FirstAsync(x => x.Id == IdParser.Parse(id) && !x.IsDeleted && x.Status == FileStatus.Active, ct) ?? throw new NotFoundException("文件不存在");
    private static bool MatchesSignature(string extension, ReadOnlySpan<byte> h) => extension switch
    {
        ".pdf" => h.StartsWith("%PDF"u8), ".jpg" or ".jpeg" => h.Length >= 3 && h[0] == 0xff && h[1] == 0xd8 && h[2] == 0xff,
        ".png" => h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        ".doc" => h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }),
        ".docx" => h.Length >= 4 && h[0] == 0x50 && h[1] == 0x4b && (h[2] == 0x03 || h[2] == 0x05 || h[2] == 0x07),
        _ => false
    };
    private static FileDto ToDto(SysFile f) => new(f.Id.ToString(), f.BusinessType, f.BusinessId.ToString(), f.Category, f.OriginalName, f.ContentType, f.Extension, f.SizeBytes, f.CreatedAt);
}
