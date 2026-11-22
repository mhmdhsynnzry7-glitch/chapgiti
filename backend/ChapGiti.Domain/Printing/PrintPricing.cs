using System.Text.Json;
using System.Text.Json.Nodes;
using ChapGiti.Domain.Common;
namespace ChapGiti.Domain.Printing;

public sealed class PrintPricing(PrintCatalog catalog)
{
    private JsonObject Options => catalog.Options;
    private JsonObject Option(string group, string key) => Options[group]?[key] as JsonObject ?? throw new RuleViolation("گزینه چاپ معتبر نیست.");
    private string Choice(JsonObject input, string key, string group)
    {
        var value = Input.Text(input, key, 1, 80);
        if (Options[group]?[value] == null)
            throw new RuleViolation("گزینه " + key + " معتبر نیست.");
        return value;
    }
    private static string Enum(JsonObject input, string key, params string[] values)
    {
        var value = Input.Text(input, key, 1, 80);
        if (!values.Contains(value))
            throw new RuleViolation("گزینه " + key + " معتبر نیست.");
        return value;
    }
    private static double N(JsonObject item, string key) => Input.Numeric(item[key]);
    private bool Flag(string group, string code, string field, bool fallback = false) => Options[group]?[code] is JsonObject o && o[field] is JsonValue value ? value.GetValue<bool>() : fallback;
    private double Numeric(string group, string code, string field, double fallback) => Options[group]?[code] is JsonObject o && o[field] != null ? N(o, field) : fallback;
    public JsonObject Validate(JsonNode? value)
    {
        var s = (JsonObject)Input.Object(value).DeepClone();
        s["pages"] = Input.Number(s, "pages", 4, 2000);
        s["quantity"] = Input.Number(s, "quantity", 1, 10000);
        var size = Choice(s, "size", "bookSizes");
        Enum(s, "paper", "white", "cream", "coated");
        Enum(s, "color", "bw", "color");
        var binding = Enum(s, "binding", "soft", "hard", "spiral");
        if (s["details"] == null)
        {
            if (!new[] { "raghi", "vaziri", "a4" }.Contains(size))
                throw new RuleViolation("برای قطع انتخاب‌شده مشخصات کامل را وارد کنید.");
            return (JsonObject)s.DeepClone();
        }
        var d = Input.Object(s["details"]);
        var custom = Flag("bookSizes", size, "isCustom", size == "custom");
        foreach (var key in new[] { "width", "height" })
        {
            var number = Input.Numeric(d[key]);
            var min = custom ? Numeric("bookSizes", size, key == "width" ? "minWidth" : "minHeight", 8) : 1;
            var max = custom ? Numeric("bookSizes", size, key == "width" ? "maxWidth" : "maxHeight", 45) : 100;
            if (!double.IsFinite(number) || number < min || number > max)
                throw new RuleViolation("ابعاد کتاب خارج از بازه مجاز قطع انتخاب‌شده است.");
        }
        var mode = Option("textPrints", Choice(d, "printType", "textPrints"));
        Enum(d, "sides", "double", "single");
        var bw = Input.Number(d, "bwPages", 0, 2000);
        var color = Input.Number(d, "colorPages", 0, 2000);
        d["bwPages"] = bw;
        d["colorPages"] = color;
        Choice(d, "bwPaper", "bookPapers");
        Choice(d, "colorPaper", "bookPapers");
        if (d["printOnly"] == null)
            throw new RuleViolation("گزینه فقط چاپ معتبر نیست.");
        var only = Input.Flag(d, "printOnly");
        var bind = Option("bookBindings", Choice(d, "bindingStyle", "bookBindings"));
        var cover = Choice(d, "coverPrint", "coverPrints");
        Choice(d, "coverPaper", "coverPapers");
        var coating = Choice(d, "coating", "coverCoatings");
        Enum(d, "orientation", "portrait", "landscape");
        Enum(d, "readingDirection", "rtl", "ltr");
        if (bw + color != N(s, "pages"))
            throw new RuleViolation("جمع صفحات رنگی و سیاه و سفید معتبر نیست.");
        if ((!mode["bw"]!.GetValue<bool>() && bw > 0) || (!mode["color"]!.GetValue<bool>() && color > 0))
            throw new RuleViolation("تعداد صفحات با نوع چاپ سازگار نیست.");
        if (bind["base"]!.GetValue<string>() != binding)
            throw new RuleViolation("نوع صحافی با مشخصات سفارش سازگار نیست.");
        var dimensions = Option("bookSizes", size);
        if (!custom && (Math.Abs(N(d, "width") - N(dimensions, "width")) > 0.01 || Math.Abs(N(d, "height") - N(dimensions, "height")) > 0.01))
            throw new RuleViolation("ابعاد با قطع انتخاب‌شده سازگار نیست.");
        if (d["extras"] is not JsonArray extras || extras.Count > 15)
            throw new RuleViolation("خدمات تکمیلی معتبر نیست.");
        var names = new HashSet<string>();
        foreach (var extra in extras)
        {
            if (extra is not JsonValue v || !v.TryGetValue<string>(out var key) || !names.Add(key))
                throw new RuleViolation("خدمات تکمیلی معتبر یا یکتا نیست.");
            var info = Option("finishingServices", key);
            if (only || Flag("coverPrints", cover, "none", cover == "none"))
                throw new RuleViolation("برای خدمات تکمیلی، جلد را انتخاب کنید.");
            if (info["matte"]!.GetValue<bool>() && Flag("coverPrints", cover, "plastic", cover == "plastic"))
                throw new RuleViolation("این خدمت روی جلد طلق قابل محاسبه نیست؛ جلد کاغذی با سلفون مات انتخاب کنید.");
            if (info["matte"]!.GetValue<bool>() && !Flag("coverCoatings", coating, "matte", coating == "matte"))
                throw new RuleViolation("این خدمت به سلفون مات نیاز دارد.");
            if (info["hard"]!.GetValue<bool>() && !bind["hard"]!.GetValue<bool>())
                throw new RuleViolation("خدمت انتخاب‌شده مخصوص جلد سخت است.");
        }
        return (JsonObject)s.DeepClone();
    }
    public JsonObject Quote(JsonObject spec, JsonNode rates, QuantityDiscount? policy = null)
    {
        decimal R(string key)
        {
            if (rates[key] == null || !decimal.TryParse(rates[key]!.ToJsonString(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount is < 0 or > 10_000_000)
                throw new RuleViolation("تعرفه این نوع چاپ برای قطع انتخاب‌شده هنوز تنظیم نشده است.");
            return amount;
        }
        static decimal Measure(JsonObject row, string name) => Convert.ToDecimal(N(row,name));
        var size = spec["size"]!.GetValue<string>();
        var quantity = Measure(spec, "quantity");
        decimal print, binding, cover = 0, extras = 0, spine = 0;
        var finishing = new List<object>();
        decimal factor = 1;
        if (spec["details"] is not JsonObject d)
        {
            var paper = spec["paper"]!.GetValue<string>();
            print = Math.Ceiling(Measure(spec, "pages") * (R(spec["color"]!.GetValue<string>()) + (paper == "cream" ? R("cream") : paper == "coated" ? R("coated") : 0)) * factor);
            binding = R(spec["binding"]!.GetValue<string>());
        }
        else
        {
            if (Flag("bookSizes", size, "isCustom", size == "custom"))
                factor = Math.Max(.5m, Measure(d, "width") * Measure(d, "height") / (14.5m * 21.5m));
            var mode = Option("textPrints", d["printType"]!.GetValue<string>());
            decimal PaperExtra(string id)
            {
                var p = Option("bookPapers", id);
                return p["type"]!.GetValue<string>() switch
                {
                    "cream" => R("cream"),
                    "coated" => R("coated") * Measure(p, "grams") / 115,
                    "label" => R("coated") * 2,
                    "colored" => R("cream") * .7m,
                    _ => Math.Max(0, (Measure(p, "grams") / 70 - 1) * R("bw"))
                };
            }
            var side = d["sides"]!.GetValue<string>() == "single" ? 2 : 1;
            decimal Page(string id, decimal ink) => Math.Max(0, ink - R("bw") * .28m) + (R("bw") * .28m + PaperExtra(id)) * side;
            var bwPaper = d["bwPaper"]!.GetValue<string>();
            var colorPaper = d["colorPaper"]!.GetValue<string>();
            var bwInk = Flag("textPrints", d["printType"]!.GetValue<string>(), "noPrint", d["printType"]!.GetValue<string>() == "no-print") ? 0 : R("bw") * Measure(mode, "bwFactor");
            print = Math.Ceiling((Measure(d, "bwPages") * Page(bwPaper, bwInk) + Measure(d, "colorPages") * Page(colorPaper, R("color") * Measure(mode, "colorFactor"))) * factor);
            var style = d["bindingStyle"]!.GetValue<string>();
            var bind = Option("bookBindings", style);
            var only = d["printOnly"]!.GetValue<bool>();
            binding = only ? 0 : Math.Ceiling((R(bind["base"]!.GetValue<string>()) * Measure(bind, "factor") + (Flag("bookBindings", style, "extraSpiral", style == "hard-spiral") ? R("spiral") : 0)) * (d["orientation"]!.GetValue<string>() == "landscape" ? 1.05m : 1));
            var cp = d["coverPrint"]!.GetValue<string>();
            var cpFactor = (decimal)Numeric("coverPrints", cp, "factor", cp switch
            {
                "front" => 1,
                "both" => 2,
                "flap" => 1.35,
                "both-flap" => 2.4,
                _ => 0
            });
            var paperFactor = (decimal)Numeric("coverPapers", d["coverPaper"]!.GetValue<string>(), "factor", d["coverPaper"]!.GetValue<string>() switch
            {
                "coated300" => 1.2,
                "coated250" => 1,
                "coated200" => .8,
                "kraft270" => 1.15,
                _ => 1.3
            });
            cover = only || Flag("coverPrints", cp, "none", cp == "none") ? 0 : Math.Ceiling((R("coverPrint") * cpFactor + R("coverPaper") * paperFactor + (Flag("coverCoatings", d["coating"]!.GetValue<string>(), "laminated", d["coating"]!.GetValue<string>() != "none") && !Flag("coverPrints", cp, "plastic", cp == "plastic") ? R("lamination") : 0)) * factor);
            foreach (var item in d["extras"]!.AsArray())
            {
                var key = item!.GetValue<string>();
                var amount = Math.Ceiling(R(Option("finishingServices", key)["rateKey"]?.GetValue<string>() ?? key));
                extras += amount;
                finishing.Add(new
                {
                    key,
                    label = Option("finishingServices", key)["label"]!.GetValue<string>(),
                    unit = amount
                });
            }
            var leaves = (Measure(d, "bwPages") * Measure(Option("bookPapers", bwPaper), "caliper") + Measure(d, "colorPages") * Measure(Option("bookPapers", colorPaper), "caliper")) / (side == 2 ? 1 : 2);
            spine = Math.Floor(leaves * 10 + .5m) / 10;
        }
        policy ??= QuantityDiscount.Standard;
        var copies = checked((long)quantity);
        var subtotal = checked((long)(print + binding + cover + extras) * copies);
        var setup = checked((long)R("setup"));
        var discountPercent = policy.PercentFor(copies);
        var gross = checked(subtotal + setup);
        if (gross > 9_007_199_254_740_991L) throw new RuleViolation("مبلغ برآورد خارج از محدودهٔ قابل نمایش است.");
        var discountAmount = policy.Savings(subtotal, copies);
        var total = gross - discountAmount;
        return JsonSerializer.SerializeToNode(new
        {
            print,
            binding,
            cover,
            extras,
            finishing,
            setup,
            subtotal,
            discountPercent,
            unitBeforeDiscount = (gross + copies - 1) / copies,
            discountAmount,
            total,
            unit = (total + copies - 1) / copies,
            spine
        })!.AsObject();
    }
}
