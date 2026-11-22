using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class SessionRepository(SqlStore db) : ISessionRepository
{
    public Task<Dictionary<string,object?>?> FindAdmin(string tokenHash,long now)=>
        (from s in db.Context.AdminSessions.AsNoTracking() join u in db.Context.AdminUsers on s.Email equals u.Email
         join n in db.Context.AdminUsernames on u.Email equals n.Email into names from n in names.DefaultIfEmpty()
         where s.TokenHash==tokenHash&&s.Expires>now select new{u.Email,username=n==null?null:n.Username}).FirstRow(db.Context);
    public Task<Dictionary<string,object?>?> FindCustomer(string tokenHash,long now)=>
        (from s in db.Context.CustomerSessions.AsNoTracking() join r in db.Context.CustomerRegistrations on s.RegistrationId equals r.Id
         join a in db.Context.CustomerAccounts on r.Id equals a.RegistrationId into accounts from a in accounts.DefaultIfEmpty()
         where s.TokenHash==tokenHash&&s.Expires>now select new{r.Id,r.Phone,r.FirstName,r.LastName,r.PhoneVerified,r.CreatedAt,r.UpdatedAt,account_id=a==null?null:a.RegistrationId}).FirstRow(db.Context);
    public async Task RemoveExpired(bool administrator,long now)
    {
        if(administrator)await db.Context.AdminSessions.Where(s=>s.Expires<=now).ExecuteDeleteAsync();
        else await db.Context.CustomerSessions.Where(s=>s.Expires<=now).ExecuteDeleteAsync();
    }
    public async Task Revoke(bool administrator,string tokenHash)
    {
        if(administrator)await db.Context.AdminSessions.Where(s=>s.TokenHash==tokenHash).ExecuteDeleteAsync();
        else await db.Context.CustomerSessions.Where(s=>s.TokenHash==tokenHash).ExecuteDeleteAsync();
    }
    public async Task Create(bool administrator,string tokenHash,string identity,long expires)
    {
        if(administrator)await db.Insert(new Models.AdminSession{TokenHash=tokenHash,Email=identity,Expires=expires});
        else await db.Insert(new Models.CustomerSession{TokenHash=tokenHash,RegistrationId=identity,Expires=expires});
    }
    public async Task<long> IncrementRateLimit(string key,long now,long expires)
    {
        // Single atomic statement across concurrent independent requests; never replay a business UoW.
        var row=await db.One("""
            MERGE dbo.Rate_Limits WITH(HOLDLOCK) AS t USING(SELECT @p0 AS [key]) s ON t.[key]=s.[key]
            WHEN MATCHED THEN UPDATE SET [count]=CASE WHEN expires<@p1 THEN 1 ELSE [count]+1 END,
                expires=CASE WHEN expires<@p1 THEN @p2 ELSE expires END
            WHEN NOT MATCHED THEN INSERT([key],[count],expires) VALUES(@p0,1,@p2)
            OUTPUT inserted.[count];
            """,key,now,expires);return row!.Num("count");
    }
    public async Task Audit(string id,string actor,string action,string createdAt)=>
        await db.Insert(new Models.AuditLog{Id=id,Actor=actor,Action=action,CreatedAt=EfRows.Date(createdAt)});
}
