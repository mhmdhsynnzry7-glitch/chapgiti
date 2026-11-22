using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;
using ChapGiti.Domain.Common;
using ChapGiti.Infrastructure.Security;
using ChapGiti.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace ChapGiti.SqlAcceptanceTests;

internal sealed class OrderWorkflowFixtures(IsolatedDatabase database)
{
    public const string Password="Isolated.Sql@2026";
    public const string Phone="09100000001", OtherPhone="09100000002", Administrator="admin@isolated.invalid";
    public string Owner {get;}=Input.NewId();
    public string Other {get;}=Input.NewId();
    public string Completed {get;private set;}="";
    public string Accepted {get;private set;}="";
    public string Pending {get;private set;}="";
    public string GeneralTicket {get;private set;}="";
    public string LinkedTicket {get;private set;}="";
    public string OriginalFile {get;private set;}="";
    private readonly Dictionary<string,string> originalFileHashes=[];
    public string StorageRoot=>Path.Combine(database.DirectoryPath,"unused-private-storage");
    public PrivateFileStore Storage=>new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Storage:Root"]=StorageRoot}).Build());
    public static byte[] Pdf(string label="synthetic")
    {
        var prefix="%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n% "+label+"\n";
        return Encoding.ASCII.GetBytes(prefix+"xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \ntrailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n"+Encoding.ASCII.GetByteCount(prefix)+"\n%%EOF\n");
    }
    public async Task SeedIdentities()
    {
        var hash=new PasswordHasher().Hash(Password);var now=Input.Now();
        foreach(var (id,phone) in new[]{(Owner,Phone),(Other,OtherPhone)})
        {
            await database.Write("INSERT dbo.Customer_Registrations(id,phone,first_name,last_name,phone_verified,created_at,updated_at) VALUES(@p0,@p1,N'Synthetic',N'Owner',1,@p2,@p2)",id,phone,now);
            await database.Write("INSERT dbo.Customer_Accounts(phone,registration_id,password_hash,created_at) VALUES(@p0,@p1,@p2,@p3)",phone,id,hash,now);
        }
        await database.Write("INSERT dbo.Admin_Users(email,password_hash,created_at) VALUES(@p0,@p1,@p2)",Administrator,hash,now);
        await database.Write("INSERT dbo.Admin_Usernames(username,email) VALUES('admin',@p0)",Administrator);
    }
    public async Task SeedHistorical()
    {
        await SeedIdentities();
        Completed=await LegacyOrder("completed",123456);
        Accepted=await LegacyOrder("confirmed",234567,modern:true);
        Pending=await LegacyOrder("review",null);
        await database.Write("UPDATE dbo.Orders SET information_request=N'Historical required replacement',information_requires_file=1 WHERE id=@p0",Pending);
        var now=Input.Now();
        await database.Write("INSERT dbo.Order_Quotes(id,order_id,revision,amount_toman,scope_text,delivery_terms,exclusions_text,created_by_admin,sent_at,state,accepted_at,accepted_by_customer,decided_at) VALUES(@p0,@p1,1,234567,N'Historical immutable scope',N'Collection',N'Shipping',@p2,@p3,'accepted',@p3,@p4,@p3)",Input.NewId(),Accepted,Administrator,now,Owner);
        OriginalFile=await HistoricalFile(Pending,"original.pdf",Pdf("historical original"));
        await HistoricalFile(Accepted,"accepted.pdf",Pdf("historically accepted"));
        GeneralTicket=await Ticket(null,"Historical general");LinkedTicket=await Ticket(Completed,"Historical linked");
        await database.Write("INSERT dbo.Settings([key],[value]) VALUES(N'acceptance-preserve',N'unchanged'),(N'orderRequirements',N'{\"book-printing\":false,\"publishing\":false,\"cover-design\":false,\"page-layout\":false,\"catalog-printing\":false}')");
        await database.Write("INSERT dbo.Notifications(id,recipient_kind,customer_id,admin_email,event_type,entity_kind,entity_id,title,body,target_path,created_at,dedupe_key) VALUES(@p0,'customer',@p1,NULL,'quote.issued','order',@p2,N'Historical quote',N'Existing event',@p3,@p4,@p5)",Input.NewId(),Owner,Accepted,"/account/orders/"+Accepted,now,"historical:"+Input.NewId());
    }
    public async Task<string> LegacyOrder(string status,long? price,bool modern=false,string? owner=null)
    {
        var id=Input.NewId();var now=Input.Now();
        await database.Write("INSERT dbo.Orders(id,code,customer,phone,title,service,spec,estimate,final_price,status,created_at,updated_at,customer_id,workflow_version) VALUES(@p0,@p1,N'Synthetic Owner',@p2,N'Historical SQL order','book-printing',@p3,345678,@p4,@p5,@p6,@p6,@p7,@p8)",
            id,Input.NewId().Replace("-","")[..20],Phone,"{\"pages\":120,\"quantity\":1,\"size\":\"raghi\",\"paper\":\"white\",\"color\":\"bw\",\"binding\":\"soft\",\"priceBreakdown\":{\"total\":345678}}",price,status,now,owner??Owner,modern?2:1);
        return id;
    }
    public async Task<string> HistoricalFile(string order,string name,byte[] bytes)
    {
        var id=Input.NewId();var key=order+"/"+id+".pdf";
        await using(var store=database.Store()){await database.Guard(store);await Storage.WritePdf(key,bytes);}
        await database.Write("INSERT dbo.Files(id,order_id,object_key,name,size,created_at) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",id,order,key,name,bytes.Length,Input.Now());
        originalFileHashes[key]=Convert.ToHexString(SHA256.HashData(bytes));return id;
    }
    public async Task<string> Ticket(string? order,string subject)
    {
        var id=Input.NewId();var now=Input.Now();
        await database.Write("INSERT dbo.Tickets(id,customer_id,subject,status,created_at,updated_at,order_id) VALUES(@p0,@p1,@p2,'closed',@p3,@p3,@p4)",id,Owner,subject,now,order);
        await database.Write("INSERT dbo.Ticket_Messages(id,ticket_id,actor,message,created_at) VALUES(@p0,@p1,'customer',N'Original synthetic customer message',@p2),(@p3,@p1,'admin',N'Original synthetic administrator reply',@p2)",Input.NewId(),id,now,Input.NewId());return id;
    }
    public async Task<Dictionary<string,string>> Snapshot()
    {
        var queries=new Dictionary<string,string>{
            ["accounts"]="SELECT * FROM dbo.Customer_Accounts ORDER BY phone FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["profiles"]="SELECT * FROM dbo.Customer_Registrations ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["administrators"]="SELECT * FROM dbo.Admin_Users ORDER BY email FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["orders"]="SELECT id,code,customer,phone,email,title,service,spec,estimate,final_price,status,notes,admin_notes,version,created_at,updated_at,customer_id,workflow_version,information_request,information_requires_file FROM dbo.Orders ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["quotes"]="SELECT * FROM dbo.Order_Quotes ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["files"]="SELECT id,order_id,object_key,name,size,created_at FROM dbo.Files ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["tickets"]="SELECT * FROM dbo.Tickets ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["messages"]="SELECT * FROM dbo.Ticket_Messages ORDER BY id FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["notifications"]="SELECT * FROM dbo.Notifications ORDER BY sequence FOR JSON PATH,INCLUDE_NULL_VALUES",
            ["settings"]="SELECT [key],[value] FROM dbo.Settings WHERE [key]<>'order_seen_baseline_sequence' ORDER BY [key] FOR JSON PATH,INCLUDE_NULL_VALUES"
        };
        var snapshots=new Dictionary<string,string>();
        foreach(var (key,sql) in queries){var row=await database.Read("SELECT ("+sql+") snapshot");var json=row!.Str("snapshot");snapshots[key]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));}
        return snapshots;
    }
    public async Task VerifyPrivateHistory(AcceptanceChecks checks)
    {
        foreach(var (key,hash) in originalFileHashes)checks.Check(Convert.ToHexString(SHA256.HashData(await Storage.ReadPdf(key)))==hash,"synthetic historical PDF bytes remain unchanged");
    }
    public bool CleanupPrivateStorage()
    {
        var root=Path.GetFullPath(StorageRoot);var fixture=Path.GetFullPath(database.DirectoryPath)+Path.DirectorySeparatorChar;
        if(!root.StartsWith(fixture,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Private fixture cleanup escaped its owned directory.");
        if(Directory.Exists(root))Directory.Delete(root,true);return !Directory.Exists(root);
    }
}
