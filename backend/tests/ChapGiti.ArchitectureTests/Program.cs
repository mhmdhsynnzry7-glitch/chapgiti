using System.Reflection;
using ChapGiti.Application.Features;
using ChapGiti.Domain.Common;
using ChapGiti.Domain.Orders;
using ChapGiti.Domain.Printing;
using ChapGiti.Domain.Tickets;
using ChapGiti.Infrastructure;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException(description);
    checks++;
    Console.WriteLine("PASS " + description);
}

void OnlyAllowedLayers(Assembly assembly, params string[] allowed)
{
    var actual = assembly.GetReferencedAssemblies()
        .Select(reference => reference.Name!)
        .Where(name => name.StartsWith("ChapGiti."));
    Check(actual.All(allowed.Contains), assembly.GetName().Name + " references only permitted layers");
}

var domain = typeof(PrintPricing).Assembly;
var application = typeof(OrderService).Assembly;
var infrastructure = typeof(DependencyInjection).Assembly;
OnlyAllowedLayers(domain);
OnlyAllowedLayers(application, "ChapGiti.Domain");
OnlyAllowedLayers(infrastructure, "ChapGiti.Domain", "ChapGiti.Application");
foreach (var assembly in new[] { domain, application })
{
    var external = assembly.GetReferencedAssemblies().Select(reference => reference.Name!);
    Check(!external.Any(name => name.StartsWith("Microsoft.AspNetCore") ||
        name.StartsWith("Microsoft.Data.SqlClient") || name.StartsWith("Microsoft.EntityFrameworkCore")),
        assembly.GetName().Name + " has no HTTP or database provider dependency");
}

OrderWorkflow.ValidateChange("new", "review", null, true);
Check(true, "permitted order transition succeeds");
try
{
    OrderWorkflow.ValidateChange("review", "printing", null, true);
    throw new InvalidOperationException("Confirmation without a final price was accepted.");
}
catch (RuleViolation)
{
    Check(true, "confirmation requires an authoritative final price");
}
try
{
    OrderWorkflow.ValidateChange("completed", "new", 1000, true);
    throw new InvalidOperationException("An invalid order transition was accepted.");
}
catch (RuleViolation)
{
    Check(true, "invalid order transition is rejected");
}
Check(TicketWorkflow.AfterReply(true) == "open" && TicketWorkflow.AfterReply(true, "awaiting_customer") == "awaiting_customer" &&
    TicketWorkflow.AfterReply(false) == "open", "ticket ownership roles determine the next reply status");
Check(!OrderWorkflow.Allowed("review",true).Contains("printing"),"new review cannot bypass customer approval even with an administrator price");
OrderWorkflow.ValidateChange("review","printing",123,true,proposalWorkflow:false);
Check(true,"explicit legacy compatibility retains historical review approvals");
Check(OrderWorkflow.AdministratorActions("awaiting_customer_approval",true,true).Length==0,"administrator transitions never expose customer quote decisions");
Check(OrderWorkflow.Allowed("confirmed",true).SequenceEqual(new[]{"printing"}),"new confirmed requests have only explicit production start");
Check(OrderWorkflow.Allowed("printing",false).SequenceEqual(new[]{"ready"})&&OrderWorkflow.Allowed("printing",true).SequenceEqual(new[]{"binding"}),"persisted only-print specification determines binding");
Check(QuoteWorkflow.DecisionStatus("request-revision","reason")=="review"&&QuoteWorkflow.DecisionStatus("decline","reason")=="cancelled","quote decisions map to the approved request states");
Console.WriteLine(checks + " architecture and domain checks passed.");
