namespace ChapGiti.Application.Abstractions;

public interface IUserManagementRepository
{
    Task<(List<Dictionary<string, object?>> Items, long Total)> ListUsers(string query, string role, string verified, int page, int pageSize, bool oldestFirst = false);
    Task<Dictionary<string, object?>?> FindUser(string id);
    Task<bool> IsAdministrator(string id);
    Task<long> CountAdministrators();
    Task SetAdministrator(string id, bool enabled, string actor, string createdAt);
}
