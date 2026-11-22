namespace ChapGiti.Domain.Authentication;

public static class OtpPolicy
{
    public const int CodeLifetimeSeconds = 120;
    public const int ResendSeconds = 60;
    public const int MaximumAttempts = 5;
    public const int SendsPerQuarterHour = 5;
    public const int SendsPerDay = 20;
    public const int ResetLifetimeSeconds = 300;
    public const int RegistrationLifetimeSeconds = 300;
}
