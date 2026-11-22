using ChapGiti.Domain.Authentication;

namespace ChapGiti.Application.Authentication;

public sealed record AccessSession(string Id, string? CustomerId, string? LegacyAdminEmail,
    string AccessHash, long AccessExpires, long Expires, long? RevokedAt);

public sealed record RefreshCredential(string Hash, string SessionId, long Expires, long? ConsumedAt);

public sealed record PendingRegistration(string ChallengeId, string FirstName, string LastName, string PasswordHash);

public sealed record AuthenticationPolicy(int OtpLifetimeSeconds = OtpPolicy.CodeLifetimeSeconds)
{
    public const string AdministratorUsername = "admin";
    public const int AccessSeconds = 900;
    public const int RefreshSeconds = 30 * 24 * 60 * 60;
    public const int ConcurrentRefreshSeconds = 10;
}
