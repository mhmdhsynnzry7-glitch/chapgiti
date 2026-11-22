namespace ChapGiti.Application.Authentication;

public sealed record CustomerProfileResponse(string Id, string Phone, string FirstName, string LastName,
    bool PhoneVerified, bool HasAccount, bool IsAdmin);
public sealed record AdministratorProfileResponse(string UserId, string Email, string DisplayName, bool Legacy, string Username);
public sealed record UserSummaryResponse(string Id, string FirstName, string LastName, string Phone,
    string CreatedAt, bool PhoneVerified, string[] Roles, long? LastLoginAt, long LoginCount, long ActiveSessions, long OrderCount);
public sealed record UserPageResponse(IReadOnlyList<UserSummaryResponse> Items, long Total, int Page, int PageSize);
public sealed record OtpChallengeResponse(bool Ok, string ChallengeId, long ExpiresAt, int ResendAfterSeconds,
    int CodeLength, string Message);
public sealed record RecoveryGrantResponse(bool Ok, string ResetToken, long ExpiresAt);
public sealed record RegistrationGrantResponse(bool Ok, string RegistrationToken, string Phone, long ExpiresAt);
public sealed record CustomerStartResponse(bool Ok, string Step, string Phone);
