using ChapGiti.Application.Authentication;

namespace ChapGiti.AuthenticationTests;

internal sealed partial class FakeStore
{
    public Dictionary<string,AccessSession> Access = [];
    public Dictionary<string,long> SessionCreatedAt = [];
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    public Dictionary<string,RefreshCredential> Refresh = [];
    public Dictionary<string,PendingRegistration> Pending = [];
    public Dictionary<string,string> LegacyAdmins = [];
    public Dictionary<string,string> AdminEmails = [];
    public Dictionary<string,string> AdminUsernames = new(StringComparer.Ordinal) { ["admin"]=AuthFixture.AdminEmail };
    public bool AdminEmailSupported = true;
    public HashSet<string> Administrators = [];
    public List<string> Audits = [];
    public Task LockSecurity() => LockPhone("");
    public Task<Dictionary<string,object?>?> ResolveAccess(string hash,long now)
    {
        var s=Access.Values.FirstOrDefault(x=>x.AccessHash==hash&&x.RevokedAt==null&&x.AccessExpires>now&&x.Expires>now);
        if(s==null)return Task.FromResult<Dictionary<string,object?>?>(null);
        if(s.LegacyAdminEmail!=null)
            return Task.FromResult<Dictionary<string,object?>?>(LegacyAdmins.ContainsKey(s.LegacyAdminEmail)?new(){
                ["session_id"]=s.Id,["legacy_admin_email"]=s.LegacyAdminEmail,["admin_email"]=AdminEmails.GetValueOrDefault(s.LegacyAdminEmail,s.LegacyAdminEmail),["is_admin"]=true}:null);
        var p=Profiles[s.CustomerId!];
        return Task.FromResult<Dictionary<string,object?>?>(new(){
            ["session_id"]=s.Id,["id"]=p.Id,["phone"]=p.Phone,["first_name"]=p.First,["last_name"]=p.Last,
            ["phone_verified"]=p.Verified,["account_id"]=p.Id,["is_admin"]=Administrators.Contains(p.Id)
        });
    }
    public Task<RefreshCredential?> FindRefresh(string hash)=>Task.FromResult(Refresh.GetValueOrDefault(hash));
    public Task<AccessSession?> FindSession(string id)=>Task.FromResult(Access.GetValueOrDefault(id));
    public Task InsertSession(AccessSession s,RefreshCredential r,long now){Access.Add(s.Id,s);SessionCreatedAt.Add(s.Id,now);Refresh.Add(r.Hash,r);return Task.CompletedTask;}
    public Task Rotate(AccessSession s,RefreshCredential previous,RefreshCredential next,long now)
    {
        Access[s.Id]=s;Refresh[previous.Hash]=previous with {ConsumedAt=now};Refresh.Add(next.Hash,next);return Task.CompletedTask;
    }
    public Task RevokeSession(string id,long now){if(Access.TryGetValue(id,out var s))Access[id]=s with {RevokedAt=s.RevokedAt??now};return Task.CompletedTask;}
    public async Task RevokeCustomer(string id,long now){foreach(var s in Access.Values.Where(x=>x.CustomerId==id).ToArray())await RevokeSession(s.Id,now);}
    public async Task RevokeLegacyAdmin(string email,long now){foreach(var s in Access.Values.Where(x=>x.LegacyAdminEmail==email).ToArray())await RevokeSession(s.Id,now);}
    public Task SavePending(PendingRegistration r){Pending.Add(r.ChallengeId,r);return Task.CompletedTask;}
    public Task<PendingRegistration?> FindPending(string id)=>Task.FromResult(Pending.GetValueOrDefault(id));
    public Task RemovePending(string id){Pending.Remove(id);return Task.CompletedTask;}
    public Task<(List<Dictionary<string,object?>> Items,long Total)> ListUsers(string query,string role,string verified,int page,int size, bool oldestFirst = false)
    {
        var found=Profiles.Values.Where(p=>Accounts.ContainsKey(p.Phone))
            .Where(p=>(p.First+" "+p.Last+p.Phone).Contains(query)&&
                (role==""||Administrators.Contains(p.Id)==(role=="admin"))&&
                (verified==""||p.Verified==(verified=="true"))).ToArray();
        return Task.FromResult(((oldestFirst ? found.AsEnumerable().Reverse() : found).Skip((page-1)*size).Take(size).Select(PublicRow).ToList(),(long)found.Length));
    }
    private Dictionary<string,object?> PublicRow(Profile p)=>new(){["id"]=p.Id,["phone"]=p.Phone,
        ["first_name"]=p.First,["last_name"]=p.Last,["phone_verified"]=p.Verified,["is_admin"]=Administrators.Contains(p.Id),["created_at"]="2026-10-24T00:00:00.000Z",
        ["last_login_at"]=Access.Values.Where(s=>s.CustomerId==p.Id&&SessionCreatedAt.ContainsKey(s.Id)).Select(s=>(long?)SessionCreatedAt[s.Id]).Max(),
        ["login_count"]=Access.Values.Count(s=>s.CustomerId==p.Id),
        ["active_sessions"]=Access.Values.Count(s=>s.CustomerId==p.Id&&s.RevokedAt==null&&s.Expires>Clock.GetUtcNow().ToUnixTimeMilliseconds()),
        ["order_count"]=0L};
    public Task<Dictionary<string,object?>?> FindUser(string id)=>Task.FromResult(Profiles.TryGetValue(id,out var p)?PublicRow(p):null);
    public Task<bool> IsAdministrator(string id)=>Task.FromResult(Administrators.Contains(id));
    public Task<long> CountAdministrators()=>Task.FromResult((long)Administrators.Count+LegacyAdmins.Count);
    public Task SetAdministrator(string id,bool enabled,string actor,string createdAt)
    {
        if(enabled)Administrators.Add(id);else Administrators.Remove(id);return Task.CompletedTask;
    }
}
