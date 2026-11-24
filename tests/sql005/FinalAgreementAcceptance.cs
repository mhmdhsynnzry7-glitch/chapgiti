using System.Net.Http.Json;
using System.Text.Json.Nodes;
namespace ChapGiti.SqlAcceptanceTests;

internal static class FinalAgreementAcceptance
{
    public static async Task Run(AcceptanceChecks checks, AcceptanceHost host, HttpClient admin, HttpClient customer, string cutId)
    {
        using var second = host.Client();
        await checks.Request(second,"POST","auth/login",new{username="admin",password=OrderWorkflowFixtures.Password});
        var tariffPath="admin/print-tariffs/"+cutId;
        var tariff=await checks.Request(admin,"GET",tariffPath);
        if(tariff["isProposed"]!.GetValue<bool>())
            await checks.Request(admin,"PUT",tariffPath,new{version=tariff["version"],rates=tariff["rates"],approveProposal=true});
        var payload=OrderWorkflowApi.Creation("book-printing");
        payload["termsAccepted"]=true;
        using var form=OrderWorkflowApi.Multipart(payload,OrderWorkflowFixtures.Pdf());
        var previous=await checks.Request(admin,"GET",tariffPath);
        var previousVersion=previous["version"]!.GetValue<int>();
        var originalRates=previous["rates"]!.AsObject();
        var changedRates=originalRates.DeepClone().AsObject();
        changedRates["setup"]=originalRates["setup"]!.GetValue<long>()+1000;
        var orderRequest=customer.PostAsync("/api/orders",form);
        var tariffEdit=checks.Request(second,"PUT",tariffPath,new{version=previousVersion,rates=changedRates});
        await Task.WhenAll(orderRequest,tariffEdit);
        using var created=await orderRequest;
        checks.Check((int)created.StatusCode==201,"real SQL accepts a new print request and primary PDF");
        var id=(await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.ToString();
        var adminPath="admin/orders/"+id;
        var userPath="customer/orders/"+id;
        var opened=await checks.Request(admin,"POST",adminPath+"/open",new{});
        var spec=opened["order"]!["spec"]!;
        if(spec is JsonValue) spec=JsonNode.Parse(spec.ToString())!;
        var estimate=spec["priceBreakdown"]!;
        var snapshot=estimate["tariffSnapshot"]!;
        var version=snapshot["version"]!.GetValue<int>();
        var savedRates=snapshot["rates"]!;
        var expected=version==previousVersion?originalRates:changedRates;
        checks.Check((version==previousVersion || version==previousVersion+1) &&
            version==estimate["tariffVersion"]!.GetValue<int>() &&
            expected.All(pair=>JsonNode.DeepEquals(savedRates[pair.Key],pair.Value)),
            "concurrent SQL tariff edit and order submission persist a complete single-version estimate");
        var file=opened["files"]![0]!["id"]!.ToString();
        async Task<long> Version()=>(await checks.Request(admin,"GET",adminPath))["order"]!["version"]!.GetValue<long>();
        var quote=new{idempotencyKey=Guid.NewGuid(),expectedVersion=await Version(),amountToman=250000,scopeText="چاپ نمونه آزمایشی",reviewedFileId=file};
        await checks.Request(admin,"POST",adminPath+"/quotes",quote);
        var quoted=await checks.Request(customer,"GET",userPath);
        var last=quoted["quotes"]!.AsArray().Last()!;
        await checks.Request(customer,"POST",userPath+"/quote/accept",new{idempotencyKey=Guid.NewGuid(),expectedVersion=await Version(),quoteId=last["id"],quoteVersion=last["version"]});
        var terms=Guid.NewGuid().ToString();
        var sent=new{idempotencyKey=terms,expectedVersion=await Version(),delivery="تحویل حضوری",reviewedFileId=file};
        await Task.WhenAll(checks.Request(admin,"POST",adminPath+"/delivery-terms",sent),checks.Request(second,"POST",adminPath+"/delivery-terms",sent));
        var readVersion=await Version();
        var accepted=new{idempotencyKey=Guid.NewGuid(),expectedVersion=readVersion,termsId=terms};
        await checks.Request(customer,"POST",userPath+"/delivery-terms/accept",accepted);
        var stale=await checks.Request(second,"POST",adminPath+"/delivery-terms",new{idempotencyKey=Guid.NewGuid(),expectedVersion=readVersion,revisionOf=terms,delivery="محل دوم"},409);
        checks.Check(stale["code"]!.ToString()=="ORDER_VERSION_CONFLICT","stale independent administrator cannot overwrite customer consent");
        var revision=Guid.NewGuid().ToString();
        var changed=new{idempotencyKey=revision,expectedVersion=await Version(),revisionOf=terms,delivery="تحویل در محل دوم",reviewedFileId=file};
        await checks.Request(second,"POST",adminPath+"/delivery-terms",changed);
        await checks.Request(second,"POST",adminPath+"/delivery-terms",changed);
        await checks.Request(admin,"POST",adminPath+"/start-printing",new{idempotencyKey=Guid.NewGuid(),expectedVersion=await Version()},409);
        await checks.Request(customer,"POST",userPath+"/delivery-terms/accept",new{idempotencyKey=Guid.NewGuid(),expectedVersion=await Version(),termsId=revision});
        await checks.Request(admin,"POST",adminPath+"/start-printing",new{idempotencyKey=Guid.NewGuid(),expectedVersion=await Version()});
        var final=await checks.Request(customer,"GET",userPath);
        checks.Check(final["order"]!["status"]!.ToString()=="printing" && final["finalConditions"]!["revision"]!.GetValue<int>()==2,
            "new SQL agreement needs fresh consent before production and retries create exactly two revisions");
    }
}
