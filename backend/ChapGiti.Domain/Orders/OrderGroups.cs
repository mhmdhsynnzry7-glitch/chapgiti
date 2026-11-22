using ChapGiti.Domain.Common;

namespace ChapGiti.Domain.Orders;

public static class OrderGroups
{
    public static string StatusFilter(string group) => group switch {
        "" => "", "review" => "new,review", "decision" => "awaiting_customer_approval",
        "queued" => "confirmed", "production" => "printing,binding", "ready" => "ready",
        "completed" => "completed", "cancelled" => "cancelled",
        _ => throw new RuleViolation("گروه درخواست معتبر نیست.")
    };
}
