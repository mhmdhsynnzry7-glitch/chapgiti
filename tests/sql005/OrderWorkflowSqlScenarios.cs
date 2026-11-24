using System.Text;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

internal static class OrderWorkflowSqlScenarios
{
    public static async Task Run(OrderWorkflowApi api)
    {
        var db=api.Database;var checks=api.Checks;
        var settings=await api.Request(api.Administrator,"GET","admin/settings");
        await api.Request(api.Administrator,"PUT","admin/settings",new JsonObject {
            ["site"]=settings["site"]!.DeepClone(),["rates"]=settings["rates"]!.DeepClone(),
            ["orderRequirements"]=new JsonObject{["book-printing"]=true,["publishing"]=false,["cover-design"]=false,["page-layout"]=false,["catalog-printing"]=false}},400);
        checks.Check((await api.Request(api.Guest,"GET","public/bootstrap"))["orderRequirements"]!.AsObject().All(option=>option.Value!.GetValue<bool>()),"public bootstrap requires a PDF for every print service");
        var before=await Snapshot(db);
        await api.Request(api.Customer,"POST","orders",OrderWorkflowApi.Creation("book-printing"),400,"ORDER_PDF_REQUIRED");
        var forged=OrderWorkflowApi.Creation("book-printing");forged["requiresPdf"]=false;forged["hasFile"]=true;
        await api.Request(api.Customer,"POST","orders",forged,400,"ORDER_PDF_REQUIRED");
        await api.Request(api.Customer,"POST","orders",OrderWorkflowApi.Multipart(OrderWorkflowApi.Creation("book-printing"),Encoding.ASCII.GetBytes("%PDF-fake")),400,"PDF_CONTENT_INVALID");
        await api.Request(api.Customer,"POST","orders",OrderWorkflowApi.Multipart(OrderWorkflowApi.Creation("book-printing"),OrderWorkflowFixtures.Pdf(),"text/plain"),415,"PDF_TYPE_INVALID");
        checks.Check(await Snapshot(db)==before,"invalid required-PDF intake leaves SQL orders/files/events/receipts/notifications untouched");
        var body=OrderWorkflowApi.Creation("book-printing");
        var id=(await api.Request(api.Customer,"POST","orders",OrderWorkflowApi.Multipart(body,OrderWorkflowFixtures.Pdf()),201))["id"]!.ToString();
        await api.Request(api.Customer,"POST","orders",OrderWorkflowApi.Multipart(body,OrderWorkflowFixtures.Pdf()));
        await api.Request(api.Customer,"POST","orders",OrderWorkflowApi.Multipart(body,OrderWorkflowFixtures.Pdf("different")),409);
        var original=(await db.Read("SELECT * FROM dbo.Files WHERE order_id=@p0",id))!;var originalId=original.Str("id");
        checks.Check(original.Num("file_version")==1&&(await db.Read("SELECT COUNT(*) n FROM dbo.Files WHERE order_id=@p0",id))!.Num("n")==1,"SQL multipart idempotency creates exactly one immutable original PDF version");
        await FileAccess(api,originalId,OrderWorkflowFixtures.Pdf());
        await RollbackInitial(api);
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/open",new{});await api.Quote(id,9_876_543_210);await api.Accept(id);
        var accepted=await JsonSnapshot(db,"SELECT * FROM dbo.Order_Quotes WHERE order_id=@p0 ORDER BY revision FOR JSON PATH,INCLUDE_NULL_VALUES",id);
        var noFlag=await api.Command(id);noFlag["message"]="Missing explicit file requirement";
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",noFlag,400,"CORRECTION_PDF_FLAG_REQUIRED");
        await api.Request(api.Customer,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true),401);
        var correction=await api.Correction(id,true);
        await Task.WhenAll(api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",correction),api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",correction));
        var cycle=(await api.Detail(id))["corrections"]!.AsArray().Last()!;var revision=cycle["id"]!.ToString();
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Order_Corrections WHERE order_id=@p0",id))!.Num("n")==1&&
            (await db.Read("SELECT COUNT(*) n FROM dbo.Notifications WHERE entity_id=@p0 AND event_type='order.information_requested'",id))!.Num("n")==1,"concurrent correction requests commit one cycle and one notification through real SQL locks");
        await BothGates(api,id,"ORDER_CORRECTION_REQUIRED");
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id),409,"CORRECTION_NEW_PDF_REQUIRED");
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id,originalId),409,"CORRECTION_NEW_PDF_REQUIRED");
        var unrelated=await api.Create(file:true);var foreign=(await db.Read("SELECT id FROM dbo.Files WHERE order_id=@p0",unrelated))!.Str("id");
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id,foreign),409,"CORRECTION_FILE_INVALID");
        await api.Request(api.Other,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id),404);
        var malicious=await api.Submission(id);malicious["requiresFile"]=false;
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",malicious,400,"CORRECTION_PAYLOAD_INVALID");
        await api.Upload(id,revision,Encoding.ASCII.GetBytes("%PDF-fake"),400);await api.Upload(id,revision,expected:404,actor:api.Other);await api.Upload(id,Guid.NewGuid().ToString(),expected:409);
        var wrongRevision=await api.Submission(id);wrongRevision["revisionId"]=Guid.NewGuid().ToString();
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",wrongRevision,409,"CORRECTION_VERSION_CONFLICT");
        var corrected=await api.Upload(id,revision,OrderWorkflowFixtures.Pdf("first correction"));var file2=corrected["id"]!.ToString();
        checks.Check(corrected["fileVersion"]!.GetValue<int>()==2&&corrected["revisionId"]!.ToString()==revision,"SQL replacement PDF is bound to the correct revision and monotonic version");
        var submission=await api.Submission(id,file2);
        await Task.WhenAll(api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",submission),api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",submission));
        checks.Check((await db.Read("SELECT state FROM dbo.Order_Corrections WHERE id=@p0",revision))!.Str("state")=="submitted","concurrent idempotent submissions persist pending administrator review");
        await BothGates(api,id,"ORDER_CORRECTION_REVIEW_REQUIRED");await api.Upload(id,revision,expected:409);
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",await api.Review(id,"reject",""),400);
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",await api.Review(id,"reject","Incorrect trim size"));
        var rejected=await JsonSnapshot(db,"SELECT * FROM dbo.Order_Corrections WHERE id=@p0 FOR JSON PATH,INCLUDE_NULL_VALUES",revision);
        await BothGates(api,id,"ORDER_CORRECTION_NOT_APPROVED");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",await api.Review(id),400);
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true));
        var secondCycle=(await api.Detail(id))["corrections"]!.AsArray().Last()!["id"]!.ToString();
        var chosen=(await api.Upload(id,secondCycle,OrderWorkflowFixtures.Pdf("chosen approved")))["id"]!.ToString();
        var newer=(await api.Upload(id,secondCycle,OrderWorkflowFixtures.Pdf("newer unselected")))["id"]!.ToString();
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id,chosen),409,"CORRECTION_FILE_STALE");
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id,newer));
        await RollbackReview(api,id);
        var review=await api.Review(id);
        await Task.WhenAll(api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",review),api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",review));
        var ready=await api.Detail(id);
        checks.Check(!ready["productionReadiness"]!["canStart"]!.GetValue<bool>()&&ready["productionReadiness"]!["approvedFileId"]!.ToString()==newer&&chosen!=newer,"SQL readiness identifies the latest reviewed PDF and still requires new terms");
        var forbiddenQuote=await api.Command(id);forbiddenQuote["amountToman"]=500000;forbiddenQuote["scopeText"]="Replacement quote";
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/quotes",forbiddenQuote,409,"ACCEPTED_PRICE_IMMUTABLE");
        checks.Check(await JsonSnapshot(db,"SELECT * FROM dbo.Order_Quotes WHERE order_id=@p0 ORDER BY revision FOR JSON PATH,INCLUDE_NULL_VALUES",id)==accepted,"attempted repricing preserves the accepted bigint quote byte-for-byte");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/start-printing",await api.Command(id),409,"FINAL_TERMS_REQUIRED");
        checks.Check((await db.Read("SELECT final_price FROM dbo.Orders WHERE id=@p0",id))!.Num("final_price")==9_876_543_210,"latest customer acceptance persists a bigint toman amount without truncation");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true));
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id,chosen),409,"CORRECTION_NEW_PDF_REQUIRED");
        var thirdCycle=(await api.Detail(id))["corrections"]!.AsArray().Last()!["id"]!.ToString();
        var file5=(await api.Upload(id,thirdCycle,OrderWorkflowFixtures.Pdf("last correction")))["id"]!.ToString();
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id,file5));
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",await api.Review(id));
        await api.SignTerms(id);
        var key=(await db.Read("SELECT object_key FROM dbo.Files WHERE id=@p0",file5))!.Str("object_key");
        var storage=api.Fixtures.Storage;var bytes=await storage.ReadPdf(key);await using(var guard=db.Store()){await db.Guard(guard);storage.DeletePdf(key);}
        try{await api.Request(api.Administrator,"POST","admin/orders/"+id+"/start-printing",await api.Command(id),409,"ORDER_FILE_UNAVAILABLE");}
        finally{await using var guard=db.Store();await db.Guard(guard);await storage.WritePdf(key,bytes);}
        await using(var guard=db.Store()){await db.Guard(guard);storage.DeletePdf(key);await storage.WritePdf(key,Encoding.ASCII.GetBytes("%PDF-fake"));}
        try{await api.Request(api.Administrator,"POST","admin/orders/"+id+"/start-printing",await api.Command(id),409,"PDF_CONTENT_INVALID");}
        finally{await using var guard=db.Store();await db.Guard(guard);storage.DeletePdf(key);await storage.WritePdf(key,bytes);}
        var start=await api.Command(id);
        await Task.WhenAll(api.Request(api.Administrator,"POST","admin/orders/"+id+"/start-printing",start),api.Request(api.Administrator,"POST","admin/orders/"+id+"/start-printing",start));
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Events WHERE order_id=@p0 AND status='printing'",id))!.Num("n")==1,"concurrent SQL production starts produce one committed printing event");
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Files WHERE order_id=@p0",id))!.Num("n")==5&&
            await JsonSnapshot(db,"SELECT * FROM dbo.Order_Corrections WHERE id=@p0 FOR JSON PATH,INCLUDE_NULL_VALUES",revision)==rejected,"all five PDF versions and immutable rejected correction history survive later cycles");
        var publicDetail=await api.CustomerDetail(id);var json=publicDetail.ToJsonString();
        checks.Check(!json.Contains("object_key")&&!json.Contains("reviewed_by_admin")&&!json.Contains("submissionFingerprint"),"customer SQL projection never exposes private storage keys or administrator identity");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true),400);
        await Legacy(api);await FileLimit(api);
    }
    private static async Task BothGates(OrderWorkflowApi api,string id,string correctionCode)
    {
        // The dedicated route rejects non-confirmed status first; PATCH reaches the same central correction gate.
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/start-printing",await api.Command(id),409);
        var modern=(await api.Database.Read("SELECT workflow_version FROM dbo.Orders WHERE id=@p0",id))!.Num("workflow_version")>=2;
        await api.Request(api.Administrator,"PATCH","admin/orders/"+id,new{idempotencyKey=Guid.NewGuid().ToString(),version=await api.Version(id),status="printing",finalPrice=200000},409,modern?"ORDER_ACTION_INVALID":correctionCode);
    }
    private static async Task Legacy(OrderWorkflowApi api)
    {
        var id=await api.Fixtures.LegacyOrder("review",null);
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true));
        await BothGates(api,id,"ORDER_CORRECTION_REQUIRED");
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.UploadSubmission(id));
        await BothGates(api,id,"ORDER_CORRECTION_REVIEW_REQUIRED");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",await api.Review(id,"reject","Clarify the title"));
        await BothGates(api,id,"ORDER_CORRECTION_NOT_APPROVED");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true));
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.UploadSubmission(id));
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",await api.Review(id));
        await api.Request(api.Administrator,"PATCH","admin/orders/"+id,new{version=await api.Version(id),status="printing",finalPrice=100},409,"CUSTOMER_APPROVAL_REQUIRED");
        api.Checks.Check((await api.Database.Read("SELECT status FROM dbo.Orders WHERE id=@p0",id))!.Str("status")=="review","historical PATCH cannot bypass mandatory customer financial consent");
    }
    private static async Task FileLimit(OrderWorkflowApi api)
    {
        await api.Database.Write("UPDATE dbo.Rate_Limits SET expires=0 WHERE [key] LIKE 'upload:%'");
        var id=await api.Create();await api.Upload(id,bytes:new byte[15*1024*1024+1],expected:413);for(var n=0;n<2;n++)await api.Upload(id);
        await Task.WhenAll(api.Upload(id),api.Upload(id));await api.Upload(id,expected:400);
        api.Checks.Check((await api.Database.Read("SELECT COUNT(*) n,COUNT(DISTINCT file_version) versions FROM dbo.Files WHERE order_id=@p0",id)) is {} row&&row.Num("n")==5&&row.Num("versions")==5,"SQL serialized concurrent uploads preserve the five-file limit and unique versions");
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/open",new{});
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true));
        var revision=(await api.Detail(id))["corrections"]!.AsArray().Last()!["id"]!.ToString();
        for(var n=0;n<5;n++)await api.Upload(id,revision);
        await api.Upload(id,revision,expected:400);
        api.Checks.Check((await api.Database.Read("SELECT COUNT(*) n FROM dbo.Files WHERE order_id=@p0",id))!.Num("n")==10,
            "five originals remain intact and the next correction has its own five-file budget");
    }
    private static async Task FileAccess(OrderWorkflowApi api,string file,byte[] bytes)
    {
        foreach(var (client,role,status) in new[]{(api.Customer,"customer",200),(api.Other,"customer",404),(api.Guest,"customer",401),(api.Administrator,"customer",401),(api.Administrator,"admin",200),(api.Customer,"admin",401)})
        foreach(var preview in new[]{false,true})
        {
            using var response=await client.GetAsync("/api/"+role+"/files/"+file+(preview?"?preview=true":""));
            api.Checks.Check((int)response.StatusCode==status,"SQL private PDF role/ownership/preview -> "+status);
            if(status!=200)continue;
            api.Checks.Check((await response.Content.ReadAsByteArrayAsync()).SequenceEqual(bytes)&&response.Content.Headers.ContentType?.MediaType=="application/pdf","SQL file metadata retrieves the exact private PDF bytes");
            api.Checks.Check(response.Headers.CacheControl?.NoStore==true&&response.Headers.GetValues("Content-Security-Policy").Single()=="sandbox"&&response.Headers.GetValues("X-Content-Type-Options").Single()=="nosniff","SQL private preview/download is sandboxed, uncached and non-sniffable");
            api.Checks.Check(preview?response.Content.Headers.ContentDisposition==null:response.Content.Headers.ContentDisposition?.DispositionType=="attachment","SQL private preview/download preserves disposition semantics");
        }
    }
    internal static async Task<string> JsonSnapshot(IsolatedDatabase db,string select,params object?[] values)=>(await db.Read("SELECT ("+select+") snapshot",values))!.Str("snapshot");
    internal static Task<string> Snapshot(IsolatedDatabase db)=>JsonSnapshot(db,"SELECT (SELECT COUNT(*) FROM dbo.Orders) orders,(SELECT COUNT(*) FROM dbo.Files) files,(SELECT COUNT(*) FROM dbo.Events) events,(SELECT COUNT(*) FROM dbo.Order_Commands) commands,(SELECT COUNT(*) FROM dbo.Notifications) notifications,(SELECT COUNT(*) FROM dbo.Order_Corrections) corrections FOR JSON PATH");
    private static async Task RollbackInitial(OrderWorkflowApi api)
    {
        var before=await Snapshot(api.Database);var files=Directory.GetFiles(api.Fixtures.StorageRoot,"*.pdf",SearchOption.AllDirectories).Order().ToArray();
        await FailNotification(api,async()=>await api.Request(api.Customer,"POST","orders",OrderWorkflowApi.Multipart(OrderWorkflowApi.Creation("book-printing"),OrderWorkflowFixtures.Pdf("rolled back")),500));
        api.Checks.Check(await Snapshot(api.Database)==before&&Directory.GetFiles(api.Fixtures.StorageRoot,"*.pdf",SearchOption.AllDirectories).Order().SequenceEqual(files),"real SQL notification failure rolls back initial order, receipt, event and newly written private PDF");
    }
    private static async Task RollbackReview(OrderWorkflowApi api,string id)
    {
        var before=await JsonSnapshot(api.Database,"SELECT * FROM dbo.Order_Corrections WHERE order_id=@p0 ORDER BY revision FOR JSON PATH,INCLUDE_NULL_VALUES",id);var version=await api.Version(id);var counts=await Snapshot(api.Database);
        await FailNotification(api,async()=>await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",await api.Review(id),500),afterProjection:true);
        api.Checks.Check(await JsonSnapshot(api.Database,"SELECT * FROM dbo.Order_Corrections WHERE order_id=@p0 ORDER BY revision FOR JSON PATH,INCLUDE_NULL_VALUES",id)==before&&await api.Version(id)==version&&await Snapshot(api.Database)==counts,"real SQL failed review transaction rolls back correction state, order version, receipt, event and notification");
    }
    internal static async Task FailNotification(OrderWorkflowApi api,Func<Task> action,bool afterProjection=false)
    {
        // Synthetic failure injection only inside the freshly-created, identity-guarded catalog.
        await api.Database.Write("CREATE TRIGGER dbo.Acceptance_RejectNotification ON dbo."+(afterProjection?"Audit_Logs":"Notifications")+" AFTER INSERT AS BEGIN SET NOCOUNT ON; THROW 51081,'Isolated acceptance notification failure',1; END");
        try{await action();}finally{await api.Database.Write("DROP TRIGGER dbo.Acceptance_RejectNotification");}
    }
}
