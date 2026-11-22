using System.Text.Json.Nodes;
using ChapGiti.Domain.Common;
namespace ChapGiti.Domain.Printing;

public static class CutTariff
{
    public static IReadOnlyList<string> Keys { get; } = ["bw","color","cream","coated","soft","hard","spiral","coverPrint","coverPaper","lamination","setup"];

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
