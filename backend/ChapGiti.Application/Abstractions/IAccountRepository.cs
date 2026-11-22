namespace ChapGiti.Application.Abstractions;

public interface IAccountRepository
{
    Task<Dictionary<string, object?>?> FindAdmin(string identity);
    Task<Dictionary<string, object?>?> FindAdminByUsername(string username);
    Task<Dictionary<string, object?>?> FindAdminById(string id);
    Task<List<Dictionary<string, object?>>> ListAdministrators();
    Task<int> CreateAdministrator(string email, string passwordHash, string createdAt);
    Task<int> UpdateAdministratorEmail(string id, string email);
    Task<int> UpdateAdministratorPassword(string id, string passwordHash);
    Task<Dictionary<string, object?>?> FindAccountOwner(string phone);
    Task<int> CreateRegistration(string id, string phone, string createdAt);
    Task<Dictionary<string, object?>?> FindCustomerAccount(string phone);
    Task<Dictionary<string, object?>?> LockCustomerAccount(string phone);
    Task<int> VerifyPhone(string customerId, string updatedAt);
    Task<int> UpdateCustomerPassword(string customerId, string passwordHash);
    Task<int> RevokeCustomerSessions(string customerId);
    Task<Dictionary<string, object?>?> LockAccountOwner(string phone);
    Task<int> CreateAccount(string phone, string customerId, string passwordHash, string createdAt);
    Task<int> UpdateProfile(string firstName, string lastName, string updatedAt, string customerId);
    Task<List<Dictionary<string, object?>>> RecentOrders(string customerId);
    Task<Dictionary<string, object?>?> LockUsernameOwner(string username);
    Task<int> SaveAdmin(string email, string passwordHash, string createdAt);
    Task<int> SaveUsername(string email, string username);
    Task<int> RevokeAdminSessions(string email);
}
