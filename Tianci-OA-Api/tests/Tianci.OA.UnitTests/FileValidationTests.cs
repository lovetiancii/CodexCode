using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Files;
using Tianci.OA.Domain.Contracts;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Files;
using Tianci.OA.Domain.Recruitment;

namespace Tianci.OA.UnitTests;

public sealed class FileValidationTests
{
    [Fact]
    public async Task Upload_rejects_files_larger_than_20_mb_before_storage()
    {
        var fixture = new Fixture();
        var request = new UploadFileRequest(
            "resume", "1", "resume", "resume.pdf", "application/pdf",
            20L * 1024 * 1024 + 1, Stream.Null);

        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.UploadAsync(request, default));

        Assert.Equal("FILE_SIZE_INVALID", error.Code);
        Assert.Equal(413, error.StatusCode);
        Assert.Equal(0, fixture.Storage.SaveCalls);
    }

    [Fact]
    public async Task Upload_rejects_executable_extension()
    {
        var fixture = new Fixture();
        await using var content = new MemoryStream("MZ"u8.ToArray());
        var request = new UploadFileRequest("resume", "1", "resume", "payload.exe", "application/octet-stream", content.Length, content);

        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.UploadAsync(request, default));

        Assert.Equal("FILE_TYPE_INVALID", error.Code);
        Assert.Equal(0, fixture.Storage.SaveCalls);
    }

    [Fact]
    public async Task Upload_rejects_spoofed_pdf_content()
    {
        var fixture = new Fixture();
        await using var content = new MemoryStream("not a real pdf"u8.ToArray());
        var request = new UploadFileRequest("resume", "1", "resume", "resume.pdf", "application/pdf", content.Length, content);

        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.UploadAsync(request, default));

        Assert.Equal("FILE_SIGNATURE_INVALID", error.Code);
        Assert.Equal(0, fixture.Storage.SaveCalls);
    }

    [Fact]
    public async Task Upload_accepts_matching_pdf_header_and_persists_hash_metadata()
    {
        var fixture = new Fixture();
        await using var content = new MemoryStream("%PDF-1.7\nunit test"u8.ToArray());
        var request = new UploadFileRequest("resume", "1", "resume", "resume.pdf", "application/pdf", content.Length, content);

        var result = await fixture.Service.UploadAsync(request, default);

        Assert.Equal("pdf", result.Extension);
        Assert.Equal("1", result.BusinessId);
        Assert.Equal(1, fixture.Storage.SaveCalls);
        var entity = Assert.Single(fixture.Files.Items);
        Assert.NotNull(entity.Sha256);
        Assert.Equal(64, entity.Sha256.Length);
        Assert.DoesNotContain("resume.pdf", entity.StorageKey);
    }

    private sealed class Fixture
    {
        public InMemoryRepository<SysFile> Files { get; } = new();
        public TrackingFileStorage Storage { get; } = new();
        public FileService Service { get; }

        public Fixture()
        {
            Service = new FileService(
                Files,
                new InMemoryRepository<Resume>(new Resume { Id = 1 }),
                new InMemoryRepository<Employee>(),
                new InMemoryRepository<EmployeeEntry>(),
                new InMemoryRepository<EmployeeContract>(),
                Storage,
                new StubIds(),
                new StubClock(),
                new StubCurrentUser());
        }
    }
}
