using ChapGiti.Application.Authentication;

namespace ChapGiti.Infrastructure.Messaging;

public static class OtpVerificationConfiguration
{
    public static OtpVerificationPolicy Read(IConfiguration configuration, IHostEnvironment environment)
    {
        // The exact environment variable also works when running .NET independently.
        var value = configuration["OTP_VERIFICATION_ENABLED"] ?? configuration["Otp:VerificationEnabled"];
        var enabled = true;
        if (value != null && !bool.TryParse(value, out enabled))
            throw new InvalidOperationException("OTP_VERIFICATION_ENABLED must be true or false.");
        if (!enabled && (!environment.IsDevelopment() ||
            !Uri.TryCreate(configuration["Site:Url"], UriKind.Absolute, out var site) || !site.IsLoopback ||
            site.Scheme is not ("http" or "https")))
            throw new InvalidOperationException("OTP_VERIFICATION_ENABLED=false is permitted only in local Development with a loopback Site:Url.");
        return new(enabled);
    }
}
