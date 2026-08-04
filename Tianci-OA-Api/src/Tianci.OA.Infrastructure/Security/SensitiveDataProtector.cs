using Microsoft.AspNetCore.DataProtection;
using Tianci.OA.Application.Abstractions;

namespace Tianci.OA.Infrastructure.Security;

public sealed class SensitiveDataProtector : ISensitiveDataProtector
{
    private readonly IDataProtector _protector;

    public SensitiveDataProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Tianci.OA.SensitiveData.v1");
    }

    public string Protect(string plaintext)
    {
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        return _protector.Unprotect(ciphertext);
    }
}
