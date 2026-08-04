using Microsoft.AspNetCore.Identity;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Domain.Identity;

namespace Tianci.OA.Infrastructure.Security;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<SysUser> _hasher = new();

    public string Hash(string username, string password)
    {
        var user = new SysUser
        {
            Username = username
        };

        return _hasher.HashPassword(user, password);
    }

    public bool Verify(string username, string hash, string password)
    {
        var user = new SysUser
        {
            Username = username
        };
        var result = _hasher.VerifyHashedPassword(user, hash, password);

        return result != PasswordVerificationResult.Failed;
    }
}
