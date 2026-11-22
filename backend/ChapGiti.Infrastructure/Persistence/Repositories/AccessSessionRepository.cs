using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class AccessSessionRepository(SqlStore db) : IAccessSessionRepository
{
    public async Task LockSecurity() => await db.Execute("DECLARE @result int; EXEC @result=sp_getapplock @Resource='ChapGiti.Authentication',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @result<0 THROW 51001,'Authentication request is busy.',1;");
    public Task<Dictionary<string,object?>?> ResolveAccess(string hash,long now) =>
        (from s in db.Context.AuthSessions.AsNoTracking()
         join a0 in db.Context.CustomerAccounts.AsNoTracking() on s.CustomerId equals a0.RegistrationId into accounts from a in accounts.DefaultIfEmpty()
         join r0 in db.Context.CustomerRegistrations.AsNoTracking() on a.RegistrationId equals r0.Id into profiles from r in profiles.DefaultIfEmpty()
         join u0 in db.Context.AdminUsers.AsNoTracking() on s.LegacyAdminEmail equals u0.Email into admins from u in admins.DefaultIfEmpty()
         where s.AccessHash==hash && s.RevokedAt==null && s.AccessExpires>now && s.Expires>now && (a!=null||u!=null)
         select new{SessionId=s.Id,Id=r==null?null:r.Id,Phone=r==null?null:r.Phone,FirstName=r==null?null:r.FirstName,LastName=r==null?null:r.LastName,PhoneVerified=r!=null&&r.PhoneVerified,AccountId=a==null?null:a.RegistrationId,s.LegacyAdminEmail,IsAdmin=u!=null?1:0,AdminEmail=u==null?null:u.EffectiveEmail}).FirstRow(db.Context);
    public async Task<ChapGiti.Application.Authentication.RefreshCredential?> FindRefresh(string hash){var r=await db.Context.AuthRefreshCredentials.AsNoTracking().SingleOrDefaultAsync(r=>r.TokenHash==hash);return r==null?null:new(r.TokenHash,r.SessionId,r.Expires,r.ConsumedAt);}
    public async Task<ChapGiti.Application.Authentication.AccessSession?> FindSession(string id){var s=await db.Context.AuthSessions.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==id);return s==null?null:new(s.Id,s.CustomerId,s.LegacyAdminEmail,s.AccessHash,s.AccessExpires,s.Expires,s.RevokedAt);}
    public async Task InsertSession(ChapGiti.Application.Authentication.AccessSession s,ChapGiti.Application.Authentication.RefreshCredential refresh,long now){await db.Insert(new Models.AuthSession{Id=s.Id,CustomerId=s.CustomerId,LegacyAdminEmail=s.LegacyAdminEmail,AccessHash=s.AccessHash,AccessExpires=s.AccessExpires,CreatedAt=now,Expires=s.Expires});await InsertRefresh(refresh,now);}
    public async Task Rotate(ChapGiti.Application.Authentication.AccessSession session,ChapGiti.Application.Authentication.RefreshCredential previous,ChapGiti.Application.Authentication.RefreshCredential next,long now){await db.Context.AuthRefreshCredentials.Where(r=>r.TokenHash==previous.Hash&&r.ConsumedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ConsumedAt,now));await db.Context.AuthSessions.Where(s=>s.Id==session.Id&&s.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(s=>s.AccessHash,session.AccessHash).SetProperty(s=>s.AccessExpires,session.AccessExpires));await InsertRefresh(next,now);}
    private async Task InsertRefresh(ChapGiti.Application.Authentication.RefreshCredential r,long now) => await db.Insert(new Models.AuthRefreshCredential{TokenHash=r.Hash,SessionId=r.SessionId,CreatedAt=now,Expires=r.Expires});
    public async Task RevokeSession(string id,long now) => await db.Context.AuthSessions.Where(s=>s.Id==id&&s.RevokedAt==null).ExecuteUpdateAsync(u=>u.SetProperty(s=>s.RevokedAt,now));
    public async Task RevokeCustomer(string id,long now) => await db.Context.AuthSessions.Where(s=>s.CustomerId==id&&s.RevokedAt==null).ExecuteUpdateAsync(u=>u.SetProperty(s=>s.RevokedAt,now));
    public async Task RevokeLegacyAdmin(string email,long now) => await db.Context.AuthSessions.Where(s=>s.LegacyAdminEmail==email&&s.RevokedAt==null).ExecuteUpdateAsync(u=>u.SetProperty(s=>s.RevokedAt,now));
}
