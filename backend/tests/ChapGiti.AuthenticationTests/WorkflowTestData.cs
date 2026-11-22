using System.Text;
using ChapGiti.Application.Common;
using ChapGiti.Domain.Common;

namespace ChapGiti.AuthenticationTests;

internal static class WorkflowTestData
{
    public static HttpContent PrintRequest(object body)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToNode(body)!.AsObject();
        payload["termsAccepted"] = true;
        var form = new MultipartFormDataContent();
        form.Add(System.Net.Http.Json.JsonContent.Create(payload), "payload");
        var file = new ByteArrayContent(Pdf());
        file.Headers.ContentType = new("application/pdf");
        form.Add(file, "file", "print.pdf");
        return form;
    }

    public static byte[] Pdf(string text="synthetic")
    {
        var prefix="%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n% "+text+"\n";
        return Encoding.ASCII.GetBytes(prefix+"xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \ntrailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n"+Encoding.ASCII.GetByteCount(prefix)+"\n%%EOF\n");
    }
    public static string Ticket(MemoryApiData data,string owner,string? order=null,string subject="Historical support")
    {
        var id=Guid.NewGuid().ToString();var now=Input.Now();
        data.Tickets[id]=new(){["id"]=id,["customer_id"]=owner,["subject"]=subject,["status"]="open",["created_at"]=now,["updated_at"]=now,["order_id"]=order};
        data.MessagesById[id]=new(){["id"]=id,["ticket_id"]=id,["actor"]="customer",["message"]="Retained synthetic historical message",["created_at"]=now};
        return id;
    }
}
