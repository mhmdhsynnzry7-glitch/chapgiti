using ChapGiti.Api.Http;
using ChapGiti.Application.Common;
using ChapGiti.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Org.BouncyCastle.Crypto.Generators;
using System.Text;

namespace ChapGiti.AuthenticationTests;

internal static class TransportScenarios
{
    public static void Run(Action<bool,string> check)
    {
        var context=new DefaultHttpContext();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {["Site:Url"]="https://printing.example"}).Build();
        var environment=new TestEnvironment();
        var transport=new HttpSessionContext(new HttpContextAccessor {HttpContext=context},config,environment);
        transport.RequireSecureTransport();
        transport.SetToken(false,new string('a',64),TimeSpan.FromMinutes(15));
        transport.SetRefreshToken(new string('b',64),TimeSpan.FromDays(30));
        check(context.Response.Headers.SetCookie.Count==2&&context.Response.Headers.SetCookie.All(value=>
            value!.Contains("secure",StringComparison.OrdinalIgnoreCase)&&value.Contains("httponly",StringComparison.OrdinalIgnoreCase)&&value.Contains("samesite=strict",StringComparison.OrdinalIgnoreCase)),
            "actual HTTPS cookie adapter sets Secure, HttpOnly and SameSite Strict on both credentials");
        config["Site:Url"]="http://printing.example";
        try {transport.RequireSecureTransport();throw new InvalidOperationException("Public HTTP accepted.");}
        catch(UseCaseException e){check(e.Kind==ErrorKind.Unavailable,"actual transport refuses public HTTP authentication");}
        config["Site:Url"]="http://127.0.0.1:3000";
        try {transport.RequireSecureTransport();throw new InvalidOperationException("Production HTTP loopback accepted.");}
        catch(UseCaseException){check(true,"HTTP loopback exception is restricted to Development");}
        environment.EnvironmentName="Development";transport.RequireSecureTransport();
        check(true,"loopback Development remains available for local testing");

        var salt="0123456789abcdef0123456789abcdef";
        var hash=SCrypt.Generate(Encoding.UTF8.GetBytes(AuthFixture.Password),Encoding.UTF8.GetBytes(salt),32768,8,1,64);
        var encoded="scrypt$"+salt+"$"+Convert.ToHexStringLower(hash);
        var passwords=new PasswordHasher();
        check(passwords.Verify(AuthFixture.Password,encoded)&&!passwords.Verify("Wrong.Password@1",encoded),"legacy scrypt password compatibility is retained");
    }
}
internal sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName {get;set;}="Production";
    public string ApplicationName {get;set;}="TransportTests";
    public string ContentRootPath {get;set;}=AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
}
