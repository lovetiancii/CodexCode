using Tianci.OA.Domain.Common;

namespace Tianci.OA.Domain.Files;

public sealed class SysFile : AuditedEntity
{
    public string BusinessType { get; set; } = "";
    public long BusinessId { get; set; }
    public string Category { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string StorageProvider { get; set; } = "local";
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Extension { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public FileStatus Status { get; set; } = FileStatus.Active;
}
