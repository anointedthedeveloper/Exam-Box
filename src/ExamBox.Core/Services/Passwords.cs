using System.Security.Cryptography;
using ExamBox.Models;
using Microsoft.AspNetCore.Identity;

namespace ExamBox.Services;

public static class Passwords
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(string password) => Hasher.HashPassword(null!, password);

    public static bool Verify(string hash, string password) =>
        Hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;

    /// <summary>Random password without ambiguous characters.</summary>
    public static string Generate(int length = 10)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        return string.Create(length, chars, (span, c) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = c[RandomNumberGenerator.GetInt32(c.Length)];
        });
    }

    /// <summary>Only requires a non-empty password; admins decide how strong theirs should be.</summary>
    public static string? Validate(string password) =>
        string.IsNullOrEmpty(password) ? "Enter a password." : null;
}
