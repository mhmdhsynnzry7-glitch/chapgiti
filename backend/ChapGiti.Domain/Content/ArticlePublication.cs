using ChapGiti.Domain.Common;
using System.Text.RegularExpressions;

namespace ChapGiti.Domain.Content;

public static class ArticlePublication
{
    public static void Transition(string? previous,string next)
    {
        if(next is not ("draft" or "published" or "archived") || previous==null&&next!="draft" || previous=="archived"&&next=="published")
            throw new RuleViolation("مقاله جدید ابتدا پیش‌نویس می‌شود؛ مقاله بایگانی‌شده را پیش از انتشار به پیش‌نویس برگردانید.");
    }
    // Markdown v1 deliberately has no embedded HTML, scripts, arbitrary embeds or active URLs.
    public static void ValidateContent(string content)
    {
        if(content.Length>40000||content.Any(c=>char.IsControl(c)&&c is not ('\r' or '\n' or '\t'))||Regex.IsMatch(content,@"<\s*/?\s*[a-zA-Z!]|(?:javascript|vbscript|data)\s*:",RegexOptions.IgnoreCase))
            throw new RuleViolation("محتوا باید متن یا Markdown امن باشد؛ HTML، اسکریپت و پیوند اجرایی مجاز نیست.");
        foreach(Match match in Regex.Matches(content,@"(!?)\[[^\]]*\]\(([^\s)]+)\)"))
        {
            var target=match.Groups[2].Value;
            var image=match.Groups[1].Value=="!";
            if(image ? !Regex.IsMatch(target,@"^/api/banner-assets/[a-fA-F0-9-]{36}$") : !SafeLink(target))
                throw new RuleViolation("نشانی تصویر یا پیوند مقاله مجاز نیست؛ تصویر را از بارگذاری سایت انتخاب کنید.");
        }
    }
    public static bool SafeLink(string value) => value.StartsWith('/')&&!value.StartsWith("//")&&!value.Contains('\\')&&!value.Any(char.IsControl)
        ||Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.UserInfo=="";
    public static void ValidatePublished(string title,string excerpt,string content,string category,string? coverId,string alt)
    {
        if(title.Trim().Length<5||excerpt.Trim().Length<10||content.Trim().Length<30||category.Trim().Length<2||coverId!=null&&alt.Trim().Length<3)
            throw new RuleViolation("برای انتشار، عنوان، خلاصه، دسته‌بندی و متن معتبر و توضیح تصویر شاخص را کامل کنید.");
    }
}
