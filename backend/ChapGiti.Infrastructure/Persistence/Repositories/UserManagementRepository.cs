using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class UserManagementRepository(SqlStore db, TimeProvider clock) : IUserManagementRepository
{
    private sealed class UserRow
    {
        public string Id { get; init; } = null!;
        public string Phone { get; init; } = null!;
        public string FirstName { get; init; } = null!;
        public string LastName { get; init; } = null!;
        public bool PhoneVerified
        {
            get; init;
        }
        public DateTime CreatedAt
        {
            get; init;
        }
        public int IsAdmin
        {
            get; init;
        }
        public long? LastLoginAt
        {
            get; init;
        }
        public long LoginCount
        {
            get; init;
        }
        public long ActiveSessions
        {
            get; init;
        }
        public long OrderCount
        {
            get; init;
        }
    }
    private IQueryable<UserRow> Users()
    {
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        return from a in db.Context.CustomerAccounts.AsNoTracking()
               join r in db.Context.CustomerRegistrations.AsNoTracking() on a.RegistrationId equals r.Id
               select new UserRow
               {
                   Id = r.Id,
                   Phone = r.Phone,
                   FirstName = r.FirstName,
                   LastName = r.LastName,
                   PhoneVerified = r.PhoneVerified,
                   CreatedAt = a.CreatedAt,
                   IsAdmin = db.Context.CustomerRoles.Any(c => c.CustomerId == r.Id && c.Role == "admin") ? 1 : 0,
                   LastLoginAt = db.Context.AuthSessions.Where(s => s.CustomerId == r.Id).Max(s => (long?)s.CreatedAt),
                   LoginCount = db.Context.AuthSessions.LongCount(s => s.CustomerId == r.Id),
                   ActiveSessions = db.Context.AuthSessions.LongCount(s => s.CustomerId == r.Id && s.RevokedAt == null && s.Expires > now),
                   OrderCount = db.Context.Orders.LongCount(o => o.CustomerId == r.Id)
               };
    }
    public async Task<(List<Dictionary<string, object?>> Items, long Total)> ListUsers(string query, string role, string verified, int page, int pageSize, bool oldestFirst = false)
    {
        var users = Users().Where(r => (query == "" || (r.FirstName + " " + r.LastName).Contains(query) || r.Phone.Contains(query)) && (role == "" || (role == "admin" && r.IsAdmin == 1) || (role == "customer" && r.IsAdmin == 0)) && (verified == "" || r.PhoneVerified == (verified == "true")));
        return (await (oldestFirst ? users.OrderBy(r=>r.CreatedAt).ThenBy(r=>r.Id) : users.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)).Skip((page - 1) * pageSize).Take(pageSize).Rows(db.Context), await users.LongCountAsync());
    }
    public Task<Dictionary<string, object?>?> FindUser(string id) => Users().Where(r => r.Id == id).FirstRow(db.Context);
    public Task<bool> IsAdministrator(string id) => db.Context.CustomerRoles.AnyAsync(r => r.CustomerId == id && r.Role == "admin");
    public async Task<long> CountAdministrators() => await db.Context.CustomerRoles.LongCountAsync(r => r.Role == "admin") + await db.Context.AdminUsers.LongCountAsync();
    public async Task SetAdministrator(string id, bool enabled, string actor, string createdAt)
    {
        if (enabled)
            await db.Insert(new Models.CustomerRole { CustomerId = id, Role = "admin", AssignedBy = actor, AssignedAt = EfRows.Date(createdAt) });
        else
            await db.Context.CustomerRoles.Where(r => r.CustomerId == id && r.Role == "admin").ExecuteDeleteAsync();
    }
}
