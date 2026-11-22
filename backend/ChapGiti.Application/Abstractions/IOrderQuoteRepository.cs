namespace ChapGiti.Application.Abstractions;

public interface IOrderQuoteRepository
{
    Task<bool> SupportsQuoteWorkflow();
    Task<List<Dictionary<string, object?>>> Quotes(string orderId);
    Task<Dictionary<string, object?>?> LockCommand(string id);
    Task InsertCommand(string id, string orderId, string actor, string kind, string hash, string now);
    Task InsertQuote(string id, string orderId, int revision, long amount, string scope, string delivery, string exclusions, string admin, string now);
    Task<int> DecideQuote(string id, string state, string reason, string? customer, string now, long version);
    Task SetInformation(string orderId, string? message, bool requiresFile);
    Task AdoptProposalWorkflow(string orderId);
}
