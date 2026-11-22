using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

internal sealed class OrderWorkflowApi : IDisposable
{
    public IsolatedDatabase Database {get;}
    public OrderWorkflowFixtures Fixtures {get;}
    public AcceptanceHost Host {get;}
    public AcceptanceChecks Checks {get;}
    public CookieContainer CustomerCookies {get;}=new();
    public CookieContainer AdministratorCookies {get;}=new();
    public CookieContainer OtherCookies {get;}=new();
    public HttpClient Customer {get;}
    public HttpClient Administrator {get;}
    public HttpClient Other {get;}
    public HttpClient Guest {get;}
    public OrderWorkflowApi(IsolatedDatabase database,OrderWorkflowFixtures fixtures,AcceptanceHost host,AcceptanceChecks checks)
    {
        Database=database;Fixtures=fixtures;Host=host;Checks=checks;
        Customer=host.Client(CustomerCookies);Administrator=host.Client(AdministratorCookies);Other=host.Client(OtherCookies);Guest=host.Client();
    }
    public async Task Login()
    {
        await Request(Customer,"POST","customer/login",new{phone=OrderWorkflowFixtures.Phone,password=OrderWorkflowFixtures.Password});
        await Request(Other,"POST","customer/login",new{phone=OrderWorkflowFixtures.OtherPhone,password=OrderWorkflowFixtures.Password});
        await Request(Administrator,"POST","auth/login",new{username="admin",password=OrderWorkflowFixtures.Password});
    }
    public async Task<JsonNode> Request(HttpClient client,string method,string path,object? body=null,int expected=200,string? code=null)
    {
        using var request=new HttpRequestMessage(new HttpMethod(method),"/api/"+path);
        if(body is HttpContent content)request.Content=content;else if(body!=null)request.Content=JsonContent.Create(body);
        using var response=await client.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
        Checks.Check((int)response.StatusCode==expected,$"real SQL API {method} {path} -> {expected} (actual {(int)response.StatusCode})");
        var result=string.IsNullOrWhiteSpace(text)?new JsonObject():JsonNode.Parse(text)!;
        if(code!=null)Checks.Check(result["code"]?.ToString()==code,"structured SQL-flow error "+code);
        return result;
    }
    public async Task<long> Version(string id)=>(await Database.Read("SELECT version FROM dbo.Orders WHERE id=@p0",id))!.Num("version");
    public Task<JsonNode> Detail(string id)=>Request(Administrator,"GET","admin/orders/"+id);
    public Task<JsonNode> CustomerDetail(string id)=>Request(Customer,"GET","customer/orders/"+id);
    public async Task<(long Notices,long Orders)> Counts(HttpClient client,string role)
    {
        var result=await Request(client,"GET",role+"/notifications/unread-count");
        return(result["unreadCount"]!.GetValue<long>(),result["unseenOrderCount"]!.GetValue<long>());
    }
    public async Task<JsonObject> Command(string id)=>new(){["idempotencyKey"]=Guid.NewGuid().ToString(),["expectedVersion"]=await Version(id)};
    public async Task<string> Create(string service="cover-design",bool file=false)
    {
        var body=Creation(service);
        var result=await Request(Customer,"POST","orders",file?Multipart(body,OrderWorkflowFixtures.Pdf()):body,201);
        return result["id"]!.ToString();
    }
    public static JsonObject Creation(string service="cover-design")=>new(){["idempotencyKey"]=Guid.NewGuid().ToString(),["title"]="Isolated SQL print request",["service"]=service,
        ["spec"]=new JsonObject{["pages"]=120,["quantity"]=1,["size"]="raghi",["paper"]="white",["color"]="bw",["binding"]="soft"}};
    public static MultipartFormDataContent Multipart(JsonObject body,byte[] bytes,string type="application/pdf")
    {
        var form=new MultipartFormDataContent();form.Add(JsonContent.Create(body),"payload");
        var content=new ByteArrayContent(bytes);content.Headers.ContentType=new(type);form.Add(content,"file","original.pdf");return form;
    }
    public async Task<JsonNode> Upload(string id,string? revision=null,byte[]? bytes=null,int expected=201,HttpClient? actor=null)
    {
        var row=await Database.Read("SELECT code FROM dbo.Orders WHERE id=@p0",id);
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/orders/"+id+"/files"+(revision==null?"":"?revisionId="+revision));
        request.Headers.Add("x-order-code",row!.Str("code"));request.Headers.Add("x-file-name","corrected.pdf");
        request.Content=new ByteArrayContent(bytes??OrderWorkflowFixtures.Pdf());request.Content.Headers.ContentType=new("application/pdf");
        using var response=await (actor??Customer).SendAsync(request);
        Checks.Check((int)response.StatusCode==expected,"real SQL private PDF upload -> "+expected+" (actual "+(int)response.StatusCode+")");
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }
    public async Task<JsonObject> Correction(string id,bool required)
    {
        var body=await Command(id);body["message"]="Correct the supplied print specification";body["requiresFile"]=required;return body;
    }
    public async Task<JsonObject> Submission(string id,string? file=null)
    {
        var latest=(await Detail(id))["corrections"]!.AsArray().Last()!;var body=await Command(id);
        body["revisionId"]=latest["id"]!.ToString();body["revisionVersion"]=latest["version"]!.GetValue<long>();body["message"]="Requested correction completed";body["fileId"]=file??"";return body;
    }
    public async Task<JsonObject> Review(string id,string decision="approve",string reason="Reviewed synthetic correction")
    {
        var latest=(await Detail(id))["corrections"]!.AsArray().Last()!;var body=await Command(id);
        body["revisionId"]=latest["id"]!.ToString();body["revisionVersion"]=latest["version"]!.GetValue<long>();body["decision"]=decision;body["reason"]=reason;return body;
    }
    public async Task Quote(string id,long amount=200000)
    {
        var body=await Command(id);body["amountToman"]=amount;body["scopeText"]="Agreed SQL-backed printing scope";body["deliveryTerms"]="Collection";body["exclusionsText"]="Shipping";
        await Request(Administrator,"POST","admin/orders/"+id+"/quotes",body);
    }
    public async Task Accept(string id)
    {
        var quote=(await CustomerDetail(id))["quotes"]!.AsArray().Last()!;var body=await Command(id);
        body["quoteId"]=quote["id"]!.ToString();body["quoteVersion"]=quote["version"]!.GetValue<long>();body["reason"]="";
        await Request(Customer,"POST","customer/orders/"+id+"/quote/accept",body);
    }
    public async Task<string> Ready()
    {
        var id=await Create();await Request(Administrator,"POST","admin/orders/"+id+"/open",new{});await Quote(id);await Accept(id);return id;
    }
    public void Dispose(){Customer.Dispose();Administrator.Dispose();Other.Dispose();Guest.Dispose();}
}
