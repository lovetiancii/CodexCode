namespace Tianci.OA.Application.Modules.Files;

public interface IFileService
{
    Task<FileDto> UploadAsync(
        UploadFileRequest request,
        CancellationToken cancellationToken);

    Task<FileDownload> DownloadAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FileDto>> ListAsync(
        string businessType,
        string businessId,
        CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
