namespace ChapGiti.Application.Authentication;

/// <summary>The host permits disabled verification only in local Development.</summary>
public sealed record OtpVerificationPolicy(bool Enabled = true)
{
    public const int DevelopmentCodeLength = 4;
    // A challenge marker, not a code or secret. Normal verification must never accept it.
    public static readonly string DevelopmentChallengeMarker = new('0', 64);

    public bool Bypasses(string purpose) => !Enabled && (purpose is "register" or "reset");
}
