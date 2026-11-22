namespace ChapGiti.Application.Abstractions;

public interface IAdminOrderRepository
{
    Task<List<Dictionary<string, object?>>> StatusCounts();
    Task<Dictionary<string, object?>?> Summary();
    Task<List<Dictionary<string, object?>>> RecentOrders();
    Task<List<Dictionary<string, object?>>> DailyTrend(DateTime since);
    Task<Dictionary<string, object?>?> UnreadMessages();
    Task<List<Dictionary<string, object?>>> SearchOrders(string status, string search, string pattern, int offset, int limit, bool oldestFirst = false);
    Task<Dictionary<string, object?>?> CountOrders(string status, string search, string pattern);
    Task<List<Dictionary<string, object?>>> ExportOrders();
    Task<Dictionary<string, object?>?> FindOrder(string id);
    Task<Dictionary<string, object?>?> LockOrder(string id);
    Task<bool> SupportsOrderNotes();
    Task<List<Dictionary<string, object?>>> InternalNotes(string id);
    Task<Dictionary<string, object?>?> LockInternalNote(string id);
    Task InsertInternalNote(string id, string orderId, string note, string actor, string createdAt);
    Task<List<Dictionary<string, object?>>> OrderFiles(string id);
    Task<List<Dictionary<string, object?>>> OrderEvents(string id);
    Task<List<Dictionary<string, object?>>> OrderSms(string id);
    Task<int> UpdateOrder(string status, long? finalPrice, string notes, string updatedAt, string id, long version);
    Task<int> InsertEvent(string id, string orderId, string status, string note, string actor, string createdAt);
    Task<Dictionary<string, object?>?> FindFile(string id);
    Task<List<Dictionary<string, object?>>> CustomerTotals();
    Task<List<Dictionary<string, object?>>> CustomerProfiles();
    Task<List<Dictionary<string, object?>>> Messages();
    Task<int> MarkMessageRead(string id);
    Task<List<Dictionary<string, object?>>> SmsLogs();
    Task<List<Dictionary<string, object?>>> AuditLogs();
}
