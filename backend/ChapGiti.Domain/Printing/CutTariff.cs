using System.Text.Json.Nodes;
using ChapGiti.Domain.Common;
namespace ChapGiti.Domain.Printing;

public static class CutTariff
{
    public static IReadOnlyList<string> Keys { get; } = ["bw","color","cream","coated","soft","hard","spiral","coverPrint","coverPaper","lamination","setup"];

    // Proposal only. Base ink follows the catalog print factor, materials follow sheet area,
    // and half of binding is fixed handling, with half proportional to spine length.
    public static JsonObject Propose(JsonObject dimensions, JsonObject basis)
    {
        if (dimensions["isCustom"]?.GetValue<bool>() != false)
            throw new RuleViolation("قطع سفارشی به ابعاد و تعرفهٔ صریح مدیر نیاز دارد.");
        decimal Measure(string key) => Convert.ToDecimal(Input.Numeric(dimensions[key]));
        var width = Measure("width");
        var height = Measure("height");
        var factor = Measure("factor");
        var selected = new JsonObject(Keys.Select(key => KeyValuePair.Create(key, basis[key]?.DeepClone())));
        var rates = Validate(selected);
        foreach (var key in Keys)
        {
            decimal amount = rates[key]!.GetValue<long>();
            var proposed = key switch
            {
                "setup" => amount,
                "bw" or "color" or "coverPrint" => amount * factor,
                "soft" or "hard" or "spiral" => amount * (21.5m + height) / 43m,
                _ => amount * width * height / (14.5m * 21.5m)
            };
            rates[key] = decimal.ToInt64(decimal.Round(proposed, 0, MidpointRounding.AwayFromZero));
        }
        return Validate(rates);
    }

    public static JsonObject Validate(JsonObject rates)
    {
        var result = new JsonObject();
        if (rates.Count != Keys.Count) throw new RuleViolation("همهٔ ردیف‌های تعرفهٔ چاپ این قطع را تکمیل کنید.");
        foreach (var key in Keys)
        {
            if (rates[key] is not JsonValue value || !decimal.TryParse(value.ToJsonString(), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount != decimal.Truncate(amount) || amount is < 0 or > 10_000_000)
                throw new RuleViolation("مبلغ تعرفه باید عدد صحیح بین صفر و ده میلیون تومان باشد.");
            result[key] = (long)amount;
        }
        return result;
    }
}
