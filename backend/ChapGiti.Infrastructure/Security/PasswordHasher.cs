using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
namespace ChapGiti.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        ChapGiti.Domain.Authentication.PasswordPolicy.Validate(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
        return "pbkdf2-sha256$600000$" + Convert.ToHexStringLower(salt) + "$" + Convert.ToHexStringLower(key);
    }
    public bool Verify(string password, string? encoded)
    {
        if (password.Length > 128)
            return false;
        try
        {
            var parts = (encoded ?? "").Split('$');
            byte[] actual, expected;
            if (parts.Length == 3 && parts[0] == "scrypt" && parts[1].Length == 32 && parts[2].Length == 128)
            {
                actual = SCrypt.Generate(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(parts[1]), 32768, 8, 1, 64);
                expected = Convert.FromHexString(parts[2]);
            }
            else if (parts.Length == 4 && parts[0] == "pbkdf2-sha256" && int.TryParse(parts[1], out var iterations) && iterations is >= 100000 and <= 2000000)
            {
                expected = Convert.FromHexString(parts[3]);
                if (expected.Length != 32)
                    return false;
                actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromHexString(parts[2]), iterations, HashAlgorithmName.SHA256, 32);
            }
            else
            {
                Rfc2898DeriveBytes.Pbkdf2(password, new byte[16], 600000, HashAlgorithmName.SHA256, 32);
                return false;
            }
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
