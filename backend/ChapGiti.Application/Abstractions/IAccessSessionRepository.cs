using ChapGiti.Application.Authentication;

namespace ChapGiti.Application.Abstractions;

public interface IAccessSessionRepository
{
    // The shared, transaction-owned security lock establishes a single lock order for
    // login, refresh, password reset and role changes. No network I/O runs under it.
    Task LockSecurity();
    Task<Dictionary<string, object?>?> ResolveAccess(string accessHash, long now);
    Task<RefreshCredential?> FindRefresh(string hash);
    Task<AccessSession?> FindSession(string id);
    Task InsertSession(AccessSession session, RefreshCredential refresh, long now);
    Task Rotate(AccessSession session, RefreshCredential previous, RefreshCredential next, long now);
    Task RevokeSession(string id, long now);
    Task RevokeCustomer(string id, long now);
    Task RevokeLegacyAdmin(string email, long now);
}
