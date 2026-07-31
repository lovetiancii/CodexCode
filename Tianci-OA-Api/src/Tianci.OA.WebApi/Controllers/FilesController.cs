using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tianci.OA.Application.Modules.Files;
using Tianci.OA.WebApi.Authorization;

namespace Tianci.OA.WebApi.Controllers;

[ApiController, Authorize, Route("api/v1/files")]
public sealed class FilesController(IFileService service) : ControllerBase
{
    [HttpPost, RequestSizeLimit(20 * 1024 * 1024), Permission("file:upload")]
    public async Task<FileDto> Upload([FromForm] string businessType, [FromForm] string businessId, [FromForm] string category, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await service.UploadAsync(new(businessType, businessId, category, file.FileName, file.ContentType, file.Length, stream), ct);
    }
    [HttpGet, Permission("file:download")] public Task<IReadOnlyList<FileDto>> List([FromQuery] string businessType, [FromQuery] string businessId, CancellationToken ct) => service.ListAsync(businessType, businessId, ct);
    [HttpGet("{id}/download"), Permission("file:download")]
    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        var result = await service.DownloadAsync(id, ct);
        return File(result.Content, result.Metadata.ContentType, result.Metadata.OriginalName, enableRangeProcessing: true);
    }
    [HttpDelete("{id}"), Permission("file:delete")] public async Task<IActionResult> Delete(string id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}
