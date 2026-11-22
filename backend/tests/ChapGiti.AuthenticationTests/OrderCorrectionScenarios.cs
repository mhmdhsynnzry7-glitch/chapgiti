using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;
using ChapGiti.Domain.Printing;

namespace ChapGiti.AuthenticationTests;

internal static class OrderCorrectionScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        check(PdfFormat.IsPdf(WorkflowTestData.Pdf()),"PDF parser accepts a structurally complete test document");
        foreach(var bytes in new[]{Array.Empty<byte>(),Encoding.ASCII.GetBytes("%PDF-fake"),Encoding.ASCII.GetBytes("%PDF-1.7\nobj endobj startxref\n0\n%%EOF"),WorkflowTestData.Pdf()[..^8]})
            check(!PdfFormat.IsPdf(bytes),"PDF parser rejects missing content, false signatures and truncation");
        var badOffset=Encoding.ASCII.GetString(WorkflowTestData.Pdf()).Replace("startxref\n", "startxref\n999");
        check(!PdfFormat.IsPdf(Encoding.ASCII.GetBytes(badOffset)),"PDF parser validates the cross-reference offset");
        await using var host=await TestApiHost.Start();var data=host.Data;
        using var customer=new HttpClient{BaseAddress=new(host.Origin)};
        using var admin=new HttpClient{BaseAddress=new(host.Origin)};
        using var other=new HttpClient{BaseAddress=new(host.Origin)};
        using var guest=new HttpClient{BaseAddress=new(host.Origin)};
        async Task<JsonNode> Req(HttpClient client,string method,string path,object? body=null,int expected=200,string? code=null)
        {
            using var request=new HttpRequestMessage(new(method),"/api/"+path);request.Headers.Add("Origin",host.Origin);
            if(body is HttpContent content)request.Content=content;else if(body!=null)request.Content=JsonContent.Create(body);
            using var response=await client.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
            check((int)response.StatusCode==expected,$"corrections {method} {path} -> {expected} (actual {(int)response.StatusCode}): "+(response.IsSuccessStatusCode?"":text));
            var result=JsonNode.Parse(text)!;if(code!=null)check(result["code"]?.ToString()==code,"structured correction error "+code);return result;
        }
        await Req(customer,"POST","customer/login",new{phone=AuthFixture.Phone,password=AuthFixture.Password});
        await Req(admin,"POST","auth/login",new{username="admin",password=AuthFixture.Password});
        var otherId=Guid.NewGuid().ToString();const string otherPhone="09177777777";
        host.Fixture.Store.Profiles[otherId]=new(otherId,otherPhone,"Other","Owner",true);
        host.Fixture.Store.Accounts[otherPhone]=(otherId,host.Fixture.Store.Accounts[AuthFixture.Phone].Hash);
        await Req(other,"POST","customer/login",new{phone=otherPhone,password=AuthFixture.Password});
        object spec=new{pages=120,quantity=1,size="raghi",paper="white",color="bw",binding="soft"};
        object CreateBody(string? id=null,string service="book-printing")=>new{idempotencyKey=id??Guid.NewGuid().ToString(),title="Correction test order",service,spec,termsAccepted=true};
        long Version(string id)=>data.Orders[id].Num("version");
        MultipartFormDataContent Multipart(object payload,byte[]? bytes=null,string type="application/pdf")
        {
            var form=new MultipartFormDataContent();form.Add(JsonContent.Create(payload),"payload");
            var file=new ByteArrayContent(bytes??WorkflowTestData.Pdf());file.Headers.ContentType=new(type);form.Add(file,"file","initial.pdf");return form;
        }
        async Task<JsonNode> Upload(string id,string revision,byte[]? bytes=null,HttpClient? client=null,int expected=201)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/api/orders/"+id+"/files"+(revision==""?"":"?revisionId="+revision));
            request.Headers.Add("Origin",host.Origin);request.Headers.Add("x-order-code",data.Orders[id].Str("code"));request.Headers.Add("x-file-name","corrected.pdf");
            request.Content=new ByteArrayContent(bytes??WorkflowTestData.Pdf());request.Content.Headers.ContentType=new("application/pdf");
            using var response=await (client??customer).SendAsync(request);var result=await response.Content.ReadFromJsonAsync<JsonNode>();
            check((int)response.StatusCode==expected,$"corrected PDF upload -> {expected} (actual {(int)response.StatusCode})");return result!;
        }
        object Correction(string id,bool file,string? key=null)=>new{idempotencyKey=key??Guid.NewGuid().ToString(),expectedVersion=Version(id),message="Please correct the supplied content",reason="Trim mismatch",requiresFile=file};
        object Submission(string id,string? file=null)=>new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),revisionId=data.CorrectionRows.Values.Where(c=>c.OrderId==id).MaxBy(c=>c.Revision)!.Id,revisionVersion=data.CorrectionRows.Values.Where(c=>c.OrderId==id).MaxBy(c=>c.Revision)!.Version,message="Requested corrections completed",fileId=file??""};
        object Review(string id,string decision="approve",string reason="Reviewed and approved")=>new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),revisionId=data.CorrectionRows.Values.Where(c=>c.OrderId==id).MaxBy(c=>c.Revision)!.Id,revisionVersion=data.CorrectionRows.Values.Where(c=>c.OrderId==id).MaxBy(c=>c.Revision)!.Version,decision,reason};
        object SendQuote(string id,long amount)=>new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),amountToman=amount,scopeText="Agreed print specification",deliveryTerms="Collection",exclusionsText="Shipping"};
        object Start(string id)=>new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id)};
        async Task Accept(string id){var q=data.QuoteRows.Values.Where(q=>q.Str("order_id")==id).MaxBy(q=>q.Num("revision"))!;await Req(customer,"POST","customer/orders/"+id+"/quote/accept",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),quoteId=q.Str("id"),quoteVersion=q.Num("version")});}

        await Req(customer,"POST","orders",Multipart(new {title="Missing terms",spec}),400,"ORDER_TERMS_REQUIRED");
        await Req(customer,"POST","orders",CreateBody(service:"unknown-service"),400,"ORDER_SERVICE_INVALID");
        var count=data.Orders.Count;
        await Req(customer,"POST","orders",CreateBody(),400,"ORDER_PDF_REQUIRED");
        await Req(customer,"POST","orders",new{idempotencyKey=Guid.NewGuid().ToString(),title="Forged file flag",service="book-printing",spec,termsAccepted=true,hasFile=true,requiresPdf=false,fileId=Guid.NewGuid().ToString()},400,"ORDER_PDF_REQUIRED");
        await Req(customer,"POST","orders",Multipart(CreateBody(),Encoding.ASCII.GetBytes("%PDF-fake")),400,"PDF_CONTENT_INVALID");
        await Req(customer,"POST","orders",Multipart(CreateBody(),type:"text/plain"),415,"PDF_TYPE_INVALID");
        check(data.Orders.Count==count&&data.Files.Count==0&&data.Assets.Count==0,"invalid/missing original PDFs never create partial orders or private files");
        var body=CreateBody();var id=(await Req(customer,"POST","orders",Multipart(body),201))["id"]!.ToString();
        await Req(customer,"POST","orders",Multipart(body));
        await Req(customer,"POST","orders",Multipart(body,WorkflowTestData.Pdf("different document")),409);
        var original=data.Files.Values.Single(f=>f.Str("order_id")==id);var originalId=original.Str("id");
        check(data.Files.Count==1&&original.Num("file_version")==1,"multipart create/replay stores one original attachment and immutable version");
        var customerFiles=await Req(customer,"GET","customer/orders/"+id);
        var administratorFiles=await Req(admin,"GET","admin/orders/"+id);
        check(customerFiles["files"]![0]!["created_at"]?.ToString()==original.Str("created_at")&&administratorFiles["files"]![0]!["created_at"]?.ToString()==original.Str("created_at"),"customer and administrator file history preserve the existing created_at API field");
        foreach(var (client,role,status) in new[]{(customer,"customer",200),(other,"customer",404),(guest,"customer",401),(admin,"customer",401),(admin,"admin",200),(customer,"admin",401)})
        foreach(var preview in new[]{false,true})
        {
            using var response=await client.GetAsync("/api/"+role+"/files/"+originalId+(preview?"?preview=true":""));
            check((int)response.StatusCode==status,$"private PDF {role} preview={preview} -> {status}");
            if(status!=200)continue;
            check(response.Content.Headers.ContentType?.MediaType=="application/pdf" && (await response.Content.ReadAsByteArrayAsync()).SequenceEqual(WorkflowTestData.Pdf()),"authorized private PDF returns the exact stored bytes");
            check(response.Headers.GetValues("Content-Security-Policy").Single()=="sandbox" && response.Headers.CacheControl?.NoStore==true &&
                response.Headers.GetValues("X-Content-Type-Options").Single()=="nosniff","private PDF download/preview remains sandboxed, uncached and non-sniffable");
            check(preview?response.Content.Headers.ContentDisposition==null:response.Content.Headers.ContentDisposition?.DispositionType=="attachment","PDF preview is inline while downloads retain their attachment filename");
        }
        data.FailNextNotification=true;var assetsBefore=data.Assets.Count;var ordersBefore=data.Orders.Count;
        await Req(customer,"POST","orders",Multipart(CreateBody()),409);
        check(data.Orders.Count==ordersBefore&&data.Assets.Count==assetsBefore&&data.Files.Count==1,"initial order rollback removes only its newly written PDF");
        await Req(admin,"POST","admin/orders/"+id+"/open",new{});
        check(data.Orders[id].Str("approved_file_id")=="","opening the order never reviews its file");
        await Req(admin,"POST","admin/orders/"+id+"/quotes",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),amountToman=200000,scopeText="Initial book",reviewedFileId=Guid.NewGuid().ToString()},409,"ORDER_APPROVED_FILE_INVALID");
        data.FailNextNotification=true;
        await Req(admin,"POST","admin/orders/"+id+"/quotes",SendQuote(id,200000),409);
        check(data.Orders[id].Str("approved_file_id")=="" && data.QuoteRows.Count==0,"failed quote notification rolls back financial proposal and file review together");
        await Req(admin,"POST","admin/orders/"+id+"/quotes",SendQuote(id,200000));
        check(data.Orders[id].Str("approved_file_id")==originalId && data.QuoteRows.Values.Single().Str("delivery_terms")=="","quote advancement records the primary file without financial delivery terms");
        await Accept(id);
        var savedBeforeDiscountChange=data.Orders[id].Str("spec");
        var savedEstimate=data.Orders[id].Num("estimate");
        await Req(admin,"PUT","admin/quantity-discount",new{active=true,threshold=0,percentage=85});
        check(savedBeforeDiscountChange==data.Orders[id].Str("spec") && data.Orders[id].Num("estimate")==savedEstimate && data.Orders[id].Num("final_price")==200000,
            "editing the discount leaves the persisted specification, estimate and accepted price intact");
        var acceptedBefore=data.QuoteRows.Values.Single(q=>q.Str("order_id")==id).ToDictionary();
        await Req(admin,"POST","admin/orders/"+id+"/corrections",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),message="Missing choice"},400,"CORRECTION_PDF_FLAG_REQUIRED");
        await Req(customer,"POST","admin/orders/"+id+"/corrections",Correction(id,true),401);
        var correction=Correction(id,true);await Task.WhenAll(Req(admin,"POST","admin/orders/"+id+"/corrections",correction),Req(admin,"POST","admin/orders/"+id+"/corrections",correction));
        var revision=data.CorrectionRows.Values.Single(c=>c.OrderId==id);
        check(revision.RequiresNewPdf&&revision.State=="requested"&&data.Orders[id].Str("approved_file_id")==""&&data.Orders[id].Str("status")=="review","correction request pauses the queue and clears production-file readiness once");
        check(data.NotificationRows.Values.Count(n=>n.Notice.Item.EventType=="order.information_requested"&&n.Notice.Item.EntityId==id)==1,"concurrent idempotent correction request produces one customer notification");
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",Start(id),409);
        await Req(admin,"PATCH","admin/orders/"+id,new{idempotencyKey=Guid.NewGuid().ToString(),version=Version(id),status="printing",finalPrice=200000},409);
        await Req(customer,"POST","customer/orders/"+id+"/corrections/submit",Submission(id,originalId),409,"CORRECTION_NEW_PDF_REQUIRED");
        await Req(customer,"POST","customer/orders/"+id+"/corrections/submit",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),message="Forged optional",requiresFile=false},400,"CORRECTION_PAYLOAD_INVALID");
        await Req(other,"POST","customer/orders/"+id+"/corrections/submit",Submission(id),404);
        await Upload(id,revision.Id,Encoding.ASCII.GetBytes("%PDF-fake"),expected:400);
        await Upload(id,revision.Id,client:other,expected:404);
        await Upload(id,Guid.NewGuid().ToString(),expected:409);
        var uploaded=await Upload(id,revision.Id);var correctedId=uploaded["id"]!.ToString();
        check(uploaded["revisionId"]!.ToString()==revision.Id&&uploaded["fileVersion"]!.GetValue<int>()==2,"new private PDF is versioned and bound to the current correction cycle");
        var submit=Submission(id,correctedId);await Req(customer,"POST","customer/orders/"+id+"/corrections/submit",submit);await Req(customer,"POST","customer/orders/"+id+"/corrections/submit",submit);
        check(data.CorrectionRows[revision.Id].State=="submitted"&&data.Orders[id].Str("information_request")!="","submission enters pending review and does not clear the production block");
        await Upload(id,revision.Id,expected:409);
        await Req(admin,"POST","admin/orders/"+id+"/quotes",SendQuote(id,220000),409,"ACCEPTED_PRICE_IMMUTABLE");
        await Req(admin,"POST","admin/orders/"+id+"/corrections/review",Review(id,"reject",""),400);
        await Req(admin,"POST","admin/orders/"+id+"/corrections/review",Review(id,"reject","Incorrect trim size"));
        var rejected=data.CorrectionRows[revision.Id];
        await Req(admin,"POST","admin/orders/"+id+"/corrections/review",Review(id),400);
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",Start(id),409);
        check(data.CorrectionRows[revision.Id]==rejected&&data.Files.Count==2,"rejected correction history and attachment versions cannot be overwritten");
        await Req(admin,"POST","admin/orders/"+id+"/corrections",Correction(id,false),400,"CORRECTION_PDF_FLAG_REQUIRED");
        await Req(admin,"POST","admin/orders/"+id+"/corrections",Correction(id,true));
        var second = data.CorrectionRows.Values.Where(c=>c.OrderId==id).MaxBy(c=>c.Revision)!;
        await Upload(id,second.Id);
        await Req(customer,"POST","customer/orders/"+id+"/corrections/submit",Submission(id));
        await Req(admin,"POST","admin/orders/"+id+"/corrections/review",Review(id));
        check(data.Orders[id].Str("status")=="confirmed" && data.Orders[id].Str("approved_file_id")!=correctedId,"second correction preserves the accepted price and reviews its own replacement");
        await Req(admin,"POST","admin/orders/"+id+"/quotes",SendQuote(id,250000),409,"ACCEPTED_PRICE_IMMUTABLE");
        check(acceptedBefore.All(p=>Equals(data.QuoteRows[acceptedBefore.Str("id")].GetValueOrDefault(p.Key),p.Value)),"rejecting a re-quote preserves the previously accepted revision unchanged");
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",Start(id),409);
        await Req(admin,"POST","admin/orders/"+id+"/corrections",Correction(id,true));
        await Req(customer,"POST","customer/orders/"+id+"/corrections/submit",Submission(id,correctedId),409,"CORRECTION_NEW_PDF_REQUIRED");
        var latest=data.CorrectionRows.Values.Where(c=>c.OrderId==id).MaxBy(c=>c.Revision)!;
        var newest=(await Upload(id,latest.Id))["id"]!.ToString();
        await Req(customer,"POST","customer/orders/"+id+"/corrections/submit",Submission(id,newest));
        data.FailNextEvent=true;var stateBefore=data.CorrectionRows[latest.Id];
        await Req(admin,"POST","admin/orders/"+id+"/corrections/review",Review(id),500);
        check(data.CorrectionRows[latest.Id]==stateBefore&&data.Orders[id].Str("approved_file_id")=="","failed review event persistence rolls back review, file selection and order state");
        var approved=Review(id);await Req(admin,"POST","admin/orders/"+id+"/corrections/review",approved);await Req(admin,"POST","admin/orders/"+id+"/corrections/review",approved);
        var approvedFile=data.Files[newest];var approvedBytes=data.Assets[approvedFile.Str("object_key")];data.Assets.Remove(approvedFile.Str("object_key"));
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",Start(id),409,"ORDER_FILE_UNAVAILABLE");
        data.Assets[approvedFile.Str("object_key")]=Encoding.ASCII.GetBytes("%PDF-fake");
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",Start(id),409,"PDF_CONTENT_INVALID");
        data.Assets[approvedFile.Str("object_key")]=approvedBytes;
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",Start(id),409,"FINAL_TERMS_REQUIRED");
        var termsId=Guid.NewGuid().ToString();
        await Req(admin,"POST","admin/orders/"+id+"/delivery-terms",new{idempotencyKey=termsId,expectedVersion=Version(id),delivery="Customer pickup",scope="Approved print run"});
        await Req(other,"POST","customer/orders/"+id+"/delivery-terms/request-amendment",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),termsId,reason="Different pickup time"},404);
        await Req(customer,"POST","customer/orders/"+id+"/delivery-terms/request-amendment",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),termsId,reason="Different pickup time"});
        await Req(customer,"POST","customer/orders/"+id+"/delivery-terms/accept",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),termsId},409,"FINAL_TERMS_STALE");
        await Req(admin,"POST","admin/orders/"+id+"/delivery-terms",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),delivery="Pickup",shippingCost=100},409,"ACCEPTED_PRICE_IMMUTABLE");
        var obsoleteTerms = termsId;
        termsId = Guid.NewGuid().ToString();
        await Req(admin,"POST","admin/orders/"+id+"/delivery-terms",new{idempotencyKey=termsId,expectedVersion=Version(id),delivery="Revised pickup time",scope="Approved print run"});
        await Req(customer,"POST","customer/orders/"+id+"/delivery-terms/accept",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),termsId=obsoleteTerms},409,"FINAL_TERMS_STALE");
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",Start(id),409,"FINAL_TERMS_REQUIRED");
        await Req(other,"POST","customer/orders/"+id+"/delivery-terms/accept",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),termsId},404);
        await Req(customer,"POST","customer/orders/"+id+"/delivery-terms/accept",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),termsId});
        var detail=await Req(admin,"GET","admin/orders/"+id);
        check(detail["productionReadiness"]!["canStart"]!.GetValue<bool>()&&detail["productionReadiness"]!["approvedFileId"]!.ToString()==newest,"only the latest approved attachment is the production source");
        var savedSpec = data.Orders[id].Str("spec");
        var changedSpec = JsonNode.Parse(savedSpec)!.AsObject();
        changedSpec["quantity"] = 18;
        data.Orders[id]["spec"] = changedSpec.ToJsonString();
        await Req(admin,"POST","admin/orders/"+id+"/start-printing",Start(id),409,"FINAL_TERMS_REQUIRED");
        await Req(admin,"POST","admin/orders/"+id+"/delivery-terms",new{idempotencyKey=Guid.NewGuid().ToString(),expectedVersion=Version(id),delivery="Collect"},409,"QUOTED_SPECIFICATION_CHANGED");
        data.Orders[id]["spec"] = savedSpec;
        var start=Start(id);await Task.WhenAll(Req(admin,"POST","admin/orders/"+id+"/start-printing",start),Req(admin,"POST","admin/orders/"+id+"/start-printing",start));
        await Req(admin,"POST","admin/orders/"+id+"/quotes",SendQuote(id,300000),409,"ACCEPTED_PRICE_IMMUTABLE");
        check(data.Events.Count(e=>e.Str("order_id")==id&&e.Str("status")=="printing")==1&&data.CorrectionRows.Values.Count(c=>c.OrderId==id)==3&&data.Files.Count==4,"concurrent production starts are idempotent and preserve all correction/file history");
        await Req(admin,"POST","admin/orders/"+id+"/corrections",Correction(id,false),400);

        var optional=(await Req(customer,"POST","orders",Multipart(CreateBody()),201))["id"]!.ToString();
        data.Orders[optional]["workflow_version"]=1;await Req(admin,"POST","admin/orders/"+optional+"/open",new{});
        await Req(admin,"POST","admin/orders/"+optional+"/corrections",Correction(optional,true));
        var blocked=await Req(admin,"PATCH","admin/orders/"+optional,new{status="printing",finalPrice=100,version=Version(optional)},409,"ORDER_CORRECTION_REQUIRED");
        await Upload(optional,data.CorrectionRows.Values.Where(c=>c.OrderId==optional).MaxBy(c=>c.Revision)!.Id);
        await Req(customer,"POST","customer/orders/"+optional+"/corrections/submit",Submission(optional));
        await Req(admin,"PATCH","admin/orders/"+optional,new{status="printing",finalPrice=100,version=Version(optional)},409,"ORDER_CORRECTION_REVIEW_REQUIRED");
        await Req(admin,"POST","admin/orders/"+optional+"/corrections/review",Review(optional,"reject","Please clarify the title"));
        await Req(admin,"PATCH","admin/orders/"+optional,new{status="printing",finalPrice=100,version=Version(optional)},409,"ORDER_CORRECTION_NOT_APPROVED");
        await Req(admin,"POST","admin/orders/"+optional+"/corrections",Correction(optional,true));
        await Upload(optional,data.CorrectionRows.Values.Where(c=>c.OrderId==optional).MaxBy(c=>c.Revision)!.Id);
        await Req(customer,"POST","customer/orders/"+optional+"/corrections/submit",Submission(optional));
        await Req(admin,"POST","admin/orders/"+optional+"/corrections/review",Review(optional));
        await Req(admin,"PATCH","admin/orders/"+optional,new{status="printing",finalPrice=100,version=Version(optional)},409,"CUSTOMER_APPROVAL_REQUIRED");
        check(data.Orders[optional].Str("status")!="printing","legacy PATCH cannot bypass price and final approval");
        var publicDetail=await Req(customer,"GET","customer/orders/"+id);
        check(!publicDetail.ToJsonString().Contains("object_key")&&!publicDetail.ToJsonString().Contains("reviewed_by_admin")&&!publicDetail.ToJsonString().Contains("submissionFingerprint"),"customer correction projections contain safe history and no storage/administrator identifiers");
        await Req(other,"GET","customer/orders/"+id,expected:404);
        var limited=(await Req(customer,"POST","orders",Multipart(CreateBody(service:"cover-design")),201))["id"]!.ToString();
        check(JsonNode.Parse(data.Orders[limited].Str("spec"))!["requiresPdf"]!.GetValue<bool>(),"all print services require a primary PDF");
        await Upload(limited,"",new byte[PdfFormat.MaximumBytes+1],expected:413);
        check(data.Files.Values.Count(f=>f.Str("order_id")==limited)==1,"oversize PDF leaves no attachment or partial storage record");
        for(var n=0;n<2;n++)await Upload(limited,"");
        await Task.WhenAll(Upload(limited,""),Upload(limited,""));
        await Upload(limited,"",expected:400);
        check(data.Files.Values.Count(f=>f.Str("order_id")==limited)==5,"serialized concurrent uploads preserve the five-file cap without overwriting versions");
        await Req(admin,"POST","admin/orders/"+limited+"/open",new{});

        var cycleCount=data.CorrectionRows.Count;data.FailNextNotification=true;
        await Req(admin,"POST","admin/orders/"+limited+"/corrections",Correction(limited,true),409);
        check(data.CorrectionRows.Count==cycleCount&&data.Orders[limited].Str("information_request")=="","failed notification persistence rolls back the correction request and readiness invalidation");
        await Req(admin,"POST","admin/orders/"+limited+"/corrections",Correction(limited,true));
        await Req(customer,"POST","customer/orders/"+limited+"/corrections/submit",Submission(limited,newest),409,"CORRECTION_FILE_INVALID");
        await Upload(limited,data.CorrectionRows.Values.Where(c=>c.OrderId==limited).MaxBy(c=>c.Revision)!.Id);
        await Req(customer,"POST","customer/orders/"+limited+"/corrections/submit",Submission(limited));
        await Req(admin,"PATCH","admin/orders/"+limited,new{idempotencyKey=Guid.NewGuid().ToString(),version=Version(limited),status="cancelled",note="Customer requested cancellation"});
        await Req(admin,"POST","admin/orders/"+limited+"/corrections/review",Review(limited),400);
        check(data.Orders[limited].Str("status")=="cancelled","reviewing an outstanding correction cannot resurrect a cancelled order");
        data.CorrectionsAvailable=false;await Req(admin,"POST","admin/orders/"+id+"/corrections",Correction(id,true),503,"ORDER_CORRECTIONS_UNAVAILABLE");
        check(host.Fixture.Sender.Sends==0,"correction API tests use only synthetic in-memory records and no SMS");
    }
}
