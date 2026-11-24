using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ChapGiti.Acceptance;

internal static class SupportAttachmentChecks
{
    public static async Task Run(Action<bool,string> check, HttpClient owner, HttpClient admin, HttpClient outsider, HttpClient guest)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ipUgAAAAASUVORK5CYII=");
        var prefix = "%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n";
        var pdf = System.Text.Encoding.ASCII.GetBytes(prefix+"xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \ntrailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n"+prefix.Length+"\n%%EOF\n");
        var initialKey = Guid.NewGuid().ToString();
        async Task<HttpResponseMessage> Intake(HttpClient client, string name) {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(initialKey), "idempotencyKey");
            form.Add(new StringContent("پیوست آزمایشی"), "subject");
            form.Add(new StringContent("درخواست آزمایشی برای بررسی پیوست"), "message");
            form.Add(new ByteArrayContent(png), "files", "initial.png");
            form.Add(new ByteArrayContent(pdf), "files", name);
            return await client.PostAsync("/api/tickets/attachments", form);
        }
        using var anonymous = await Intake(guest,"sample.pdf");
        check((int)anonymous.StatusCode==401,"unauthenticated initial attachment is rejected");
        using var invalidIntake = await Intake(owner,"wrong.jpg");
        check((int)invalidIntake.StatusCode==400,"invalid second initial file prevents the entire ticket");
        using var created = await Intake(owner,"sample.pdf");
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.ToString();
        var ticket = "/api/tickets/"+id;
        var adminTicket = "/api/admin/tickets/"+id;
        using var initialReplay = await Intake(owner,"sample.pdf");
        check((int)initialReplay.StatusCode==200 && (await initialReplay.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.ToString()==id,
            "lost initial response replays the original ticket");
        using var renamed = await Intake(owner,"another.pdf");
        check((int)renamed.StatusCode==409,"different initial file cannot reuse a ticket request");
        var initial = (await admin.GetFromJsonAsync<JsonObject>(adminTicket))!["messages"]!.AsArray().Single()!["attachments"]!.AsArray();
        check(initial.Count==2 && initial[0]!["name"]!.ToString()=="initial.png" && initial[1]!["name"]!.ToString()=="sample.pdf","first message retains the ordered image and PDF exactly once");
        var initialUrl="/attachments/"+initial[1]!["id"];
        check((await (await admin.GetAsync(adminTicket+initialUrl)).Content.ReadAsByteArrayAsync()).SequenceEqual(pdf),"admin downloads original initial PDF");
        using var notOwned = await outsider.GetAsync(ticket+initialUrl);
        check((int)notOwned.StatusCode==404,"initial attachments belong only to the owning customer or administrator");
        var key = Guid.NewGuid().ToString();
        async Task<HttpResponseMessage> Post(HttpClient client,string path,string messageKey,string fileName,byte[] bytes,int count=1) {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(messageKey),"idempotencyKey");
            form.Add(new StringContent("نمونهٔ کنترل"),"message");
            for(int i=0;i<count;i++){
                var file=new ByteArrayContent(bytes);
                file.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");
                form.Add(file,"files",fileName);
            }
            return await client.PostAsync(path+"/messages/attachments",form);
        }
        using var sent = await Post(owner,ticket,key,"sample.png",png,2);
        check((int)sent.StatusCode==201,"customer can attach two validated private images");
        using var retry = await Post(owner,ticket,key,"sample.png",png,2);
        check((int)retry.StatusCode==200,"a lost response can be retried without creating another message or file");
        using var changed = await Post(owner,ticket,key,"renamed.png",png,2);
        check((int)changed.StatusCode==409,"different attachments under the same idempotency key conflict");
        var detail=await admin.GetFromJsonAsync<JsonObject>(adminTicket);
        var msg=detail!["messages"]!.AsArray().Single(m=>m!["id"]!.ToString()==key)!;
        var files=msg["attachments"]!.AsArray();
        check(files.Count==2 && files.All(f=>f!["digest"]==null && f["mime"]!.ToString()=="image/png"),"persisted message exposes only display metadata");
        var suffix="/attachments/"+files[0]!["id"];
        foreach(var (client,path,expected) in new[]{(guest,ticket,401),(outsider,ticket,404),(owner,ticket,200),(admin,adminTicket,200),(owner,adminTicket,401)}) {
            using var download=await client.GetAsync(path+suffix);
            check((int)download.StatusCode==expected,"ticket attachment download enforces recipient/role boundary "+expected);
            if(expected==200)check((await download.Content.ReadAsByteArrayAsync()).SequenceEqual(png) &&
                download.Content.Headers.ContentDisposition?.DispositionType=="attachment","authorized download returns original bytes as attachment");
        }
        using var invalid=await Post(owner,ticket,Guid.NewGuid().ToString(),"photo.jpg",png);
        check((int)invalid.StatusCode==400 && (await invalid.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.ToString()=="TICKET_ATTACHMENT_INVALID_TYPE","mismatched attachment is rejected before storage");
        using var many=await Post(owner,ticket,Guid.NewGuid().ToString(),"photo.png",png,4);
        check((int)many.StatusCode==400,"attachment count limit applies");
        using var oversized=await Post(owner,ticket,Guid.NewGuid().ToString(),"large.png",new byte[4*1024*1024+1]);
        check((int)oversized.StatusCode==413,"per-file size limit applies on the server");
        using var response=await Post(admin,adminTicket,Guid.NewGuid().ToString(),"answer.png",png);
        check((int)response.StatusCode==201,"administrator can attach a private response");
        var reopened=await owner.GetFromJsonAsync<JsonObject>(ticket);
        check(reopened!["messages"]!.AsArray().Count==3 && reopened["ticket"]!["status"]!.ToString()=="open",
            "reloading preserves both attachments and a reply does not demand customer action");
    }
}
