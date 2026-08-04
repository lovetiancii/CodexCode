namespace Tianci.OA.Application.Modules.Files;

public sealed record UploadFileRequest(
    string BusinessType,
    string BusinessId,
    string Category,
    string OriginalName,
    string ContentType,
    long Length,
    Stream Content);

public sealed record FileDto(
    string Id,
    string BusinessType,
    string BusinessId,
    string Category,
    string OriginalName,
    string ContentType,
    string Extension,
    long SizeBytes,
    DateTime CreatedAt);

public sealed record FileDownload(
    FileDto Metadata,
    Stream Content);
