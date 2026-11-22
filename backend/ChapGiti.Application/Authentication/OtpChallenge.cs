namespace ChapGiti.Application.Authentication;

public sealed record OtpChallenge(
    string Id,
    string Phone,
    string Purpose,
    string? CustomerId,
    string? CodeHash,
    long CreatedAt,
    long Expires,
    int Attempts,
    bool Consumed,
    string DeliveryStatus);

public sealed record PasswordResetGrant(string TokenHash, string CustomerId, string Phone, long Expires, bool Consumed);

public sealed record RegistrationGrant(string TokenHash, string Phone, long Expires, bool Consumed);
