using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace ChapGiti.Domain.Common;

public static class Input
{
    public static string Text(JsonObject input, string key, int min = 0, int max = 5000, string fallback = "")
    {
        var node = input[key];
        if (node == null)
        {
            if (min > 0)
                throw new RuleViolation("فیلد " + key + " الزامی است.");
            return fallback;
        }
        if (node is not JsonValue v || !v.TryGetValue<string>(out var text))
            throw new RuleViolation("مقدار " + key + " معتبر نیست.");
        text = text.Trim();
        if (text.Length < min || text.Length > max)
            throw new RuleViolation("طول " + key + " معتبر نیست.");
        return text;
    }
    public static long Number(JsonObject input, string key, long min, long max, long? fallback = null)
    {
        if (input[key] == null && fallback != null)
            return fallback.Value;
        if (input[key] is not JsonValue v)
            throw new RuleViolation("مقدار " + key + " معتبر نیست.");
        if (v.TryGetValue<string>(out var text))
        {
            text = NormalizeDigits(text.Trim());
            if (!Regex.IsMatch(text, "^[0-9]{1,16}$") ||
                !long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var integer) ||
                integer < min || integer > max)
                throw new RuleViolation("مقدار " + key + " معتبر نیست.");
            return integer;
        }
        var n = Numeric(v);
        if (!double.IsFinite(n) || n != Math.Truncate(n) || n < min || n > max)
            throw new RuleViolation("مقدار " + key + " معتبر نیست.");
        return (long)n;
    }
    public static double Numeric(JsonNode? value) => value is JsonValue v
        ? v.TryGetValue<double>(out var d) ? d
        : v.TryGetValue<int>(out var i) ? i
        : v.TryGetValue<long>(out var l) ? l
        : v.TryGetValue<decimal>(out var m) ? (double)m : double.NaN
        : double.NaN;

    public static string NormalizeDigits(string value)
    {
        const string fa = "۰۱۲۳۴۵۶۷۸۹", ar = "٠١٢٣٤٥٦٧٨٩";
        return new string(value.Select(c => fa.Contains(c) ? (char)('0' + fa.IndexOf(c)) :
            ar.Contains(c) ? (char)('0' + ar.IndexOf(c)) : c).ToArray());
    }
    public static bool Flag(JsonObject input, string key, bool fallback = false)
    {
        if (input[key] == null)
            return fallback;
        if (input[key] is not JsonValue v || !v.TryGetValue<bool>(out var value))
            throw new RuleViolation("مقدار " + key + " معتبر نیست.");
        return value;
    }
    public static string Phone(JsonObject input, string key = "phone")
    {
        var value = Text(input, key, 1, 30);
        const string fa = "۰۱۲۳۴۵۶۷۸۹", ar = "٠١٢٣٤٥٦٧٨٩";
        value = new string(value.Select(c => fa.Contains(c) ? (char)('0' + fa.IndexOf(c)) : ar.Contains(c) ? (char)('0' + ar.IndexOf(c)) : c).ToArray());
        if (!Regex.IsMatch(value, @"^09[0-9]{9}$"))
            throw new RuleViolation("شماره همراه را به صورت ۰۹xxxxxxxxx وارد کنید.");
        return value;
    }
    public static string Id(JsonObject input, string key = "idempotencyKey")
    {
        var value = Text(input, key, 1, 36);
        if (!Guid.TryParseExact(value, "D", out _))
            throw new RuleViolation("شناسه درخواست معتبر نیست.");
        return value.ToLowerInvariant();
    }
    public static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
    public static string NewId() => Guid.NewGuid().ToString();
    public static JsonObject Object(JsonNode? node) => node as JsonObject ?? throw new RuleViolation("ساختار اطلاعات معتبر نیست.");
}
