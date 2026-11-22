using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ChapGiti.Api.Http;
using ChapGiti.Application.Abstractions;
using ChapGiti.Application.Common;
using ChapGiti.Application.Authentication;
using ChapGiti.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChapGiti.AuthenticationTests;

internal static class ApiScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host=await TestApiHost.Start();
        var f=host.Fixture;var actor=f.Store.Accounts[AuthFixture.Phone].Id;var memory=host.Data;
        var app=host.App;var origin=host.Origin;
        using var client=new HttpClient(new HttpClientHandler {UseCookies=false}) {BaseAddress=new Uri(origin)};
        var cookies=new Dictionary<string,string>();
        async Task<HttpResponseMessage> Send(string method,string path,object? body=null,bool authenticated=true,string? source=null)
        {
            var request=new HttpRequestMessage(new HttpMethod(method),path);
            request.Headers.TryAddWithoutValidation("Origin",source??origin);
            if(authenticated&&cookies.Count>0)request.Headers.TryAddWithoutValidation("Cookie",string.Join("; ",cookies.Select(x=>x.Key+"="+x.Value)));
            if(body is HttpContent content)request.Content=content;
            else if(body!=null)request.Content=path is "orders" or "/api/orders" ? WorkflowTestData.PrintRequest(body) : JsonContent.Create(body);
            var response=await client.SendAsync(request);
            if(response.Headers.TryGetValues("Set-Cookie",out var values))
                foreach(var value in values)
                {
                    var pair=value.Split(';')[0].Split('=',2);
                    if(pair[0] is not ("giti_customer_access" or "giti_customer_refresh" or "giti_admin_access" or "giti_admin_refresh"))continue;
                    if(pair[1]=="")cookies.Remove(pair[0]);else cookies[pair[0]]=pair[1];
                }
            return response;
        }
        async Task<JsonNode?> Expect(string method,string path,object? body=null,int status=200,bool authenticated=true)
        {
            using var response=await Send(method,path,body,authenticated);
            var text=await response.Content.ReadAsStringAsync();
            if((int)response.StatusCode!=status)throw new InvalidOperationException(method+" "+path+" expected "+status+" got "+(int)response.StatusCode+": "+text);
            check(true,"HTTP "+method+" "+path+" -> "+status);
            return text.Length==0||response.Content.Headers.ContentType?.MediaType!="application/json"?null:JsonNode.Parse(text);
        }
        try
        {
            var endpoints=((IEndpointRouteBuilder)app).DataSources.SelectMany(x=>x.Endpoints).OfType<RouteEndpoint>().ToArray();
            var routes=endpoints.SelectMany(e=>e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Select(m=>m+" "+Regex.Replace(e.RoutePattern.RawText??"",@"\{[^}]+\}","{id}"))??[]).ToHashSet();
            var expected=new[]{
                "GET api/public/bootstrap","GET api/auth/me","POST api/auth/login","POST api/auth/logout",
                "POST api/customer/start","POST api/customer/otp/request","POST api/customer/otp/verify","POST api/customer/registration/complete",
                "POST api/customer/password/verify","POST api/customer/password/reset","POST api/customer/login",
                "GET api/customer/me","PUT api/customer/profile","POST api/customer/logout","GET api/customer/orders",
                "POST api/quote","POST api/orders","POST api/track","POST api/contact","GET api/tickets","POST api/tickets",
                "POST api/orders/{id}/files","GET api/tickets/{id}","POST api/tickets/{id}/messages",
                "GET api/banner-assets/{id}","GET api/admin/overview","GET api/admin/orders","GET api/admin/export",
                "GET api/admin/customers","GET api/admin/messages","GET api/admin/sms","GET api/admin/audit",
                "GET api/admin/settings","PUT api/admin/settings","GET api/admin/banners","PUT api/admin/banners",
                "GET api/admin/portfolio","PUT api/admin/portfolio","POST api/admin/banner-assets","GET api/admin/articles",
                "POST api/admin/articles","PUT api/admin/articles/{id}","POST api/admin/seed","GET api/admin/tickets",
                "GET api/admin/orders/{id}","PATCH api/admin/orders/{id}","POST api/admin/orders/{id}/open","POST api/admin/orders/{id}/notes","GET api/admin/activity","GET api/admin/files/{id}","PATCH api/admin/messages/{id}",
                "GET api/admin/tickets/{id}","PATCH api/admin/tickets/{id}","POST api/admin/tickets/{id}/messages"
            };
            check(expected.All(routes.Contains),"every legacy dispatcher method/URL is represented by a Controller");
            var actions=endpoints.SelectMany(e=>e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Select(m=>m+" "+e.RoutePattern.RawText+" "+
                string.Join(",",e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Mvc.ConsumesAttribute>().SelectMany(c=>c.ContentTypes).Order()))??[]).ToArray();
            check(actions.Distinct().Count()==actions.Length,"Controller method/URL/media-type actions are unambiguous");
            foreach(var route in routes.Where(x=>x.Contains("api/admin/")))
            {
                var parts=route.Split(' ',2);
                await Expect(parts[0],"/"+parts[1].Replace("{id}",Guid.NewGuid().ToString()),parts[0]=="GET"?null:new{},401,false);
            }
            await Expect("GET","/health");
            await Expect("GET","/api/public/bootstrap");
            await Expect("GET","/api/customer/me",status:401);
            using(var badOrigin=await Send("POST","/api/customer/login",new {phone=AuthFixture.Phone,password=AuthFixture.Password},source:"https://untrusted.invalid"))
                check(badOrigin.StatusCode==HttpStatusCode.Forbidden,"CSRF/origin protection runs before authentication and model binding");
            using(var missingOrigin=new HttpRequestMessage(HttpMethod.Post,"/api/auth/refresh"))
            using(var response=await client.SendAsync(missingOrigin))
                check(response.StatusCode==HttpStatusCode.Forbidden,"refresh requires a matching Origin header");
            await Expect("POST","/api/customer/login",new {phone=AuthFixture.Phone,password=AuthFixture.Password,roles=new[]{"admin"}},400);
            await Expect("POST","/api/customer/login",new {phone=AuthFixture.Phone,password="Wrong.Password@1"},401);
            using(var login=await Send("POST","/api/customer/login",new {phone=AuthFixture.Phone,password=AuthFixture.Password}))
            {
                check(login.IsSuccessStatusCode,"HTTP password login succeeds");
                var set=login.Headers.GetValues("Set-Cookie").Where(x=>x.StartsWith("giti_customer_access=")||x.StartsWith("giti_customer_refresh=")).ToArray();
                check(set.Length==2&&set.All(x=>x.Contains("httponly",StringComparison.OrdinalIgnoreCase)&&x.Contains("samesite=strict",StringComparison.OrdinalIgnoreCase)),
                    "both authentication cookies are HttpOnly and SameSite Strict");
            }
            await Expect("GET","/api/auth/me",status:401);
            await Expect("POST","/api/auth/login",new {username="admin",password=AuthFixture.Password});
            await Expect("GET","/api/auth/me");
            await Expect("GET","/api/customer/me");
            await Expect("GET","/api/auth/session");
            await Expect("GET","/api/admin/users?q=&role=&verified=&page=1&pageSize=20");
            await Expect("GET","/api/admin/users?page=0",status:400);
            await Expect("PUT","/api/admin/users/"+actor,new {firstName="Malicious",lastName="Update"},404);
            await Expect("PUT","/api/customer/profile",new {firstName="Owner",lastName="Updated",id=Guid.NewGuid()},400);
            await Expect("PUT","/api/customer/profile",new {firstName="Owner",lastName="Updated",phone="09111111111"},400);
            await Expect("PUT","/api/customer/profile",new {firstName="Owner",lastName="Updated",phone=AuthFixture.Phone});
            await Expect("PUT","/api/admin/users/"+actor+"/roles/admin",new {},400);
            await Expect("PUT","/api/admin/users/"+actor+"/roles/admin",new {enabled=false},400);
            await Expect("POST","/api/customer/otp/request",new {phone=AuthFixture.Phone,purpose="login"},400);

            var spec=new JsonObject {["pages"]="۱۲۰",["quantity"]="١٠٠",["size"]="raghi",["paper"]="white",["color"]="bw",["binding"]="soft"};
            var quote=await Expect("POST","/api/quote",spec);
            check(quote!["total"]!.GetValue<long>()==3792000,"HTTP calculator normalizes Persian/Arabic strings without changing tariffs");
            foreach(var value in new JsonNode?[]{null,JsonValue.Create(""),JsonValue.Create(3),JsonValue.Create(2001),JsonValue.Create(4.5),JsonValue.Create(true)})
            {
                var invalid=(JsonObject)spec.DeepClone();invalid["pages"]=value;
                await Expect("POST","/api/quote",invalid,400);
            }
            var key=Guid.NewGuid().ToString();
            var order=await Expect("POST","/api/orders",new {title="Regression book",spec,idempotencyKey=key},201);
            await Expect("POST","/api/orders",new {title="Regression book",spec,idempotencyKey=key});
            check(memory.Orders.Count==1,"Controller migration preserves order idempotency");
            var orderId=order!["id"]!.ToString();var code=order["code"]!.ToString();
            await Expect("POST","/api/track",new {code,phone=AuthFixture.Phone});
            await Expect("GET","/api/customer/orders");
            using(var content=new ByteArrayContent(WorkflowTestData.Pdf()))
            {
                content.Headers.ContentType=new("application/pdf");
                var request=new HttpRequestMessage(HttpMethod.Post,"/api/orders/"+orderId+"/files"){Content=content};
                request.Headers.Add("Origin",origin);request.Headers.Add("Cookie",string.Join("; ",cookies.Select(x=>x.Key+"="+x.Value)));request.Headers.Add("x-order-code",code);request.Headers.Add("x-file-name","test.pdf");
                using var upload=await client.SendAsync(request);check(upload.StatusCode==HttpStatusCode.Created,"owned private PDF upload remains supported");
            }
            var ticketId=WorkflowTestData.Ticket(memory,memory.Orders[orderId].Str("customer_id"));
            await Expect("POST","/api/tickets",new{subject="Disabled support",message="Open support ticket"},201);
            await Expect("GET","/api/tickets");await Expect("GET","/api/tickets/"+ticketId);
            await Expect("POST","/api/tickets/"+ticketId+"/messages",new{message="Customer reply",idempotencyKey=Guid.NewGuid().ToString()},201);
            await Expect("POST","/api/contact",new{subject="Disabled contact",message="New contact ticket"},201);

            foreach(var path in new[]{"overview","orders","export","customers","messages","sms","audit","settings","banners","portfolio","articles","tickets","orders/"+orderId,"tickets/"+ticketId})
                await Expect("GET","/api/admin/"+path);
            await Expect("GET","/api/admin/files/"+memory.Files.Keys.First());
            await Expect("POST","/api/admin/orders/"+orderId+"/open",new {});
            await Expect("PATCH","/api/admin/messages/"+Guid.NewGuid(),new {});
            await Expect("PATCH","/api/admin/tickets/"+ticketId,new {status="open"});
            await Expect("POST","/api/admin/tickets/"+ticketId+"/messages",new {message="Administrator reply",idempotencyKey=Guid.NewGuid().ToString()},201);
            await Expect("PUT","/api/admin/settings",new {site=memory.Copy("settings"),rates=memory.Copy("rates")});
            await Expect("PUT","/api/admin/banners",memory.Copy("banners"));
            await Expect("PUT","/api/admin/portfolio",memory.Copy("portfolio"));
            var article=new {slug="regression-article",title="Regression article",excerpt="A sufficiently long excerpt",content=new string('x',40),category="Testing",status="draft",version=1};
            var savedArticle=await Expect("POST","/api/admin/articles",article,201);
            await Expect("PUT","/api/admin/articles/"+savedArticle!["id"],article);
            await Expect("POST","/api/admin/seed");
            var image=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jfZkAAAAASUVORK5CYII=");
            using(var content=new ByteArrayContent(image))
            {
                content.Headers.ContentType=new("image/png");
                var saved=await Expect("POST","/api/admin/banner-assets",content,201);
                await Expect("GET",saved!["url"]!.ToString());
            }
            using(var oversized=new StringContent("{\"padding\":\""+new string('x',100_001)+"\"}",Encoding.UTF8,"application/json"))
                await Expect("POST","/api/quote",oversized,413);
            using(var wrongMedia=new StringContent("not JSON",Encoding.UTF8,"text/plain"))
                await Expect("POST","/api/quote",wrongMedia,415);
            var created=await Expect("POST","/api/admin/users",new {firstName="New",lastName="User",phone="09111111111",password=AuthFixture.Password,confirmation=AuthFixture.Password},201);
            var newId=created!["id"]!.ToString();
            await Expect("PUT","/api/admin/users/"+newId+"/roles/admin",new {enabled=true},400);
            f.Store.Profiles[newId]=f.Store.Profiles[newId] with {Verified=true};
            await Expect("PUT","/api/admin/users/"+newId+"/roles/admin",new {enabled=true},400);
            await Expect("PUT","/api/admin/users/"+newId+"/roles/admin",new {enabled=false},400);
            await Expect("POST","/api/auth/refresh");
            await Expect("POST","/api/customer/logout");
            await Expect("GET","/api/admin/users");
            await Expect("GET","/api/customer/me",status:401);
            await Expect("POST","/api/customer/refresh",status:401);
            await Expect("POST","/api/auth/logout");
            await Expect("GET","/api/admin/users",status:401);
            await Expect("POST","/api/auth/refresh",status:401);
            check(memory.Orders.Count==1&&memory.Tickets.Count==3,"archived and new support conversations coexist in the memory-only API");
            await using var production=await TestApiHost.Start(production:true);
            using var productionClient=new HttpClient {BaseAddress=new Uri(production.Origin)};
            foreach(var path in new[]{"customer/login","customer/otp/request","CUSTOMER/otp/request","customer/password/verify","auth/refresh"})
            {
                using var request=new HttpRequestMessage(HttpMethod.Post,"/api/"+path){Content=JsonContent.Create(new {phone=AuthFixture.Phone,password=AuthFixture.Password,purpose="reset"})};
                request.Headers.Add("Origin",production.Origin);
                using var response=await productionClient.SendAsync(request);
                check((int)response.StatusCode==503,"production HTTP blocks "+path+" before model binding and credential processing");
            }
            check(production.Fixture.Sender.Sends==0&&production.Fixture.Store.Access.Count==0,"HTTP production cannot send authentication SMS or create credentials");
        }
        finally { await app.StopAsync(); }
    }
}
