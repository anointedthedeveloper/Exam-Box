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

    /// <summary>Random temporary password without ambiguous characters.</summary>
    public static string Generate(int length = 10)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        return string.Create(length, chars, (span, c) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = c[RandomNumberGenerator.GetInt32(c.Length)];
        });
    }

    public static string? Validate(string password)
    {
        if (password.Length < 8) return "Password must be at least 8 characters.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "Password must contain letters and numbers.";
        return null;
    }
}
