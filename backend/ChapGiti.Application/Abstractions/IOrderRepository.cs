namespace ChapGiti.Application.Abstractions;

public interface IOrderRepository
{
    Task<bool> TrackingCodeInUse(string code);
    Task<Dictionary<string, object?>?> LockExistingOrder(string id);
    Task<int> InsertOrder(string id, string code, string customer, string phone, string email, string title, string service, string specification, long estimate, string createdAt, string customerId);
    Task<int> InsertInitialEvent(string id, string orderId, string createdAt);
    Task<Dictionary<string, object?>?> TrackOrder(string code, string phone, string customerId);
    Task<List<Dictionary<string, object?>>> TrackingEvents(string orderId);
    Task<long> CountCustomerOrders(string customerId, string statuses);
    Task<List<Dictionary<string, object?>>> CustomerOrders(string customerId, int skip, int take, string statuses);
    Task<Dictionary<string, object?>?> CustomerOrder(string id, string customerId);
    Task<List<Dictionary<string, object?>>> CustomerOrderFiles(string id, string customerId);
    Task<Dictionary<string,object?>?> FindOwnedFile(string fileId,string customerId);
    Task<Dictionary<string, object?>?> FindOwnedOrder(string id, string code, string customerId);
    Task<Dictionary<string, object?>?> LockOwnedOrder(string id, string code, string customerId);
    Task<Dictionary<string, object?>?> CountFiles(string orderId);
    Task<int> InsertFile(string id, string orderId, string key, string name, int size, string createdAt);
}
