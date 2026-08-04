using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Files;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/files")]
public sealed class FilesController : ControllerBase
{
    private const long MaxUploadSize = 20 * 1024 * 1024;

    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadSize)]
    [Permission("file:upload")]
    public async Task<FileDto> Upload(
        [FromForm] string businessType,
        [FromForm] string businessId,
        [FromForm] string category,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();

        var request = new UploadFileRequest(
            businessType,
            businessId,
            category,
            file.FileName,
            file.ContentType,
            file.Length,
            stream);

        return await _fileService.UploadAsync(request, cancellationToken);
    }

    [HttpGet]
    [Permission("file:download")]
    public Task<IReadOnlyList<FileDto>> List(
        [FromQuery] string businessType,
        [FromQuery] string businessId,
        CancellationToken cancellationToken)
    {
        return _fileService.ListAsync(
            businessType,
            businessId,
            cancellationToken);
    }

    [HttpGet("{id}/download")]
    [Permission("file:download")]
    public async Task<IActionResult> Download(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _fileService.DownloadAsync(id, cancellationToken);

        return File(
            result.Content,
            result.Metadata.ContentType,
            result.Metadata.OriginalName,
            enableRangeProcessing: true);
    }

    [HttpDelete("{id}")]
    [Permission("file:delete")]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        await _fileService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
