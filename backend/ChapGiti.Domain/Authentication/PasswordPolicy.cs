using ChapGiti.Domain.Common;

namespace ChapGiti.Domain.Authentication;

/// <summary>Creation/change policy only. Historical credentials are verified without this check.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 6;
    public const int MaximumLength = 128;
    public const string Guidance = "رمز عبور باید حداقل ۶ کاراکتر، شامل یک حرف بزرگ انگلیسی (A-Z) و یک کاراکتر خاص مانند ! یا @ باشد.";
    // Printable ASCII punctuation; whitespace, digits and non-English letters aren't special characters.
    public static bool IsValid(string? value) => value is { Length: >= MinimumLength and <= MaximumLength }
        && value.Any(c => c is >= 'A' and <= 'Z')
        && value.Any(c => c is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~');

    public static string Validate(string? value)
    {
        if (!IsValid(value)) throw new RuleViolation(Guidance);
        return value!;
    }

    public static string Confirm(string? value, string? confirmation)
    {
        Validate(value);
        if (value != confirmation) throw new RuleViolation("تکرار رمز عبور یکسان نیست.");
        return value!;
    }
}
