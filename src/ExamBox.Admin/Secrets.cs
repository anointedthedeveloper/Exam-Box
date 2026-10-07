using System.Security.Cryptography;
using System.Text;

namespace ExamBox.Admin;

/// <summary>Encrypts the remembered password so only the current Windows user can read it (DPAPI).</summary>
internal static class Secrets
{
    public static string Protect(string plain) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser));

    public static string? Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText)) return null;
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedText), null, DataProtectionScope.CurrentUser)); }
        catch { return null; }
    }
}
