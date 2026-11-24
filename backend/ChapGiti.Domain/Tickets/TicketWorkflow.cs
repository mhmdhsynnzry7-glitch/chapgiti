using ChapGiti.Domain.Common;
namespace ChapGiti.Domain.Tickets;

public static class TicketWorkflow
{
    public static void ValidateStatus(string status)
    {
        if (status is not ("open" or "awaiting_customer" or "closed"))
            throw new RuleViolation("وضعیت تیکت معتبر نیست.");
    }
    public static string AfterReply(bool administrator, string current = "open") => administrator && current == "awaiting_customer" ? current : "open";
}
