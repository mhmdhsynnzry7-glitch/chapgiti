namespace ChapGiti.Application.Features;

internal static class ContactLinks
{
    public static string Validate(string platform, string value)
    {
        if (value == "") return "";
        if (Regex.IsMatch(value, @"[\x00-\x20\\]") || !Uri.TryCreate(value, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || !url.IsDefaultPort || url.UserInfo != "" || url.Query != "" || url.Fragment != "")
            throw Invalid();
        var host = url.IdnHost.ToLowerInvariant();
        var path = Uri.UnescapeDataString(url.AbsolutePath);
        var valid = platform switch
        {
            "instagram" => host is "instagram.com" or "www.instagram.com" && Regex.IsMatch(path, @"^/[A-Za-z0-9_.]{1,30}/?$"),
            "telegram" => host is "t.me" or "telegram.me" && Regex.IsMatch(path, @"^/(?:s/)?[A-Za-z0-9_]{1,100}/?$|^/\+[A-Za-z0-9_-]{1,100}/?$|^/joinchat/[A-Za-z0-9_-]{1,100}/?$"),
            "bale" => host == "ble.ir" && Regex.IsMatch(path, @"^/(?:join/)?[A-Za-z0-9_-]{1,100}/?$"),
            _ => false
        };
        if (!valid) throw Invalid();
        return url.AbsoluteUri;
    }

    private static UseCaseException Invalid() => new(ErrorKind.Invalid,
        "نشانی شبکهٔ اجتماعی باید پیوند HTTPS معتبرِ حساب یا کانال همان شبکه، بدون پارامتر اضافه باشد.", code: "SOCIAL_URL_INVALID");
}
