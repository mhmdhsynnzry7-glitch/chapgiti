using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ChapGiti.Application.Common;

namespace ChapGiti.SqlAcceptanceTests;

internal static class OrderConcurrencySqlScenarios
{
    public static async Task Run(OrderWorkflowApi api)
    {
        // Different Kestrel requests have different scoped SqlStore connections and real SQL locks.
        var id=await api.Ready();
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true));
        var submit=await api.UploadSubmission(id);var start=await api.Command(id);
        var responses=await Task.WhenAll(Send(api.Customer,"customer/orders/"+id+"/corrections/submit",submit),Send(api.Administrator,"admin/orders/"+id+"/start-printing",start));
        api.Checks.Check(responses.SequenceEqual(new[]{200,409}),"simultaneous SQL submission/start cannot print before administrator review");
        var review=await api.Review(id);start=await api.Command(id);
        responses=await Task.WhenAll(Send(api.Administrator,"admin/orders/"+id+"/corrections/review",review),Send(api.Administrator,"admin/orders/"+id+"/start-printing",start));
        api.Checks.Check(responses.SequenceEqual(new[]{200,409})&&(await api.Database.Read("SELECT status FROM dbo.Orders WHERE id=@p0",id))!.Str("status")=="confirmed","simultaneous SQL review/start honors the order version and requires a fresh start command after approval");
        var id2=await api.Ready();
        await api.Request(api.Administrator,"POST","admin/orders/"+id2+"/corrections",await api.Correction(id2,true));
        await api.Request(api.Customer,"POST","customer/orders/"+id2+"/corrections/submit",await api.UploadSubmission(id2));
        var approve=await api.Review(id2);var reject=await api.Review(id2,"reject","Conflicting review");
        responses=await Task.WhenAll(Send(api.Administrator,"admin/orders/"+id2+"/corrections/review",approve),Send(api.Administrator,"admin/orders/"+id2+"/corrections/review",reject));
        api.Checks.Check(responses.Count(s=>s==200)==1&&responses.Count(s=>s==409)==1,"simultaneous SQL approve/reject commands commit exactly one decision ("+string.Join(',',responses)+")");
        api.Checks.Check((await api.Database.Read("SELECT COUNT(*) n FROM dbo.Order_Corrections WHERE order_id=@p0 AND state IN('approved','rejected') AND version=3",id2))!.Num("n")==1,"SQL competing review preserves one immutable terminal correction version");
        for(var n=0;n<3;n++)
        {
            var race=await api.Ready();var correction=await api.Correction(race,true);start=await api.Command(race);
            responses=await Task.WhenAll(Send(api.Administrator,"admin/orders/"+race+"/corrections",correction),Send(api.Administrator,"admin/orders/"+race+"/start-printing",start));
            var state=(await api.Database.Read("SELECT status,(SELECT COUNT(*) FROM dbo.Order_Corrections c WHERE c.order_id=o.id AND c.state IN('requested','submitted','rejected')) unresolved FROM dbo.Orders o WHERE id=@p0",race))!;
            api.Checks.Check(responses.Count(s=>s==200)==1&&responses.Count(s=>s==409)==1&&!(state.Str("status")=="printing"&&state.Num("unresolved")>0),"real SQL correction/start race never commits printing with an unresolved cycle (iteration "+(n+1)+")");
        }
        var replay=await api.Ready();var body=await api.Correction(replay,true);
        await api.Request(api.Administrator,"POST","admin/orders/"+replay+"/corrections",body);
        var counts=await OrderWorkflowSqlScenarios.Snapshot(api.Database);var version=await api.Version(replay);
        var changed=(JsonObject)body.DeepClone();changed["reason"]="Changed reason for the same key";
        await api.Request(api.Administrator,"POST","admin/orders/"+replay+"/corrections",changed,409,"ORDER_COMMAND_CONFLICT");
        api.Checks.Check(await OrderWorkflowSqlScenarios.Snapshot(api.Database)==counts&&await api.Version(replay)==version,"SQL command receipt binds the payload and rejects conflicting reuse without side effects");
        await ApprovedProductionFile(api);
    }
    private static async Task ApprovedProductionFile(OrderWorkflowApi api)
    {
        var id=await api.Create(file:true);await api.Request(api.Administrator,"POST","admin/orders/"+id+"/open",new{});await api.Quote(id);await api.Accept(id);
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections",await api.Correction(id,true));
        var revision=(await api.Detail(id))["corrections"]!.AsArray().Last()!["id"]!.ToString();
        var selected=(await api.Upload(id,revision,OrderWorkflowFixtures.Pdf("approved production v2")))["id"]!.ToString();
        var newest=(await api.Upload(id,revision,OrderWorkflowFixtures.Pdf("not selected v3")))["id"]!.ToString();
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id,selected),409,"CORRECTION_FILE_STALE");
        await api.Request(api.Customer,"POST","customer/orders/"+id+"/corrections/submit",await api.Submission(id,newest));
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/corrections/review",await api.Review(id));
        await api.SignTerms(id);
        await api.Request(api.Administrator,"POST","admin/orders/"+id+"/start-printing",await api.Command(id));
        var row=(await api.Database.Read("SELECT status,approved_file_id FROM dbo.Orders WHERE id=@p0",id))!;
        api.Checks.Check(row.Str("status")=="printing"&&row.Str("approved_file_id")==newest&&selected!=newest,"actual SQL production requires the reviewed latest replacement and a fresh agreement");
    }
    private static async Task<int> Send(HttpClient client,string path,JsonObject body)
    {using var response=await client.PostAsJsonAsync("/api/"+path,body);return (int)response.StatusCode;}
}
