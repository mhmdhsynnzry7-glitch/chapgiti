using System.Security.Cryptography;
using System.Text;
using ChapGiti.Infrastructure.Messaging;

namespace ChapGiti.Infrastructure.Security;

public sealed class OtpCodeProtector(MelipayamakOtpOptions options) : IOtpCodeProtector
{
    public string Hash(string challengeId, string phone, string purpose, string code)
    {
        if (!options.Available)
            throw new InvalidOperationException("OTP is not configured.");
        // Domain separation: the high-entropy console credential also protects short codes at rest.
        var key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.ApiKey), Encoding.UTF8.GetBytes("ChapGiti.OTP.v1"));
        return Convert.ToHexStringLower(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes(challengeId + "|" + phone + "|" + purpose + "|" + code)));
    }

    public bool Matches(string expectedHash, string challengeId, string phone, string purpose, string code)
    {
        if (!options.Available || expectedHash.Length != 64)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHash),
            Encoding.ASCII.GetBytes(Hash(challengeId, phone, purpose, code)));
    }
}
