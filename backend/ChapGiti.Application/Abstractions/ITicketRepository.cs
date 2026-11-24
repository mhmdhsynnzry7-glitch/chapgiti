namespace ChapGiti.Application.Abstractions;

public sealed record TicketFile(string Id, string MessageId, string Name, string Mime, int Size, int Position, string Digest);

public interface ITicketRepository
{
    Task<Dictionary<string, object?>?> LockExistingTicket(string id);
    Task<int> InsertTicket(string id, string customerId, string subject, string createdAt, string? orderId = null);
    Task<bool> SupportsOrderLinks();
    Task<bool> SupportsMessageReads();
    Task<List<Dictionary<string, object?>>> SearchTickets(string? customerId, string query, string status, int skip, int take, bool oldestFirst = false);
    Task<long> CountTickets(string? customerId, string query, string status);
    Task<List<Dictionary<string, object?>>> OrderConversations(string orderId);
    Task<string?> ActiveOrderTicket(string customer, string order, bool locked = true);
    Task<long> UnreadSupport(string? customerId, string incomingActor);
    Task<int> MarkMessagesRead(string ticket, string actor, string throughId);
    Task<bool> SupportsAttachments();
    Task<List<TicketFile>> TicketFiles(string ticketId, string? messageId = null, string? fileId = null);
    Task AddFile(TicketFile file);
    Task<int> InsertInitialMessage(string id, string message, string createdAt);
    Task<List<Dictionary<string, object?>>> ListAll();
    Task<List<Dictionary<string, object?>>> ListOwned(string customerId);
    Task<List<Dictionary<string, object?>>> Messages(string ticketId);
    Task<int> UpdateStatus(string status, string updatedAt, string ticketId);
    Task<Dictionary<string, object?>?> LockExistingMessage(string id);
    Task<int> InsertMessage(string id, string ticketId, string actor, string message, string createdAt);
    Task<int> UpdateAfterReply(string status, string updatedAt, string ticketId);
    Task<Dictionary<string, object?>?> FindTicket(string id, bool administrator, string? customerId, bool locked);
}
