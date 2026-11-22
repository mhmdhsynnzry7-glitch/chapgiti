using ChapGiti.Application.Authentication;

namespace ChapGiti.Application.Abstractions;

public interface IOtpRepository
{
    // Call inside the unit of work before locking any challenge/account for this phone.
    Task LockPhone(string phone);
    Task<OtpChallenge?> Latest(string phone);
    Task<int> CountRequests(string phone, long since);
    Task Insert(OtpChallenge challenge);
    Task<bool> Activate(string id, string codeHash, long now);
    Task Fail(string id, long now);
    Task<OtpChallenge?> Find(string id);
    Task<OtpChallenge?> LockChallenge(string id);
    Task RecordFailure(string id, bool exhaust, long now);
    Task Consume(string id, long now);
    Task Invalidate(string phone, long now);
    Task AddRegistrationGrant(string tokenHash, string phone, long expires, long now);
    Task<RegistrationGrant?> FindRegistrationGrant(string tokenHash, bool locked = false);
    Task<bool> ConsumeRegistrationGrant(string tokenHash, long now);
    Task AddReset(string tokenHash, string customerId, long expires, long now);
    Task<PasswordResetGrant?> FindReset(string tokenHash, bool locked = false);
    Task RevokeResets(string customerId, long now);
    Task SavePending(PendingRegistration registration);
    Task<PendingRegistration?> FindPending(string challengeId);
    Task RemovePending(string challengeId);
}
