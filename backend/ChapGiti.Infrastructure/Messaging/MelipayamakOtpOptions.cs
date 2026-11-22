using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ChapGiti.Infrastructure.Messaging;

public sealed class MelipayamakOtpOptions(IConfiguration configuration)
{
    public bool Enabled => configuration.GetValue<bool>("Otp:Enabled");
    public string ApiKey => configuration["Otp:ApiKey"]?.Trim() ?? "";
    public int CodeLength => configuration.GetValue<int?>("Otp:CodeLength") ?? 4;
    public bool Available => Enabled && CodeLength is >= 4 and <= 10 &&
        Regex.IsMatch(ApiKey, "^[a-zA-Z0-9_-]{16,256}$");
}
