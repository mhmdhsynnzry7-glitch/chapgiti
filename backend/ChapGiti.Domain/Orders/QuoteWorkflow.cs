using ChapGiti.Domain.Common;

namespace ChapGiti.Domain.Orders;

public static class QuoteWorkflow
{
    public static void ValidateIssue(string orderStatus, long amount, string scope)
    {
        if (orderStatus is not ("review" or "awaiting_customer_approval" or "confirmed"))
            throw new RuleViolation("پیشنهاد فقط پس از بررسی درخواست قابل ارسال است.");
        if (amount is < 0 or > 100_000_000_000 || string.IsNullOrWhiteSpace(scope))
            throw new RuleViolation("مبلغ و شرح خدمات پیشنهاد را وارد کنید.");
    }

    public static string DecisionStatus(string action, string reason) => action switch
    {
        "accept" => "confirmed",
        "request-revision" when !string.IsNullOrWhiteSpace(reason) => "review",
        "decline" when !string.IsNullOrWhiteSpace(reason) => "cancelled",
        _ => throw new RuleViolation("برای اصلاح یا انصراف، دلیل کوتاه بنویسید.")
    };
}
