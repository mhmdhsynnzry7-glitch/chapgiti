using ChapGiti.Domain.Common;
using System.Collections.Frozen;
using System.Text.Json.Nodes;
namespace ChapGiti.Domain.Orders;

public static class OrderWorkflow
{
    public static readonly FrozenDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["new"] = "در انتظار بررسی",
        ["review"] = "در حال بررسی چاپخانه",
        ["awaiting_customer_approval"] = "منتظر تأیید شما",
        ["confirmed"] = "تأییدشده؛ در صف چاپ",
        ["printing"] = "در حال چاپ",
        ["binding"] = "صحافی",
        ["ready"] = "آماده تحویل",
        ["completed"] = "تحویل شده",
        ["cancelled"] = "رد / لغو شده"
    }.ToFrozenDictionary();

    public static bool RequiresBinding(JsonObject specification) =>
        specification["details"] is not JsonObject details || details["printOnly"] is not JsonValue value ||
        !value.TryGetValue<bool>(out var printOnly) || !printOnly;

    public static string[] Allowed(string current, bool bindingRequired, bool proposalWorkflow = true) => current switch {
        "new" => ["review"],
        "review" => [proposalWorkflow ? "awaiting_customer_approval" : "printing", "cancelled"],
        "awaiting_customer_approval" => ["confirmed", "review", "cancelled"],
        "confirmed" => proposalWorkflow ? ["printing"] : ["printing", "cancelled"],
        "printing" => [bindingRequired ? "binding" : "ready"],
        "binding" => ["ready"],
        "ready" => ["completed"],
        _ => []
    };

    public static string[] AdministratorActions(string current,bool bindingRequired,bool proposalWorkflow) =>
        proposalWorkflow && current=="awaiting_customer_approval" ? [] :
        Allowed(current,bindingRequired,proposalWorkflow).Where(status=>status!="awaiting_customer_approval").ToArray();

    public static void ValidateChange(string current, string next, long? finalPrice, bool bindingRequired, string reason = "", bool proposalWorkflow = true)
    {
        if (!Labels.ContainsKey(next))
            throw new RuleViolation("وضعیت نامعتبر است.");
        if (next != current && !Allowed(current, bindingRequired, proposalWorkflow).Contains(next))
            throw new RuleViolation("این تغییر وضعیت مجاز نیست.");
        if (next == "printing" && current is "review" or "confirmed" && finalPrice is null)
            throw new RuleViolation("برای تأیید سفارش قیمت تأییدشده را وارد کنید.");
        if (next == "cancelled" && next != current && string.IsNullOrWhiteSpace(reason))
            throw new RuleViolation("دلیل رد سفارش را وارد کنید.");
    }
}
