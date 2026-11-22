using ChapGiti.Application.Common;
using ChapGiti.Application.Features;

namespace ChapGiti.AuthenticationTests;

internal static class SessionScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        var f=new AuthFixture();f.Existing(true);
        await f.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]=AuthFixture.Password});
        var oldAccess=f.Request.CustomerToken!;var oldRefresh=f.Request.RefreshToken!;
        check(!f.Store.Access.Values.Any(s=>s.AccessHash==oldAccess)&&!f.Store.Refresh.ContainsKey(oldRefresh),"access and refresh credentials are stored as digests");
        f.Clock.Advance(900);
        await AuthScenarios.Reject(()=>f.Accounts.GetCustomer(),ErrorKind.Unauthenticated);
        await f.Sessions.Refresh();
        check(f.Request.CustomerToken!=oldAccess&&f.Request.RefreshToken!=oldRefresh,"expired short access can be renewed with rotating refresh credentials");
        var winnerRefresh=f.Request.RefreshToken;
        var loser=new FakeRequest {RefreshToken=oldRefresh};
        var loserSession=new SessionService(f.Store,f.Store,f.Store,loser,f.Clock);
        await AuthScenarios.Reject(()=>loserSession.Refresh(),ErrorKind.Conflict);
        check(f.Store.Access.Values.All(s=>s.RevokedAt==null)&&loser.CustomerToken==null,"concurrent refresh loser receives no tokens and does not revoke the winner");
        f.Clock.Advance(11);
        await AuthScenarios.Reject(()=>loserSession.Refresh(),ErrorKind.Unauthenticated);
        check(f.Store.Access.Values.All(s=>s.RevokedAt!=null),"refresh reuse after the concurrency window revokes the whole family");
        f.Request.RefreshToken=winnerRefresh;
        await AuthScenarios.Reject(()=>f.Sessions.Refresh(),ErrorKind.Unauthenticated);
        check(true,"descendant credentials cannot refresh after family revocation");

        f=new AuthFixture();f.Existing(true);await f.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]=AuthFixture.Password});
        var raw=f.Request.RefreshToken!;
        var r1=new FakeRequest {RefreshToken=raw};var r2=new FakeRequest {RefreshToken=raw};
        var s1=new SessionService(f.Store,f.Store,f.Store,r1,f.Clock);var s2=new SessionService(f.Store,f.Store,f.Store,r2,f.Clock);
        async Task<bool> Try(SessionService s){try{await s.Refresh();return true;}catch(UseCaseException e) when(e.Kind==ErrorKind.Conflict){return false;}}
        var results=await Task.WhenAll(Try(s1),Try(s2));
        check(results.Count(x=>x)==1&&f.Store.Refresh.Count==2,"two simultaneous refreshes rotate exactly once");
        await f.Accounts.LogoutCustomer();
        check(f.Store.Access.Values.All(x=>x.RevokedAt!=null)&&f.Request.CustomerToken==null&&f.Request.RefreshToken==null,"customer logout invalidates only its session credentials");
        r1.RefreshToken=raw;
        await AuthScenarios.Reject(()=>s1.Refresh(),ErrorKind.Unauthenticated);

        f=new AuthFixture();f.Existing(true);await f.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]=AuthFixture.Password});
        f.Clock.Advance(30*86400);
        await AuthScenarios.Reject(()=>f.Sessions.Refresh(),ErrorKind.Unauthenticated);
        check(true,"refresh has an absolute thirty-day expiry");

        f=new AuthFixture();f.Existing(true);f.Request.SecureTransport=false;
        await AuthScenarios.Reject(()=>f.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]=AuthFixture.Password}),ErrorKind.Unavailable);
        check(f.Store.Access.Count==0,"non-HTTPS production authentication fails before issuing credentials");
    }
}
