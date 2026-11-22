namespace ChapGiti.Infrastructure.Persistence;

// CK_OtpDelivery permits only pending/sent/failed. A pending registration row with
// all five attempts reserved and no OTP hash cannot be a usable SMS challenge.
// This combination tags grant rows without changing the deployed schema or
// shortening their purpose/mode-bound 256-bit token digest.
internal static class RegistrationGrantStorage
{
    internal const string Predicate = "purpose='register' AND delivery_status='pending' AND attempts=5 AND code_hash IS NULL";

    internal static bool IsGrant(Dictionary<string, object?> row) =>
        row.Str("purpose") == "register" && row.Str("delivery_status") == "pending" &&
        row.Num("attempts") == 5 && row.GetValueOrDefault("code_hash") == null;
}
