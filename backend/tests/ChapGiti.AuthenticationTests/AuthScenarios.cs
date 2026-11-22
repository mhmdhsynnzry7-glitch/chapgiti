using System.Text.Json.Nodes;
using ChapGiti.Application.Authentication;
using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using ChapGiti.Domain.Common;

namespace ChapGiti.AuthenticationTests;

internal static class AuthScenarios
{
    private static JsonObject Login(string phone=AuthFixture.Phone,string password=AuthFixture.Password) =>
        new(){["phone"]=phone,["password"]=password};
    internal static async Task Reject(Func<Task<UseCaseResult>> action, ErrorKind? kind=null)
    {
        try { await action(); throw new InvalidOperationException("Expected rejection."); }
        catch(UseCaseException e) { if(kind!=null&&e.Kind!=kind)throw; }
    }
    public static async Task Run(Action<bool,string> check)
    {
        {
            var f=new AuthFixture();
            await f.Accounts.StartCustomer(new(){["phone"]=AuthFixture.Phone});
            check(f.Store.Profiles.Count==0&&f.Request.CustomerToken==null,"start cannot create an unverified identity");
            await Reject(()=>f.Otp.Request(new(){["phone"]=AuthFixture.Phone,["purpose"]="login"}),ErrorKind.Invalid);
            check(f.Sender.Sends==0,"routine OTP/passwordless login is rejected");
            var c=await f.Send(phone:"۰۹۰۰۰۰۰۰۸۲۶");
            var id=c["challengeId"]!.ToString();
            check(f.Store.Accounts.Count==0&&f.Store.Profiles.Count==0&&f.Store.Pending.Count==0,"phone-only OTP issuance stores no customer profile or password");
            check(f.Store.Challenges[id].Phone==AuthFixture.Phone&&f.Store.Challenges[id].CodeHash?.Length==64,"normalized phone and protected OTP are stored");
            // Random challenge IDs and timestamps can coincidentally contain the
            // fake four-digit code. Verify the public schema instead of substrings.
            check(c.Count == 6 && new[] { "ok", "challengeId", "expiresAt", "resendAfterSeconds", "codeLength", "message" }.All(c.ContainsKey) &&
                !c["message"]!.ToString().Contains(f.Sender.Code), "OTP is never returned to the browser");
            await Reject(()=>f.Verify(c,"9999"),ErrorKind.Invalid);
            check(f.Store.Challenges[id].Attempts==1,"failed attempts commit");
            var registrationGrant=AuthFixture.Data(await f.Verify(c,"۰۱۲۳"));
            check(f.Store.Accounts.Count==0&&f.Store.Profiles.Count==0,"OTP verification issues a grant without creating a customer");
            await f.Complete(registrationGrant);
            var profile=f.Store.Profiles.Single().Value;
            check(profile.Verified&&profile.First=="Test"&&f.Store.Accounts.Count==1,"verified registration grant atomically finalizes profile and account");
            check(f.Request.CustomerToken==null&&f.Request.RefreshToken==null,"registration OTP never authenticates without password login");
            await Reject(()=>f.Verify(c),ErrorKind.Invalid);
            check(f.Store.Profiles.Count==1&&f.Store.Pending.Count==0,"OTP replay cannot create a duplicate account");
            await Reject(()=>f.Accounts.LoginCustomer(Login(password:"Wrong.Password@1")),ErrorKind.Unauthenticated);
            await f.Accounts.LoginCustomer(Login());
            check(f.Request.CustomerToken!=null&&f.Request.RefreshToken!=null,"password login creates the unified access/refresh session");
        }
        {
            var f=new AuthFixture();var id=f.Existing(true);var old=f.Store.Accounts[AuthFixture.Phone].Hash;
            await Reject(async()=>{await f.Send();return new();},ErrorKind.Conflict);
            check(f.Store.Profiles.Count==1&&f.Store.Accounts[AuthFixture.Phone].Hash==old&&f.Store.Profiles[id].First=="Test",
                "duplicate registration is rejected without overwriting an existing account or profile");
            check(f.Request.CustomerToken==null&&f.Sender.Sends==0,"duplicate registration cannot send OTP or act as login");
            f=new AuthFixture();var c=await f.Send();id=f.Existing(true);old=f.Store.Accounts[AuthFixture.Phone].Hash;
            await Reject(()=>f.Verify(c),ErrorKind.Conflict);
            check(f.Store.Profiles.Count==1&&f.Store.Accounts[AuthFixture.Phone].Hash==old&&f.Store.Profiles[id].First=="Test",
                "an account created after OTP issuance cannot be overwritten during registration verification");
        }
        {
            var f=new AuthFixture();var c=await f.Send();f.Clock.Advance(120);
            await Reject(()=>f.Verify(c),ErrorKind.Invalid);
            check(f.Store.Accounts.Count==0,"OTP expires at the exact two-minute boundary");
            f=new AuthFixture();c=await f.Send();
            for(var n=0;n<5;n++)await Reject(()=>f.Verify(c,"9999"),ErrorKind.Invalid);
            await Reject(()=>f.Verify(c),ErrorKind.Invalid);
            check(f.Store.Challenges[c["challengeId"]!.ToString()].Attempts==5,"five failures exhaust the OTP");
        }
        {
            var f=new AuthFixture();var old=await f.Send();
            await Reject(async()=>{await f.Send();return new();},ErrorKind.TooManyRequests);
            f.Clock.Advance(61);f.Sender.Code="4567";var c=await f.Send();
            await Reject(()=>f.Verify(old),ErrorKind.Invalid);await f.Verify(c,"4567");
            check(f.Store.Challenges[old["challengeId"]!.ToString()].Consumed,"resend cooldown is shared across purposes and replaces the old code");
            f=new AuthFixture();await f.Send();
            var now=f.Clock.GetUtcNow().ToUnixTimeMilliseconds();
            for(var n=0;n<4;n++){var id=new string((char)('a'+n),64);f.Store.Challenges[id]=new(id,AuthFixture.Phone,"register",null,null,now-70000,now+1000,0,true,"failed");}
            f.Clock.Advance(61);
            await Reject(async()=>{await f.Send();return new();},ErrorKind.TooManyRequests);
            check(f.Sender.Sends==1,"phone request quota survives replacement challenge IDs");
        }
        {
            var f=new AuthFixture();f.Sender.Available=false;
            await Reject(async()=>{await f.Send();return new();},ErrorKind.Unavailable);
            check(f.Store.Challenges.Count==0,"unconfigured provider fails closed");
            f=new AuthFixture();f.Sender.Fail=true;
            await Reject(async()=>{await f.Send();return new();},ErrorKind.Unavailable);
            check(f.Store.Challenges.Values.All(x=>x.Consumed&&x.CodeHash==null),"SMS failure leaves no usable challenge");
        }
        {
            var unknown=new AuthFixture();
            await Reject(async()=>{await unknown.Send("reset");return new();},ErrorKind.NotFound);
            var legacy=new AuthFixture();legacy.Existing(false);
            await Reject(async()=>{await legacy.Send("reset");return new();},ErrorKind.Forbidden);
            var known=new AuthFixture();known.Existing(true);var c=await known.Send("reset");
            check(unknown.Store.Challenges.Count==0&&legacy.Store.Challenges.Count==0,
                "unknown and unverified recovery requests create no challenge");
            check(unknown.Sender.Sends==0&&legacy.Sender.Sends==0&&known.Sender.Sends==1,"only eligible customers invoke the recovery provider");
            check(!legacy.Store.Profiles.Single().Value.Verified&&unknown.Store.Profiles.Count==0,"unknown/unverified recovery cannot create an account or silently verify a legacy phone");
            await Reject(()=>known.Verify(c),ErrorKind.Invalid);
            var grant=AuthFixture.Data(await known.Verify(c,reset:true));
            check(grant.ContainsKey("resetToken")&&known.Request.CustomerToken==null,"purpose-bound recovery grants do not create login sessions");
            await Reject(()=>known.Verify(c,reset:true),ErrorKind.Invalid);
            check(!known.Store.Grants.ContainsKey(grant["resetToken"]!.ToString()),"only a recovery-token digest is persisted");
        }
        {
            var f=new AuthFixture();var id=f.Existing(false);
            await f.Accounts.LoginCustomer(Login());
            check(!f.Store.Profiles[id].Verified,"legacy password login does not silently mark the phone verified");
            await Reject(()=>f.Otp.Request(new(){["phone"]="09111111111",["purpose"]="verify-phone"}),ErrorKind.Forbidden);
            var c=await f.Send("verify-phone");
            await f.Otp.VerifyPhone(new(){["challengeId"]=c["challengeId"]!.ToString(),["code"]="0123"});
            check(f.Store.Profiles[id].Verified,"legacy phone verification requires a password-authenticated owning session plus OTP");
        }
        {
            var f=new AuthFixture();f.Existing(true);await f.Accounts.LoginCustomer(Login());
            var previousRefresh=f.Request.RefreshToken!;
            var grant=AuthFixture.Data(await f.Verify(await f.Send("reset"),reset:true));
            await Reject(()=>f.Otp.ResetPassword(AuthFixture.ResetBody(grant,confirmation:"Wrong.Password@1")),ErrorKind.Invalid);
            await Reject(()=>f.Otp.ResetPassword(AuthFixture.ResetBody(grant,"short")),ErrorKind.Invalid);
            check(f.Store.Grants.Values.All(x=>!x.Consumed),"invalid new passwords do not consume the recovery grant");
            await f.Otp.ResetPassword(AuthFixture.ResetBody(grant));
            check(f.Request.CustomerToken==null&&f.Request.RefreshToken==null&&f.Store.Access.Values.All(x=>x.RevokedAt!=null),"password reset revokes every access/refresh family and logs out");
            f.Request.RefreshToken=previousRefresh;
            await Reject(()=>f.Sessions.Refresh(),ErrorKind.Unauthenticated);
            await Reject(()=>f.Otp.ResetPassword(AuthFixture.ResetBody(grant)),ErrorKind.Invalid);
            await Reject(()=>f.Accounts.LoginCustomer(Login()),ErrorKind.Unauthenticated);
            await f.Accounts.LoginCustomer(Login(password:AuthFixture.NewPassword));
            check(f.Request.CustomerToken!=null,"new password succeeds while the old password, recovery replay and old refresh fail");
            f=new AuthFixture();f.Existing(true);grant=AuthFixture.Data(await f.Verify(await f.Send("reset"),reset:true));f.Clock.Advance(300);
            await Reject(()=>f.Otp.ResetPassword(AuthFixture.ResetBody(grant)),ErrorKind.Invalid);
            check(f.Passwords.Verify(AuthFixture.Password,f.Store.Accounts[AuthFixture.Phone].Hash),"expired recovery grant cannot update credentials");
        }
        {
            var f=new AuthFixture();var c=await f.Send();
            async Task<bool> Verify(){try{await f.Verify(c);return true;}catch(UseCaseException){return false;}}
            var results=await Task.WhenAll(Verify(),Verify());
            check(results.Count(x=>x)==1&&f.Store.Accounts.Count==0&&f.Store.Challenges.Values.Count(x=>x.DeliveryStatus=="grant")==1,"concurrent registration verification issues exactly one grant without creating an account");
            f=new AuthFixture();f.Existing(true);var grant=AuthFixture.Data(await f.Verify(await f.Send("reset"),reset:true));
            async Task<bool> Reset(){try{await f.Otp.ResetPassword(AuthFixture.ResetBody(grant));return true;}catch(UseCaseException){return false;}}
            results=await Task.WhenAll(Reset(),Reset());
            check(results.Count(x=>x)==1,"concurrent recovery consumes exactly once");
        }
        await SessionScenarios.Run(check);
        await AuthorizationScenarios.Run(check);
    }
}
