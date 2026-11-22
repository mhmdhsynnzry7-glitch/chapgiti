using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ChapGiti.Domain.Common;

namespace ChapGiti.Domain.Printing;

public static class PrintCatalogRules
{
    public static readonly IReadOnlyDictionary<string, string> PricingRules = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bookSizes"] = "size-factor-v1",
        ["textPrints"] = "text-ink-v1",
        ["bookPapers"] = "paper-v1",
        ["bookBindings"] = "binding-v1",
        ["coverPrints"] = "cover-print-v1",
        ["coverPapers"] = "cover-paper-v1",
        ["coverCoatings"] = "coating-v1",
        ["finishingServices"] = "finishing-v1"
    };
    public static JsonObject Validate(string kind, string code, string label, string rule, JsonObject metadata)
    {
        if (!PricingRules.TryGetValue(kind, out var supported) || rule != supported)
            throw new RuleViolation("قاعده قیمت‌گذاری این گزینه پشتیبانی نمی‌شود.");
        if (!Regex.IsMatch(code, @"^[a-zA-Z0-9][a-zA-Z0-9_-]{0,79}$") || code is "constructor" or "prototype" or "__proto__")
            throw new RuleViolation("کد ثابت گزینه معتبر نیست.");
        if (label.Trim().Length is < 2 or > 160)
            throw new RuleViolation("عنوان گزینه باید بین ۲ تا ۱۶۰ کاراکتر باشد.");
        var m = (JsonObject)metadata.DeepClone();
        double Number(string key, double min, double max)
        {
            var n = Input.Numeric(m[key]);
            if (!double.IsFinite(n) || n < min || n > max)
                throw new RuleViolation("مقدار «" + key + "» در تنظیمات چاپ معتبر نیست.");
            return n;
        }
        bool Flag(string key)
        {
            if (m[key] is not JsonValue v || !v.TryGetValue<bool>(out var b))
                throw new RuleViolation("گزینه «" + key + "» معتبر نیست.");
            return b;
        }
        string Text(string key, params string[] values)
        {
            if (m[key] is not JsonValue v || !v.TryGetValue<string>(out var value) || !values.Contains(value))
                throw new RuleViolation("گزینه «" + key + "» پشتیبانی نمی‌شود.");
            return value;
        }
        switch (kind)
        {
            case "bookSizes":
                Text("unit", "cm");
                Number("width", 1, 100);
                Number("height", 1, 100);
                Number("factor", .1, 20);
                var custom = Flag("isCustom");
                var minWidth = Number("minWidth", 1, 100);
                var maxWidth = Number("maxWidth", minWidth, 100);
                var minHeight = Number("minHeight", 1, 100);
                var maxHeight = Number("maxHeight", minHeight, 100);
                if (custom && (Number("width", 1, 100) < minWidth || Number("width", 1, 100) > maxWidth || Number("height", 1, 100) < minHeight || Number("height", 1, 100) > maxHeight))
                    throw new RuleViolation("ابعاد پیش‌فرض باید در بازه مجاز باشد.");
                break;
            case "textPrints":
                var bw = Flag("bw");
                var color = Flag("color");
                if (!bw && !color)
                    throw new RuleViolation("حداقل یک نوع صفحه باید پشتیبانی شود.");
                var bwFactor = Number("bwFactor", 0, 20);
                var colorFactor = Number("colorFactor", 0, 20);
                var noPrint = Flag("noPrint");
                var technology = Text("technology", "digital", "toner", "ink", "mixed", "none");
                if (noPrint ? technology != "none" || !bw || color || bwFactor != 0 || colorFactor != 0 : technology == "none" || bw && bwFactor == 0 || color && colorFactor == 0)
                    throw new RuleViolation("فناوری و ضرایب چاپ با نوع صفحه سازگار نیست.");
                break;
            case "bookPapers":
                Number("grams", 20, 1000);
                Number("caliper", .001, 2);
                Text("type", "white", "cream", "coated", "label", "colored");
                break;
            case "bookBindings":
                var basis = Text("base", "soft", "hard", "spiral");
                Number("factor", 0, 20);
                var hard = Flag("hard");
                var spiral = Flag("extraSpiral");
                if (hard != (basis == "hard") || spiral && !hard)
                    throw new RuleViolation("پایه صحافی و سازگاری جلد سخت یکسان نیست.");
                break;
            case "coverPrints":
                var coverFactor = Number("factor", 0, 20);
                var none = Flag("none");
                var plastic = Flag("plastic");
                if (none && plastic || (none || plastic) && coverFactor != 0)
                    throw new RuleViolation("قاعده چاپ جلد با نوع جلد سازگار نیست.");
                break;
            case "coverPapers":
                Number("factor", 0, 20);
                break;
            case "coverCoatings":
                if (!Flag("laminated") && Flag("matte"))
                    throw new RuleViolation("پوشش مات باید سلفون داشته باشد.");
                Flag("matte");
                break;
            case "finishingServices":
                Flag("matte");
                Flag("hard");
                Text("rateKey", "roundCorners", "shrink", "goldFoil", "silverFoil", "spotUV", "endpaperBw", "endpaperColor", "bookmark", "coloredEdges", "metallicEdges", "bellyBand", "thickEdges", "hardCoverJacket", "ribbon", "laminatedEndpaper");
                break;
        }
        m["label"] = label.Trim();
        return m;
    }
}
