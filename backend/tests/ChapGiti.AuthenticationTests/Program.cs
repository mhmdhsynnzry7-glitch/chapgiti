using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ChapGiti.Application.Authentication;
using ChapGiti.Application.Common;
using ChapGiti.AuthenticationTests;
using ChapGiti.Infrastructure.Messaging;
using ChapGiti.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = Encoding.UTF8;
if(args.Contains("--serve-ui"))
{
    await using var host=await TestApiHost.Start(browser:true,otpVerificationEnabled:!args.Contains("--otp-bypass"));
    Console.WriteLine("TEST_API_ORIGIN="+host.Origin);
    await Microsoft.Extensions.Hosting.HostingAbstractionsHostExtensions.WaitForShutdownAsync(host.App);
    return;
}

var checks = 0;
void Check(bool condition,string name)
{
    if(!condition)throw new InvalidOperationException(name);
    checks++;Console.WriteLine("PASS "+name);
}
var failedSuites = 0;
async Task Suite(string name, Func<Task> run)
{
    try { await run(); }
    catch (Exception ex) { failedSuites++; Environment.ExitCode = 1; Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); }
}
if (args is ["--release-contracts"])
{
    await Suite(nameof(OrderCorrectionScenarios), () => OrderCorrectionScenarios.Run(Check));
    await Suite(nameof(RequestQuoteScenarios), () => RequestQuoteScenarios.Run(Check));
    await Suite(nameof(WorkflowSupportScenarios), () => WorkflowSupportScenarios.Run(Check));
    await Suite(nameof(RealtimeScenarios), () => RealtimeScenarios.Run(Check));
    Console.WriteLine($"{checks} in-memory release workflow checks passed; {failedSuites} suites failed.");
    return;
}
await Suite(nameof(OrderCorrectionScenarios), () => OrderCorrectionScenarios.Run(Check));
await Suite(nameof(OrderAttentionScenarios), () => OrderAttentionScenarios.Run(Check));
if(args.Contains("--order-workflow-only")) { Console.WriteLine(checks+" order workflow checks passed; no SQL connection or real SMS was used."); return; }
await Suite(nameof(AuthScenarios), () => AuthScenarios.Run(Check));
await Suite(nameof(RegistrationStorageScenarios), () => RegistrationStorageScenarios.Run(Check));
await Suite(nameof(RegistrationScenarios), () => RegistrationScenarios.Run(Check));
await Suite(nameof(PasswordRecoveryScenarios), () => PasswordRecoveryScenarios.Run(Check));
await Suite(nameof(CustomerLoginErrorScenarios), () => CustomerLoginErrorScenarios.Run(Check));
await Suite(nameof(IndependentAuthenticationScenarios), () => IndependentAuthenticationScenarios.Run(Check));
await Suite(nameof(OtpSwitchScenarios), () => OtpSwitchScenarios.Run(Check));
await Suite(nameof(OtpSwitchConfigurationScenarios), () => OtpSwitchConfigurationScenarios.Run(Check));
await Suite(nameof(OtpSwitchApiScenarios), () => OtpSwitchApiScenarios.Run(Check));
await Suite(nameof(RealOtpScenarios), () => RealOtpScenarios.Run(Check));
await Suite(nameof(OtpProviderScenarios), () => OtpProviderScenarios.Run(Check));
await Suite(nameof(OtpProviderApiScenarios), () => OtpProviderApiScenarios.Run(Check));
await Suite(nameof(CustomerExperienceApiScenarios), () => CustomerExperienceApiScenarios.Run(Check));
await Suite(nameof(BusinessScenarios), () => BusinessScenarios.Run(Check));
await Suite(nameof(WorkflowSupportScenarios), () => WorkflowSupportScenarios.Run(Check));
await Suite(nameof(ContentAuditScenarios), () => ContentAuditScenarios.Run(Check));
await Suite(nameof(ServiceCmsScenarios), () => ServiceCmsScenarios.Run(Check));
await Suite(nameof(BannerContactScenarios), () => BannerContactScenarios.Run(Check));
await Suite(nameof(CmsVisibilityScenarios), () => CmsVisibilityScenarios.Run(Check));
await Suite(nameof(PricingScenarios), () => { PricingScenarios.Run(Check); return Task.CompletedTask; });
await Suite(nameof(TransportScenarios), () => { TransportScenarios.Run(Check); return Task.CompletedTask; });
await Suite(nameof(ApiScenarios), () => ApiScenarios.Run(Check));
await Suite(nameof(RequestQuoteScenarios), () => RequestQuoteScenarios.Run(Check));
await Suite(nameof(NotificationScenarios), () => NotificationScenarios.Run(Check));
await Suite(nameof(RealtimeScenarios), () => RealtimeScenarios.Run(Check));
await Suite(nameof(NotificationDeadlockScenarios), () => NotificationDeadlockScenarios.Run(Check));
{
    var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
        ["Otp:Enabled"]="true",["Otp:ApiKey"]="fake-provider-key-for-offline-tests",["Otp:CodeLength"]="4"
    }).Build();
    var handler=new ProviderHandler();
    using var http=new HttpClient(handler);
    var provider=new MelipayamakOtpSender(http,new MelipayamakOtpOptions(config),new());
    Check(await provider.Send(AuthFixture.Phone)=="0123","provider preserves leading zeroes in a four-digit code");
    var providerRequest=JsonNode.Parse(handler.Body!)!.AsObject();
    Check(handler.Method==HttpMethod.Post&&handler.Url=="https://console.melipayamak.com/api/send/otp/fake-provider-key-for-offline-tests"&&
        providerRequest.Count==1&&providerRequest["to"]!.ToString()==AuthFixture.Phone,"provider uses HTTP POST, the official console endpoint and an exact to-only payload");
    foreach(var payload in new[]{"{}", """{"code":null,"status":"rejected"}""","""{"code":"123","status":null}""","""{"code":1234,"status":null}""","not json"})
    {
        handler.Response=payload;
        try {await provider.Send(AuthFixture.Phone);throw new InvalidOperationException("Malformed provider response accepted.");}
        catch(UseCaseException error){Check(error.Kind==ErrorKind.Unavailable&&!error.ToString().Contains("fake-provider-key"),"provider errors fail closed without credentials or raw response");}
    }
    handler.Response="""{"code":"0123","status":null}""";handler.Status=HttpStatusCode.Forbidden;
    try{await provider.Send(AuthFixture.Phone);throw new InvalidOperationException("HTTP failure accepted.");}
    catch(UseCaseException e){Check(e.Kind==ErrorKind.Unavailable,"provider HTTP failures cannot authenticate");}
    var protector=new OtpCodeProtector(new MelipayamakOtpOptions(config));
    var hash=protector.Hash("challenge","phone","login","0123");
    Check(!protector.Matches(hash,"different","phone","login","0123")&&!protector.Matches(hash,"challenge","other","login","0123")&&
        !protector.Matches(hash,"challenge","phone","reset","0123"),"OTP digest is bound to its challenge, phone and purpose");
}
Console.WriteLine(failedSuites+" suites failed. "+checks+" authentication checks passed; no SQL connection or real SMS was used.");

internal sealed class ProviderHandler : HttpMessageHandler
{
    public string Response {get;set;}="""{"code":"0123","status":null}""";
    public HttpStatusCode Status {get;set;}=HttpStatusCode.OK;
    public string? Url {get;private set;}
    public string? Body {get;private set;}
    public HttpMethod? Method {get;private set;}
    public int Requests {get;private set;}
    public Exception? Failure {get;set;}
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        Requests++;
        Method=request.Method;Url=request.RequestUri!.AbsoluteUri;Body=await request.Content!.ReadAsStringAsync(cancellationToken);
        if (Failure != null) throw Failure;
        return new HttpResponseMessage(Status){Content=new StringContent(Response,Encoding.UTF8,"application/json")};
    }
}
