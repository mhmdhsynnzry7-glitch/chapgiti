using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(SqlStore db) : IAccountRepository
{
    private IQueryable<Models.AdminUser> Admins => db.Context.AdminUsers.AsNoTracking();
    public Task<Dictionary<string,object?>?> FindAdminByUsername(string username) =>
        (from n in db.Context.AdminUsernames.AsNoTracking() join u in Admins on n.Email equals u.Email where n.Username==username select new{Id=u.Email,Email=u.EffectiveEmail,u.PasswordHash}).FirstRow(db.Context);
    public Task<Dictionary<string,object?>?> FindAdmin(string identity) => Admins.Where(u=>u.EffectiveEmail==identity).Select(u=>new{Id=u.Email,Email=u.EffectiveEmail,u.PasswordHash}).FirstRow(db.Context);
    public Task<Dictionary<string,object?>?> FindAdminById(string id) => Admins.Where(u=>u.Email==id).Select(u=>new{Id=u.Email,Email=u.EffectiveEmail,u.PasswordHash}).FirstRow(db.Context);
    public Task<List<Dictionary<string,object?>>> ListAdministrators() => Admins.OrderBy(u=>u.CreatedAt).ThenBy(u=>u.Email).Select(u=>new{Email=u.EffectiveEmail,u.CreatedAt}).Rows(db.Context);
    public async Task<int> CreateAdministrator(string email,string passwordHash,string createdAt){await db.Insert(new Models.AdminUser{Email=email,LoginEmail=email,PasswordHash=passwordHash,CreatedAt=EfRows.Date(createdAt)});return 1;}
    public Task<int> UpdateAdministratorEmail(string id,string email) => db.Context.AdminUsers.Where(u=>u.Email==id).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.LoginEmail,email));
    public Task<int> UpdateAdministratorPassword(string id,string passwordHash) => db.Context.AdminUsers.Where(u=>u.Email==id).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.PasswordHash,passwordHash));
    public Task<Dictionary<string,object?>?> FindAccountOwner(string phone) => db.Context.CustomerAccounts.AsNoTracking().Where(a=>a.Phone==phone).Select(a=>new{a.RegistrationId}).FirstRow(db.Context);
    public async Task<int> CreateRegistration(string id,string phone,string createdAt){await db.Insert(new Models.CustomerRegistration{Id=id,Phone=phone,FirstName="",LastName="",CreatedAt=EfRows.Date(createdAt),UpdatedAt=EfRows.Date(createdAt)});return 1;}
    public Task<Dictionary<string,object?>?> FindCustomerAccount(string phone) => (from a in db.Context.CustomerAccounts.AsNoTracking() join r in db.Context.CustomerRegistrations.AsNoTracking() on a.RegistrationId equals r.Id where a.Phone==phone select new{a.RegistrationId,a.PasswordHash,r.Phone,r.FirstName,r.LastName,r.PhoneVerified}).FirstRow(db.Context);
    // Range locks bind OTP grants and account creation to a single serializable transaction.
    public Task<Dictionary<string,object?>?> LockCustomerAccount(string phone) => db.One("SELECT registration_id,password_hash FROM dbo.Customer_Accounts WITH(UPDLOCK,HOLDLOCK) WHERE phone=@p0",phone);
    public Task<Dictionary<string,object?>?> LockAccountOwner(string phone) => db.One("SELECT registration_id FROM dbo.Customer_Accounts WITH(UPDLOCK,HOLDLOCK) WHERE phone=@p0",phone);
    public Task<int> VerifyPhone(string id,string updatedAt) => db.Context.CustomerRegistrations.Where(r=>r.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.PhoneVerified,true).SetProperty(r=>r.UpdatedAt,EfRows.Date(updatedAt)));
    public Task<int> UpdateCustomerPassword(string id,string hash) => db.Context.CustomerAccounts.Where(a=>a.RegistrationId==id).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.PasswordHash,hash));
    public Task<int> RevokeCustomerSessions(string id) => db.Context.CustomerSessions.Where(s=>s.RegistrationId==id).ExecuteDeleteAsync();
    public async Task<int> CreateAccount(string phone,string customerId,string hash,string createdAt){await db.Insert(new Models.CustomerAccount{Phone=phone,RegistrationId=customerId,PasswordHash=hash,CreatedAt=EfRows.Date(createdAt)});return 1;}
    public Task<int> UpdateProfile(string firstName,string lastName,string updatedAt,string id) => db.Context.CustomerRegistrations.Where(r=>r.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.FirstName,firstName).SetProperty(r=>r.LastName,lastName).SetProperty(r=>r.UpdatedAt,EfRows.Date(updatedAt)));
    public Task<List<Dictionary<string,object?>>> RecentOrders(string id) => db.Context.Orders.AsNoTracking().Where(o=>o.CustomerId==id).OrderByDescending(o=>o.CreatedAt).Take(20).Select(o=>new{o.Code,o.Title,o.Status,o.Estimate,o.CreatedAt}).Rows(db.Context);
    public Task<Dictionary<string,object?>?> LockUsernameOwner(string username) => db.One("SELECT email FROM dbo.Admin_Usernames WITH(UPDLOCK,HOLDLOCK) WHERE username=@p0",username);
    // Maintenance upserts intentionally use HOLDLOCK; they are not ordinary account CRUD.
    public Task<int> SaveAdmin(string email,string hash,string createdAt) => db.Execute("MERGE dbo.Admin_Users WITH(HOLDLOCK) t USING(SELECT @p0 email) s ON t.email=s.email WHEN MATCHED THEN UPDATE SET password_hash=@p1 WHEN NOT MATCHED THEN INSERT(email,password_hash,created_at) VALUES(@p0,@p1,@p2);",email,hash,createdAt);
    public Task<int> SaveUsername(string email,string username) => db.Execute("MERGE dbo.Admin_Usernames WITH(HOLDLOCK) t USING(SELECT @p0 email) s ON t.email=s.email WHEN MATCHED THEN UPDATE SET username=@p1 WHEN NOT MATCHED THEN INSERT(username,email) VALUES(@p1,@p0);",email,username);
    public Task<int> RevokeAdminSessions(string email) => db.Context.AdminSessions.Where(s=>s.Email==email).ExecuteDeleteAsync();
}
