using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Acceptance;

namespace ChapGiti.AuthenticationTests;

internal static class SupportFilesScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        // Two uniform 2×2 images encoded by sharp, independent of uploaded filenames.
        var jpeg=Convert.FromBase64String("/9j/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/wAARCAACAAIDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAj/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFAEBAAAAAAAAAAAAAAAAAAAAAP/EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAMAwEAAhEDEQA/AKpAB//Z");
        var webp=Convert.FromBase64String("UklGRiQAAABXRUJQVlA4IBgAAAAwAQCdASoCAAIAAUAmJaQAA3AA/vz0AAA=");
        check(ChapGiti.Domain.Tickets.TicketFileFormat.Mime(jpeg,".jpeg")=="image/jpeg" && ChapGiti.Domain.Tickets.TicketFileFormat.Mime(jpeg,".jpg")=="image/jpeg","JPEG attachment is recognized from its structure");
        check(ChapGiti.Domain.Tickets.TicketFileFormat.Mime(webp,".webp")=="image/webp","WebP attachment is recognized from its container and image frame");
        check(ChapGiti.Domain.Tickets.TicketFileFormat.Mime(webp[..20],".webp")==null && ChapGiti.Domain.Tickets.TicketFileFormat.Mime(jpeg[..20],".jpg")==null,"truncated image files are rejected");
        await using var host=await TestApiHost.Start(browser:true);
        HttpClient Client() {
            var client=new HttpClient(new HttpClientHandler{UseCookies=false}){BaseAddress=new Uri(host.Origin)};
            client.DefaultRequestHeaders.Add("Origin",host.Origin);
            return client;
        }
        async Task Login(HttpClient client,string role,string phone="") {
            var response = role=="admin" ?
                await client.PostAsJsonAsync("/api/auth/login",new{username="admin",password=AuthFixture.Password}):
                await client.PostAsJsonAsync("/api/customer/login",new{phone,password=AuthFixture.Password});
            response.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Add("Cookie",string.Join("; ",response.Headers.GetValues("Set-Cookie").Select(c=>c.Split(';')[0])));
        }
        using var admin=Client();using var owner=Client();using var other=Client();using var guest=Client();
        await Login(admin,"admin");await Login(owner,"customer",AuthFixture.Phone);await Login(other,"customer","09100000000");
        await SupportAttachmentChecks.Run(check,owner,admin,other,guest);
        var threadId = host.Data.Tickets.Keys.Single();
        var fileCount=host.Data.Assets.Count;var messageCount=host.Data.MessagesById.Count;
        host.Data.FailNextNotification=true;
        using (var form=new MultipartFormDataContent()) {
            form.Add(new StringContent(Guid.NewGuid().ToString()),"idempotencyKey");
            form.Add(new StringContent("امتحان بازگشت تراکنش"),"message");
            var image=host.Data.Assets.Values.First();
            form.Add(new ByteArrayContent(image),"files","نمونه.png");
            var aborted=await owner.PostAsync("/api/tickets/"+threadId+"/messages/attachments",form);
            check(!aborted.IsSuccessStatusCode && host.Data.Assets.Count==fileCount && host.Data.MessagesById.Count==messageCount && host.Data.Attachments.Count==5,
                  "rolled-back ticket transaction leaves no physical orphan, message or metadata");
        }
        var ticketCount=host.Data.Tickets.Count;
        var metadataCount=host.Data.Attachments.Count;
        host.Data.FailNextNotification=true;
        using (var form=new MultipartFormDataContent()) {
            form.Add(new StringContent(Guid.NewGuid().ToString()),"idempotencyKey");
            form.Add(new StringContent("تیکت آزمایشی"),"subject");
            form.Add(new StringContent("آزمون بازگشت ایجاد تیکت و پیوست"),"message");
            form.Add(new ByteArrayContent(host.Data.Assets.Values.First()),"files","نمونه.png");
            using var rejected=await owner.PostAsync("/api/tickets/attachments",form);
            check(!rejected.IsSuccessStatusCode && host.Data.Tickets.Count==ticketCount && host.Data.MessagesById.Count==messageCount
                  && host.Data.Assets.Count==fileCount && host.Data.Attachments.Count==metadataCount,
                  "failed initial notification rolls back ticket, initial message, metadata and private bytes");
        }
        var directory=Path.Combine(Path.GetTempPath(),"chap-log-test-"+Guid.NewGuid().ToString("N"));
        host.App.Configuration["Storage:Root"]=directory;
        host.App.Configuration["Diagnostics:FileBytes"]="1024";
        host.App.Configuration["Diagnostics:RetainFiles"]="2";
        try {
            string? requestId=null;
            for(int i=0;i<19;i++){
                host.Data.FailNextOrderList=true;
                using var res=await admin.GetAsync("/api/admin/orders?q=PRIVATE-INPUT-SENTINEL");
                var error=await res.Content.ReadFromJsonAsync<JsonObject>();
                requestId=error!["requestId"]!.ToString();
                check((int)res.StatusCode==503 && error["code"]!=null && res.Headers.GetValues("X-Request-Id").Single()==requestId,"503 error document correlates with response header");
            }
            var logs=Directory.GetFiles(Path.Combine(directory,"logs"),"errors-*.jsonl");
            var events=logs.SelectMany(File.ReadAllLines).ToArray();
            check(logs.Length==2 && events.Length<19,"bounded rolling log retains configured file count");
            check(events.Any(line=>line.Contains(requestId!)) && events.All(line=>!line.Contains("PRIVATE-INPUT-SENTINEL")&&!line.Contains("Cookie")&&!line.Contains(AuthFixture.Password)),"allowlisted server diagnostics contain correlation and omit private request values");
            host.App.Configuration["Storage:Root"]=logs[0];
            host.Data.FailNextOrderList=true;
            using var failure=await admin.GetAsync("/api/admin/orders");
            check((int)failure.StatusCode==503,"inaccessible log destination preserves the application error response");
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
