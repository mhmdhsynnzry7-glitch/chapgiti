using ChapGiti.Application.Common;

namespace ChapGiti.AuthenticationTests;

internal static class AuthorizationScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        var f=new AuthFixture();var actor=f.Existing(true);
        await f.Accounts.LoginCustomer(new(){["phone"]=AuthFixture.Phone,["password"]=AuthFixture.Password});
        await AuthScenarios.Reject(()=>f.Accounts.GetAdmin(),ErrorKind.Unauthenticated);
        f.Store.Administrators.Add(actor);
        await AuthScenarios.Reject(()=>f.Users.List("","","",1,20),ErrorKind.Unauthenticated);
        check(true,"customer sessions and historical customer admin roles never authorize administrator use cases");
        f.Store.LegacyAdmins[AuthFixture.AdminEmail]=f.Passwords.Hash(AuthFixture.Password);
        await f.Accounts.LoginAdmin(new(){["username"]="admin",["password"]=AuthFixture.Password});
        var before=f.Request.CustomerToken;
        var admin=AuthFixture.Data(await f.Accounts.GetAdmin());
        check(admin["email"]!.ToString()==AuthFixture.AdminEmail&&before!=null&&f.Request.AdminToken!=null,"fixed-username login issues an independent administrator session without replacing customer credentials");
        await AuthScenarios.Reject(()=>f.Users.SetAdministrator(actor,true),ErrorKind.Invalid);
        await AuthScenarios.Reject(()=>f.Users.SetAdministrator(actor,false),ErrorKind.Invalid);
        check(f.Store.Administrators.Contains(actor),"retired customer-role grants fail closed without rewriting historical assignments");
        var create=new System.Text.Json.Nodes.JsonObject {["phone"]="09111111111",["firstName"]="New",["lastName"]="Customer",["password"]=AuthFixture.Password,["confirmation"]=AuthFixture.Password};
        var created=AuthFixture.Data(await f.Users.Create(create));var id=created["id"]!.ToString();
        check(!f.Store.Profiles[id].Verified&&f.Store.Accounts["09111111111"].Hash!=AuthFixture.Password,"administrator-created customers have secure hashes and unverified mobiles");
        await AuthScenarios.Reject(()=>f.Users.Create(create),ErrorKind.Conflict);
        var data=AuthFixture.Data(await f.Users.List("New","customer","false",1,1));
        check(data["total"]!.GetValue<long>()==1&&data["items"]!.AsArray().Count==1&&!data.ToJsonString().Contains("password_hash"),"user search and pagination expose only public read models");
        var profile=f.Store.Profiles[id];
        await f.Accounts.SaveProfile(new(){["firstName"]="Changed",["lastName"]="Owner",["phone"]=AuthFixture.Phone});
        await AuthScenarios.Reject(()=>f.Accounts.SaveProfile(new(){["firstName"]="Changed",["lastName"]="Owner",["id"]=id}),ErrorKind.Invalid);
        await AuthScenarios.Reject(()=>f.Accounts.SaveProfile(new(){["firstName"]="Changed",["lastName"]="Owner",["roles"]=new System.Text.Json.Nodes.JsonArray("admin")}),ErrorKind.Invalid);
        await AuthScenarios.Reject(()=>f.Accounts.SaveProfile(new(){["firstName"]="Changed",["lastName"]="Owner",["phone"]="09111111111"}),ErrorKind.Invalid);
        check(f.Store.Profiles[id]==profile&&f.Store.Profiles[actor].First=="Changed","profile ownership, role injection and mobile restrictions survive independent authentication");
        await f.Accounts.LogoutCustomer();
        await f.Accounts.GetAdmin();
        check(f.Request.CustomerToken==null&&f.Request.AdminToken!=null,"customer logout preserves administrator authentication");
        await f.Accounts.LogoutAdmin();
        await AuthScenarios.Reject(()=>f.Accounts.GetAdmin(),ErrorKind.Unauthenticated);
        check(true,"administrator logout invalidates the administrator context");
    }
}
