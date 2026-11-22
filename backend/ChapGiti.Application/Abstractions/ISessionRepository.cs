namespace ChapGiti.Application.Abstractions;

public interface ISessionRepository
{
    Task<Dictionary<string, object?>?> FindAdmin(string tokenHash, long now);
    Task<Dictionary<string, object?>?> FindCustomer(string tokenHash, long now);
    Task RemoveExpired(bool administrator, long now);
    Task Revoke(bool administrator, string tokenHash);
    Task Create(bool administrator, string tokenHash, string identity, long expires);
    Task<long> IncrementRateLimit(string key, long now, long expires);
    Task Audit(string id, string actor, string action, string createdAt);
}
