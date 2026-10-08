using System.Security.Cryptography;
using System.Text;
using ExamBox.Data;

namespace ExamBox.Services;

/// <summary>
/// Keeps the password an admin set for a student in recoverable (encrypted) form so the student and the
/// admin can look it up. The key lives in the data folder next to the database. Sign-in still checks the hash.
/// </summary>
public sealed class PasswordVault
{
    private readonly byte[] _key;

    public PasswordVault(DbFactory factory)
    {
        var path = Path.Combine(factory.DataDir, "secret.key");
        if (File.Exists(path)) _key = Convert.FromBase64String(File.ReadAllText(path).Trim());
        else
        {
            _key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllText(path, Convert.ToBase64String(_key));
        }
    }

    public string Protect(string plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length]; var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public string? Reveal(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText)) return null;
        try
        {
            var all = Convert.FromBase64String(protectedText);
            var nonce = all[..12]; var tag = all[12..28]; var cipher = all[28..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }
}
