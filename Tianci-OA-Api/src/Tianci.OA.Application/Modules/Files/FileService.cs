using System.Security.Cryptography;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Contracts;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Files;
using Tianci.OA.Domain.Recruitment;

namespace Tianci.OA.Application.Modules.Files;

public sealed class FileService(
    IRepository<SysFile> files,
    IRepository<Resume> resumes,
    IRepository<Employee> employees,
    IRepository<EmployeeEntry> entries,
    IRepository<EmployeeContract> contracts,
    IFileStorage storage,
    IDataScopeService dataScope,
    ISnowflakeIdGenerator ids,
    IClock clock,
    ICurrentUser user) : IFileService
{
    private const long MaxSize = 20 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string[]> Allowed =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ["application/pdf"],
            [".doc"] = ["application/msword", "application/octet-stream"],
            [".docx"] =
        [
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/zip",
            "application/octet-stream"
        ],
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"]
        };

    public async Task<FileDto> UploadAsync(
        UploadFileRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Length <= 0 || request.Length > MaxSize)
        {
            throw new BusinessException(
                "文件大小必须在 1 字节至 20 MB 之间",
                "FILE_SIZE_INVALID",
                request.Length > MaxSize ? 413 : 400);
        }

        var businessId = IdParser.Parse(request.BusinessId, "businessId");
        await EnsureBusinessExists(
            request.BusinessType,
            businessId,
            cancellationToken);

        var safeName = Path.GetFileName(request.OriginalName);
        if (string.IsNullOrWhiteSpace(safeName)
            || safeName != request.OriginalName)
        {
            throw new BusinessException("文件名无效");
        }

        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (!Allowed.TryGetValue(extension, out var contentTypes)
            || !contentTypes.Contains(
                request.ContentType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException("不支持的文件类型", "FILE_TYPE_INVALID");
        }

        await using var memory = new MemoryStream();
        await request.Content.CopyToAsync(memory, cancellationToken);
        if (memory.Length != request.Length)
        {
            throw new BusinessException("文件长度校验失败");
        }

        var header = memory
            .GetBuffer()
            .AsSpan(0, (int)Math.Min(memory.Length, 16));

        if (!MatchesSignature(extension, header))
        {
            throw new BusinessException("文件内容与扩展名不匹配", "FILE_SIGNATURE_INVALID");
        }

        memory.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(memory)).ToLowerInvariant();
        memory.Position = 0;
        var storageKey = await storage.SaveAsync(
            memory,
            extension,
            cancellationToken);
        var entity = new SysFile
        {
            BusinessType = request.BusinessType.ToLowerInvariant(),
            BusinessId = businessId,
            Category = request.Category.Trim(),
            OriginalName = safeName,
            StorageKey = storageKey,
            ContentType = request.ContentType,
            Extension = extension.TrimStart('.'),
            SizeBytes = request.Length,
            Sha256 = hash,
            Status = FileStatus.Active
        };

        EntityAudit.Create(entity, ids, clock, user);

        try
        {
            await files.InsertAsync(entity, cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(storageKey, cancellationToken);
            throw;
        }

        return ToDto(entity);
    }

    public async Task<FileDownload> DownloadAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await Required(id, cancellationToken);
        await EnsureBusinessExists(
            entity.BusinessType,
            entity.BusinessId,
            cancellationToken);
        var content = await storage.OpenReadAsync(
            entity.StorageKey,
            cancellationToken);

        return new FileDownload(ToDto(entity), content);
    }

    public async Task<IReadOnlyList<FileDto>> ListAsync(
        string businessType,
        string businessId,
        CancellationToken cancellationToken)
    {
        var parsedBusinessId = IdParser.Parse(businessId, "businessId");
        await EnsureBusinessExists(
            businessType,
            parsedBusinessId,
            cancellationToken);

        var normalizedBusinessType = businessType.ToLowerInvariant();
        var entities = await files.ListAsync(
            file => file.BusinessType == normalizedBusinessType
                && file.BusinessId == parsedBusinessId
                && !file.IsDeleted
                && file.Status == FileStatus.Active,
            cancellationToken);

        return [.. entities.Select(ToDto)];
    }

    public async Task DeleteAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await Required(id, cancellationToken);
        await EnsureBusinessExists(
            entity.BusinessType,
            entity.BusinessId,
            cancellationToken);

        entity.IsDeleted = true;
        entity.DeletedAt = clock.UtcNow;
        entity.DeletedBy = user.UserId;
        entity.Status = FileStatus.Quarantined;

        await files.UpdateAsync(entity, cancellationToken);
    }

    private async Task EnsureBusinessExists(
        string type,
        long id,
        CancellationToken cancellationToken)
    {
        var exists = type.ToLowerInvariant() switch
        {
            "resume" => await resumes.ExistsAsync(
                resume => resume.Id == id && !resume.IsDeleted,
                cancellationToken),
            "employee" => await employees.ExistsAsync(
                employee => employee.Id == id && !employee.IsDeleted,
                cancellationToken),
            "entry" => await entries.ExistsAsync(
                entry => entry.Id == id && !entry.IsDeleted,
                cancellationToken),
            "contract" => await contracts.ExistsAsync(
                contract => contract.Id == id && !contract.IsDeleted,
                cancellationToken),
            _ => throw new BusinessException("不支持的业务附件类型")
        };

        if (!exists)
        {
            throw new NotFoundException("附件关联的业务记录不存在");
        }

        await EnsureDataScopeAsync(type, id, cancellationToken);
    }

    private Task EnsureDataScopeAsync(
        string type,
        long id,
        CancellationToken cancellationToken)
    {
        return type.ToLowerInvariant() switch
        {
            "resume" => dataScope.EnsureCanAccessResumeAsync(
                id,
                cancellationToken),
            "employee" => dataScope.EnsureCanAccessEmployeeAsync(
                id,
                cancellationToken),
            "entry" => dataScope.EnsureCanAccessEntryAsync(
                id,
                cancellationToken),
            "contract" => dataScope.EnsureCanAccessContractAsync(
                id,
                cancellationToken),
            _ => throw new BusinessException("不支持的业务附件类型")
        };
    }

    private async Task<SysFile> Required(
        string id,
        CancellationToken cancellationToken)
    {
        var fileId = IdParser.Parse(id);
        return await files.FirstAsync(
                file => file.Id == fileId
                    && !file.IsDeleted
                    && file.Status == FileStatus.Active,
                cancellationToken)
            ?? throw new NotFoundException("文件不存在");
    }

    private static bool MatchesSignature(
        string extension,
        ReadOnlySpan<byte> header)
    {
        return extension switch
        {
            ".pdf" => header.StartsWith("%PDF"u8),
            ".jpg" or ".jpeg" => header.Length >= 3
                && header[0] == 0xff
                && header[1] == 0xd8
                && header[2] == 0xff,
            ".png" => header.Length >= 8
                && header[..8].SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            ".doc" => header.Length >= 8
                && header[..8].SequenceEqual(
                    new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }),
            ".docx" => header.Length >= 4
                && header[0] == 0x50
                && header[1] == 0x4b
                && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07),
            _ => false
        };
    }

    private static FileDto ToDto(SysFile file)
    {
        return new FileDto(
            file.Id.ToString(),
            file.BusinessType,
            file.BusinessId.ToString(),
            file.Category,
            file.OriginalName,
            file.ContentType,
            file.Extension,
            file.SizeBytes,
            file.CreatedAt);
    }
}
