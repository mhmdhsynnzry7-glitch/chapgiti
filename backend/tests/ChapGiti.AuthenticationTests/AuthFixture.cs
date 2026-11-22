using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Authentication;
using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using ChapGiti.Infrastructure.Messaging;
using ChapGiti.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace ChapGiti.AuthenticationTests;

internal sealed class AuthFixture
{
    public const string AdminEmail = "legacy@example.test";
    public const string Phone = "09000000826";
    public const string Password = "Old.Test.Password@1";
    public const string NewPassword = "New.Test.Password@2";
    public FakeStore Store { get; } = new();
    public FakeRequest Request { get; } = new();
    public FakeSender Sender { get; } = new();
    public ManualClock Clock { get; } = new();
    public PasswordHasher Passwords { get; } = new();
    public CustomerOtpService Otp { get; }
    public AccountService Accounts { get; }
    public SessionService Sessions { get; }
    public AdminUserService Users { get; }
    private readonly IOtpCodeProtector protector;

    public AuthFixture(bool verificationEnabled = true, bool providerConfigured = true)
    {
        Store.Clock = Clock;
        var session = Sessions = new SessionService(Store, Store, Store, Request, Clock);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Otp:Enabled"]=providerConfigured ? "true" : "false",
            ["Otp:ApiKey"]=providerConfigured ? "local-test-only-key-not-a-real-credential" : "", ["Otp:CodeLength"]="4"
        }).Build();
        protector = new OtpCodeProtector(new MelipayamakOtpOptions(config));
        Otp = OtpInMode(verificationEnabled);
        Accounts = new AccountService(Store, Store, session, Passwords);
        Users = new AdminUserService(Store, Store, Store, session, Passwords);
    }

    public CustomerOtpService OtpInMode(bool enabled) => new(Store, Store, Store, Sessions, Sender,
        protector, Passwords, Clock, new(), new(enabled));

    public string Existing(bool verified = false)
    {
        var id = Guid.NewGuid().ToString();
        Store.Profiles[id] = new(id,Phone,"Test","Customer",verified);
        Store.Accounts[Phone] = (id,Passwords.Hash(Password));
        return id;
    }

    public async Task<JsonObject> Send(string purpose = "register", string? phone = null) =>
        Data(await Otp.Request(new JsonObject { ["phone"]=phone??Phone, ["purpose"]=purpose }));

    public Task<UseCaseResult> Complete(JsonObject grant) => Otp.CompleteRegistration(RegistrationBody(grant));

    public static JsonObject RegistrationBody(JsonObject grant) => new() {
        ["registrationToken"] = grant["registrationToken"]!.ToString(), ["firstName"] = "Test", ["lastName"] = "Customer",
        ["password"] = Password, ["confirmation"] = Password
    };

    public Task<UseCaseResult> Verify(JsonObject request, string code = "0123", bool reset = false)
    {
        var body = new JsonObject { ["challengeId"]=request["challengeId"]!.GetValue<string>(), ["code"]=code };
        return reset ? Otp.VerifyReset(body) : Otp.VerifyRegistration(body);
    }

    public static JsonObject Data(UseCaseResult result) =>
        JsonSerializer.SerializeToNode(result.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();

    public static JsonObject ResetBody(JsonObject grant, string password = NewPassword, string? confirmation = null) =>
        new() { ["resetToken"]=grant["resetToken"]!.GetValue<string>(), ["password"]=password, ["confirmation"]=confirmation??password };

    public static string Digest(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset current = DateTimeOffset.UtcNow;
    private TimeSpan offset;
    public bool TrackWallClock { get; set; }
    public override DateTimeOffset GetUtcNow() => TrackWallClock ? DateTimeOffset.UtcNow + offset : current;
    public void Advance(int seconds) { if (TrackWallClock) offset += TimeSpan.FromSeconds(seconds); else current = current.AddSeconds(seconds); }
}

internal sealed class FakeSender : IOtpSender
{
    public bool Available { get; set; } = true;
    public int CodeLength { get; set; } = 4;
    public string Code { get; set; } = "0123";
    public bool Fail { get; set; }
    public int Sends { get; private set; }
    public Task<string> Send(string phone)
    {
        Sends++;
        if (Fail) throw new UseCaseException(ErrorKind.Unavailable,"Provider unavailable.");
        return Task.FromResult(Code);
    }
}

internal sealed class FakeRequest : IRequestSession
{
    public string ClientAddress => "test-client";
    public string? CustomerToken { get; private set; }
    public string? AdminToken { get; private set; }
    public string? RefreshToken { get; set; }
    public string? AdminRefreshToken { get; set; }
    public bool SecureTransport { get; set; } = true;
    public string? GetToken(bool administrator) => administrator ? AdminToken : CustomerToken;
    public void SetToken(bool administrator, string token, TimeSpan lifetime)
    {
        if (administrator) AdminToken = lifetime == TimeSpan.Zero ? null : token;
        else CustomerToken = lifetime == TimeSpan.Zero ? null : token;
    }
    public string? GetRefreshToken(bool administrator = false) => administrator ? AdminRefreshToken : RefreshToken;
    public void SetRefreshToken(string token, TimeSpan lifetime, bool administrator = false)
    {
        if (administrator) AdminRefreshToken = lifetime == TimeSpan.Zero ? null : token;
        else RefreshToken = lifetime == TimeSpan.Zero ? null : token;
    }
    public void ClearLegacyCookies() { }
    public void RequireSecureTransport()
    {
        if (!SecureTransport) throw new UseCaseException(ErrorKind.Unavailable, "HTTPS required.");
    }
}

internal sealed record Profile(string Id,string Phone,string First,string Last,bool Verified);

internal sealed partial class FakeStore : IOtpRepository, IAccountRepository, ISessionRepository, IUnitOfWork,
    IAccessSessionRepository, IUserManagementRepository
{
    public Dictionary<string,Profile> Profiles = [];
    public Dictionary<string,(string Id,string Hash)> Accounts = [];
    public bool FailNextAccountCreation;
    public Dictionary<string,OtpChallenge> Challenges = [];
    public Dictionary<string,PasswordResetGrant> Grants = [];
    public Dictionary<string,(string Id,long Expires)> Sessions = [];
    private readonly Dictionary<string,(int Count,long Expires)> limits = [];
    private readonly SemaphoreSlim gate = new(1);
    private readonly AsyncLocal<bool> active = new();
    public Func<Action>? SnapshotExternalState;

    public async Task<T> Transaction<T>(Func<Task<T>> action)
    {
        if (active.Value) return await action();
        await gate.WaitAsync();
        active.Value = true;
        var p = Profiles.ToDictionary();
        var a = Accounts.ToDictionary();
        var c = Challenges.ToDictionary();
        var g = Grants.ToDictionary();
        var s = Sessions.ToDictionary();
        var access = Access.ToDictionary();
        var sessionCreated = SessionCreatedAt.ToDictionary();
        var refresh = Refresh.ToDictionary();
        var roles = Administrators.ToHashSet();
        var pending = Pending.ToDictionary();
        var audit = Audits.ToList();
        var admins = LegacyAdmins.ToDictionary();
        var emails = AdminEmails.ToDictionary();
        var usernames = AdminUsernames.ToDictionary();
        var restoreExternal = SnapshotExternalState?.Invoke();
        try { return await action(); }
        catch { restoreExternal?.Invoke(); Profiles=p; Accounts=a; Challenges=c; Grants=g; Sessions=s;
            Access=access; SessionCreatedAt=sessionCreated; Refresh=refresh; Administrators=roles; Pending=pending; Audits=audit; LegacyAdmins=admins; AdminEmails=emails; AdminUsernames=usernames; throw; }
        finally { active.Value=false; gate.Release(); }
    }

    public Task LockPhone(string phone)
    {
        if (!active.Value) throw new InvalidOperationException("Expected transaction.");
        return Task.CompletedTask;
    }
    public Task<OtpChallenge?> Latest(string phone) =>
        Task.FromResult(Challenges.Values.Where(x=>x.Phone==phone&&x.DeliveryStatus!="grant").OrderByDescending(x=>x.CreatedAt).FirstOrDefault());
    public Task<int> CountRequests(string phone,long since) =>
        Task.FromResult(Challenges.Values.Count(x=>x.Phone==phone&&x.DeliveryStatus!="grant"&&x.CreatedAt>=since));
    public Task Insert(OtpChallenge c) { Challenges.Add(c.Id,c); return Task.CompletedTask; }
    public Task<bool> Activate(string id,string hash,long now)
    {
        var c=Challenges[id];
        if(c.Consumed||c.Expires<=now||c.DeliveryStatus!="pending") return Task.FromResult(false);
        Challenges[id]=c with {CodeHash=hash,DeliveryStatus="sent"};return Task.FromResult(true);
    }
    public Task Fail(string id,long now) { Challenges[id]=Challenges[id] with {Consumed=true,CodeHash=null,DeliveryStatus="failed"};return Task.CompletedTask; }
    public Task<OtpChallenge?> Find(string id)=>Task.FromResult(Challenges.GetValueOrDefault(id));
    public Task<OtpChallenge?> LockChallenge(string id)=>Find(id);
    public Task RecordFailure(string id,bool exhaust,long now)
    {
        var c=Challenges[id];Challenges[id]=c with {Attempts=c.Attempts+1,Consumed=exhaust,CodeHash=exhaust?null:c.CodeHash};return Task.CompletedTask;
    }
    public Task Consume(string id,long now){Challenges[id]=Challenges[id] with{Consumed=true,CodeHash=null};return Task.CompletedTask;}
    public Func<string,string,long,long,Task>? GrantStorageCheck;
    public async Task AddRegistrationGrant(string hash,string phone,long expires,long now)
    {
        // Every registration API/browser fixture now validates the real INSERT against
        // the existing SQL CHECK definitions before accepting logical in-memory state.
        if(GrantStorageCheck!=null)await GrantStorageCheck(hash,phone,expires,now);
        else await new ChapGiti.Infrastructure.Persistence.Repositories.OtpRepository(new SchemaCheckedOtpSql())
            .AddRegistrationGrant(hash,phone,expires,now);
        Challenges.Add(hash,new(hash,phone,"register",null,null,now,expires,0,false,"grant"));
    }
    public Task<RegistrationGrant?> FindRegistrationGrant(string hash,bool locked=false)
    {
        var row=Challenges.GetValueOrDefault(hash);
        return Task.FromResult(row is {Purpose:"register",DeliveryStatus:"grant"}
            ? new RegistrationGrant(hash,row.Phone,row.Expires,row.Consumed) : null);
    }
    public async Task<bool> ConsumeRegistrationGrant(string hash,long now)
    {
        var grant=await FindRegistrationGrant(hash);
        if(grant==null||grant.Consumed||grant.Expires<=now)return false;
        await Consume(hash,now);return true;
    }
    public Task Invalidate(string phone,long now)
    {
        foreach(var c in Challenges.Values.Where(x=>x.Phone==phone&&!x.Consumed).ToArray())Challenges[c.Id]=c with{Consumed=true,CodeHash=null};
        return Task.CompletedTask;
    }
    public Task AddReset(string hash,string customerId,long expires,long now)
    {
        Grants.Add(hash,new(hash,customerId,Profiles[customerId].Phone,expires,false));return Task.CompletedTask;
    }
    public Task<PasswordResetGrant?> FindReset(string hash,bool locked=false)=>Task.FromResult(Grants.GetValueOrDefault(hash));
    public Task RevokeResets(string customerId,long now)
    {
        foreach(var g in Grants.Values.Where(x=>x.CustomerId==customerId).ToArray())Grants[g.TokenHash]=g with{Consumed=true};
        return Task.CompletedTask;
    }
    public Task<Dictionary<string,object?>?> FindAccountOwner(string phone)=>
        Task.FromResult(Accounts.TryGetValue(phone,out var a)?new Dictionary<string,object?>{{"registration_id",a.Id}}:null);
    public Task<Dictionary<string,object?>?> LockAccountOwner(string phone)=>FindAccountOwner(phone);
    public Task<int> CreateRegistration(string id,string phone,string createdAt){Profiles[id]=new(id,phone,"","",false);return Task.FromResult(1);}
    public Task<int> VerifyPhone(string id,string updatedAt){Profiles[id]=Profiles[id] with{Verified=true};return Task.FromResult(1);}
    public Task<int> CreateAccount(string phone,string id,string hash,string createdAt)
    {
        if(FailNextAccountCreation){FailNextAccountCreation=false;throw new InvalidOperationException("Isolated account-write failure.");}
        Accounts.Add(phone,(id,hash));return Task.FromResult(1);
    }
    public Task<int> UpdateProfile(string first,string last,string updatedAt,string id){Profiles[id]=Profiles[id] with{First=first,Last=last};return Task.FromResult(1);}
    public Task<int> UpdateCustomerPassword(string id,string hash)
    {
        var phone=Accounts.FirstOrDefault(x=>x.Value.Id==id).Key;
        if(phone==null)return Task.FromResult(0);
        Accounts[phone]=(id,hash);return Task.FromResult(1);
    }
    public Task<int> RevokeCustomerSessions(string id)
    {
        var keys=Sessions.Where(x=>x.Value.Id==id).Select(x=>x.Key).ToArray();
        foreach(var key in keys)Sessions.Remove(key);
        return Task.FromResult(keys.Length);
    }
    public Task<Dictionary<string,object?>?> FindCustomerAccount(string phone)=>
        Task.FromResult(Accounts.TryGetValue(phone,out var a)&&Profiles.TryGetValue(a.Id,out var p)
            ? new Dictionary<string,object?>{{"registration_id",a.Id},{"password_hash",a.Hash},{"phone",p.Phone},{"first_name",p.First},{"last_name",p.Last},{"phone_verified",p.Verified}} : null);
    public Task<Dictionary<string,object?>?> LockCustomerAccount(string phone)=>FindCustomerAccount(phone);
    public Task<Dictionary<string,object?>?> FindCustomer(string hash,long now)
    {
        if(!Sessions.TryGetValue(hash,out var session)||session.Expires<=now)return Task.FromResult<Dictionary<string,object?>?>(null);
        var p=Profiles[session.Id];
        return Task.FromResult<Dictionary<string,object?>?>(new() {
            {"id",p.Id},{"phone",p.Phone},{"first_name",p.First},{"last_name",p.Last},{"phone_verified",p.Verified},
            {"account_id",Accounts.TryGetValue(p.Phone,out var a)&&a.Id==p.Id?p.Id:null}
        });
    }
    public Task Create(bool administrator,string hash,string id,long expires){Sessions.Add(hash,(id,expires));return Task.CompletedTask;}
    public Task Revoke(bool administrator,string hash){Sessions.Remove(hash);return Task.CompletedTask;}
    public Task RemoveExpired(bool administrator,long now)=>Task.CompletedTask;
    public Task<long> IncrementRateLimit(string key,long now,long expires)
    {
        var old=limits.GetValueOrDefault(key);
        var count=old.Expires>now?old.Count+1:1;limits[key]=(count,old.Expires>now?old.Expires:expires);
        return Task.FromResult((long)count);
    }
    public Task Audit(string id,string actor,string action,string createdAt) { Audits.Add(actor+":"+action); return Task.CompletedTask; }
    public Task<List<Dictionary<string,object?>>> RecentOrders(string id)=>Task.FromResult(new List<Dictionary<string,object?>>());
    public Task<Dictionary<string,object?>?> FindAdmin(string identity) => FindAdminById(LegacyAdmins.Keys.FirstOrDefault(id => AdminEmails.GetValueOrDefault(id,id) == identity) ?? "");
    public Task<Dictionary<string,object?>?> FindAdminByUsername(string username) => FindAdminById(AdminUsernames.GetValueOrDefault(username, ""));
    public Task<Dictionary<string,object?>?> FindAdminById(string id) => Task.FromResult(LegacyAdmins.TryGetValue(id,out var hash) ? new Dictionary<string,object?> { ["id"]=id,["email"]=AdminEmails.GetValueOrDefault(id,id),["password_hash"]=hash } : null);
    public Task<List<Dictionary<string,object?>>> ListAdministrators() => Task.FromResult(LegacyAdmins.Keys.Select(id => new Dictionary<string,object?> { ["email"]=AdminEmails.GetValueOrDefault(id,id),["created_at"]="2026-10-27T00:00:00Z" }).ToList());
    public Task<int> CreateAdministrator(string email,string hash,string createdAt) { LegacyAdmins.Add(email,hash); AdminEmails[email]=email; return Task.FromResult(1); }
    public Task<int> UpdateAdministratorEmail(string id,string email)
    {
        if(!AdminEmailSupported)throw new UseCaseException(ErrorKind.Unavailable,"Email editing is unavailable in this isolated fixture.");
        if(!LegacyAdmins.ContainsKey(id))return Task.FromResult(0);
        AdminEmails[id]=email; return Task.FromResult(1);
    }
    public Task<int> UpdateAdministratorPassword(string id,string hash) { if(!LegacyAdmins.ContainsKey(id))return Task.FromResult(0); LegacyAdmins[id]=hash; return Task.FromResult(1); }
    public Task<Dictionary<string,object?>?> FindAdmin(string tokenHash,long now)=>Task.FromResult<Dictionary<string,object?>?>(null);
    public Task<Dictionary<string,object?>?> LockUsernameOwner(string username)=>Task.FromResult(AdminUsernames.TryGetValue(username,out var id) ? new Dictionary<string,object?>{["email"]=id} : null);
    public Task<int> SaveAdmin(string email,string hash,string createdAt) { LegacyAdmins[email]=hash; return Task.FromResult(1); }
    public Task<int> SaveUsername(string email,string username) { AdminUsernames[username]=email; return Task.FromResult(1); }
    public Task<int> RevokeAdminSessions(string email)=>Task.FromResult(0);
}
