using System.Security.Cryptography;

namespace Loopin.Services;

public class PasswordService
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const string Marker = "PBKDF2";

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return string.Join(
            "$",
            Marker,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool VerifyPassword(string storedPassword, string suppliedPassword)
    {
        if (string.IsNullOrWhiteSpace(storedPassword) || suppliedPassword is null)
        {
            return false;
        }

        if (!storedPassword.StartsWith($"{Marker}$", StringComparison.Ordinal))
        {
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(storedPassword),
                System.Text.Encoding.UTF8.GetBytes(suppliedPassword));
        }

        var parts = storedPassword.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                suppliedPassword,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string storedPassword)
    {
        if (!storedPassword.StartsWith($"{Marker}$", StringComparison.Ordinal))
        {
            return true;
        }

        var parts = storedPassword.Split('$');
        return parts.Length != 4 ||
            !int.TryParse(parts[1], out var iterations) ||
            iterations < Iterations;
    }
}
