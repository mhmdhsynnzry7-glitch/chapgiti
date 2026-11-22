using System.Text.Json.Nodes;
using ChapGiti.Domain.Common;
using ChapGiti.Domain.Printing;

namespace ChapGiti.AuthenticationTests;

internal static class PricingScenarios
{
    public static void Run(Action<bool,string> check)
    {
        var root=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data","defaults.json")))!.AsObject();
        var pricing=new PrintPricing(new PrintCatalog(root["options"]!.AsObject()));
        check(!JsonValue.Create(120)!.TryGetValue<double>(out _), "regression reproduction: the former double-only parser rejects an integer-backed JsonValue");
        JsonObject Spec(JsonNode? pages) => new(){["pages"]=pages,["quantity"]=100,["size"]="raghi",["paper"]="white",["color"]="bw",["binding"]="soft"};
        foreach (var quantity in new[] {49,50,51,100})
        {
            var input = Spec(JsonValue.Create(120)); input["quantity"] = quantity;
            var quote = pricing.Quote(pricing.Validate(input), root["rates"]!);
            var eligible = Input.Numeric(quote["subtotal"]);
            check(Input.Numeric(quote["discountPercent"]) == (quantity > 50 ? 10 : 0) &&
                Input.Numeric(quote["discountAmount"]) == (quantity > 50 ? Math.Floor(eligible / 10) : 0), "exclusive threshold, one discount at " + quantity);
            var alternate = pricing.Quote(pricing.Validate(input), root["rates"]!, new(true, 10, 25));
            check(Input.Numeric(alternate["discountAmount"]) == Math.Floor(eligible / 4), "administrator override " + quantity);
            check(Input.Numeric(pricing.Quote(pricing.Validate(input), root["rates"]!, new(false, 0, 100))["discountAmount"]) == 0, "inactive policy " + quantity);
        }
        foreach(var value in new JsonNode?[]{JsonValue.Create(120),JsonValue.Create(120L),JsonValue.Create(120d),JsonValue.Create("120"),JsonValue.Create("۱۲۰"),JsonValue.Create("١٢٠"),JsonValue.Create(" 120 ")})
        {
            var spec=pricing.Validate(Spec(value));
            var quote=pricing.Quote(spec,root["rates"]!);
            check(Input.Number(spec,"pages",4,2000)==120&&Input.Numeric(quote["total"])==3792000,
                "equivalent typed/serialized/numeric-string page counts retain the same authoritative price: "+value);
        }
        foreach(var n in new[]{4,2000})check(Input.Number(pricing.Validate(Spec(JsonValue.Create(n))),"pages",4,2000)==n,"inclusive page-count boundary "+n);
        foreach(var value in new JsonNode?[]{null,JsonValue.Create(""),JsonValue.Create("   "),JsonValue.Create(3),JsonValue.Create(2001),JsonValue.Create(4.5),JsonValue.Create("۴.۵"),JsonValue.Create("1e2"),JsonValue.Create(true),new JsonArray(120),new JsonObject()})
        {
            try{pricing.Validate(Spec(value));throw new InvalidOperationException("Invalid page count accepted.");}
            catch(RuleViolation){check(true,"invalid page count rejected: "+(value?.ToJsonString()??"null"));}
        }
        var detailed=Spec(JsonValue.Create("۱۲۰"));
        detailed["details"]=JsonNode.Parse("""{"width":14.5,"height":21.5,"printType":"bw-text","sides":"double","bwPages":"١٢٠","colorPages":"۰","bwPaper":"white70","colorPaper":"white70","printOnly":false,"bindingStyle":"hot-glue","coverPrint":"front","coverPaper":"coated250","coating":"gloss","orientation":"portrait","readingDirection":"rtl","extras":[]}""");
        var normalized=pricing.Validate(detailed);
        check(Input.Number(normalized["details"]!.AsObject(),"bwPages",0,2000)==120,"detailed Persian/Arabic page counts are normalized before consistency checks");
        detailed["details"]!["colorPages"]=1;
        try{pricing.Validate(detailed);throw new InvalidOperationException("Mismatched page sum accepted.");}
        catch(RuleViolation){check(true,"page-sum consistency remains enforced");}
        detailed["details"]!["colorPages"]=0;detailed["details"]!["extras"]=new JsonArray("goldFoil");
        try{pricing.Validate(detailed);throw new InvalidOperationException("Invalid finishing accepted.");}
        catch(RuleViolation){check(true,"existing finishing restrictions remain enforced");}
    }
}
