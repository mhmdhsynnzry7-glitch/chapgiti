using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChapGiti.Domain.Common;
using ChapGiti.Domain.Content;
using ChapGiti.Domain.Printing;

namespace ChapGiti.AuthenticationTests;

internal static class CatalogMagazineScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host=await TestApiHost.Start();
        using var client=new HttpClient(new HttpClientHandler{UseCookies=true}){BaseAddress=new Uri(host.Origin)};client.DefaultRequestHeaders.Add("Origin",host.Origin);
        async Task<JsonNode> Send(string method,string route,object? body=null,int status=200,string? code=null)
        {
            using var request=new HttpRequestMessage(new HttpMethod(method),"/api/"+route);if(body!=null)request.Content=JsonContent.Create(body);
            using var response=await client.SendAsync(request);var json=JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            check((int)response.StatusCode==status,"catalog/magazine "+method+" "+route+" -> "+status);
            if(code!=null)check(json["code"]?.ToString()==code,"structured failure "+code);return json;
        }
        await Send("GET","admin/print-catalog",status:401);await Send("GET","admin/articles",status:401);
        await Send("POST","auth/login",new{username="admin",password=AuthFixture.Password});
        var catalog=await Send("GET","public/print-catalog");check(catalog["items"]!.AsArray().Count==80,"all existing catalogs retain the same business meanings");
        var format=catalog["items"]!.AsArray().First(n=>n!["kind"]!.ToString()=="bookSizes"&&n["code"]!.ToString()=="vaziri")!.DeepClone().AsObject();
        format["label"]="نام نمایشی ویرایش‌شده";await Send("PUT","admin/print-catalog/"+format["id"],format);await Send("PUT","admin/print-catalog/"+format["id"],format,409,"CATALOG_VERSION_CONFLICT");
        var added=format.DeepClone().AsObject();added["code"]="supported-new-size";added["label"]="قطع تازه معتبر";added["active"]=true;added["metadata"]!["width"]=18;added["metadata"]!["height"]=25;added["metadata"]!["factor"]=1.3;
        var created=await Send("POST","admin/print-catalog",added,201);check(host.Data.CatalogItems[created["id"]!.ToString()].Metadata["width"]!.GetValue<int>()==18,"new standard dimensions persist without a code/name lookup");
        await Send("POST","admin/print-catalog",added,409,"CATALOG_CODE_EXISTS");
        var unsupported=added.DeepClone().AsObject();unsupported["code"]="unpriced";unsupported["pricingRule"]="unknown-pricing";await Send("POST","admin/print-catalog",unsupported,400);
        var mutated=(await Send("GET","admin/print-catalog"))["items"]!.AsArray().First(n=>n!["id"]!.ToString()==format["id"]!.ToString())!.DeepClone().AsObject();mutated["code"]="rename-fixed-code";await Send("PUT","admin/print-catalog/"+mutated["id"],mutated,400,"CATALOG_CODE_IMMUTABLE");
        var def=(await Send("GET","admin/print-catalog"))["items"]!.AsArray().First(n=>n!["kind"]!.ToString()=="bookSizes"&&n["isDefault"]!.GetValue<bool>())!.DeepClone().AsObject();def["active"]=false;await Send("PUT","admin/print-catalog/"+def["id"],def,400,"CATALOG_DEFAULT_INACTIVE");
        var spec=new{pages=120,quantity=100,size="raghi",paper="white",color="bw",binding="soft"};
        await Send("POST","quote",spec,401);
        await Send("POST","customer/login",new{phone=AuthFixture.Phone,password=AuthFixture.Password});
        var quote=await Send("POST","quote",spec);check(quote["total"]!.GetValue<long>()==3792000,"default authoritative price includes the current nonstacking quantity discount");
        await Send("PUT","admin/quantity-discount",new {active=true,threshold=50,percentage=101},400);
        await Send("PUT","admin/quantity-discount",new {active=true,threshold=50,percentage=25});
        var adjusted = await Send("POST","quote",spec);
        check(adjusted["discountPercent"]!.GetValue<int>()==25 && quote["total"]!.GetValue<long>()==3792000,"later policy changes do not rewrite a previous estimate");
        var removable = added.DeepClone().AsObject(); removable["code"]="temporary-option";
        var removalId=(await Send("POST","admin/print-catalog",removable,201))["id"]!.ToString();
        var syntheticId=Guid.NewGuid().ToString();
        host.Data.Orders[syntheticId]=new(){["id"]=syntheticId,["spec"]=new JsonObject{["catalogSnapshot"]=new JsonObject{["options"]=new JsonArray(new JsonObject{["id"]=removalId})}}.ToJsonString()};
        await Send("DELETE","admin/print-catalog/"+removalId+"?version=1",status:409,code:"CATALOG_DELETE_PROTECTED");
        host.Data.Orders.Remove(syntheticId);
        await Send("DELETE","admin/print-catalog/"+removalId+"?version=1");
        await Send("DELETE","admin/print-catalog/"+def["id"]+"?version=1",status:409);
        await Send("POST","quote",spec with{size="disabled-or-unknown"},400,"CATALOG_SELECTION_UNAVAILABLE");
        var badIds=JsonSerializer.SerializeToNode(spec)!.AsObject();badIds["catalogIds"]=new JsonObject{["bookSizes:raghi"]=12};await Send("POST","quote",badIds,400,"CATALOG_SELECTION_INVALID");
        host.Data.CatalogAvailable=false;await Send("POST","quote",spec,503,"PRINT_CATALOG_UNAVAILABLE");host.Data.CatalogAvailable=true;
        var emptyCut=created["id"]!.ToString();
        var newCutSpec=JsonSerializer.SerializeToNode(spec)!.AsObject();
        newCutSpec["size"]="supported-new-size";
        newCutSpec["details"]=JsonNode.Parse("""{"width":18,"height":25,"printType":"bw-text","sides":"double","bwPages":120,"colorPages":0,"bwPaper":"white70","colorPaper":"white70","printOnly":false,"bindingStyle":"hot-glue","coverPrint":"front","coverPaper":"coated250","coating":"gloss","orientation":"portrait","readingDirection":"rtl","extras":[]}""");
        await Send("POST","quote",newCutSpec,409,"CUT_TARIFF_NOT_CONFIGURED");
        var unset=await Send("GET","admin/print-tariffs/"+emptyCut);
        check(unset["version"]!.GetValue<int>()==0 && unset["rates"]==null,"new cut starts without invented prices");
        var raghi=host.Data.CatalogItems.Values.Single(i=>i.Kind=="bookSizes" && i.Code=="raghi");
        var original=await Send("GET","admin/print-tariffs/"+raghi.Id);
        var prices=original["rates"]!.DeepClone().AsObject();
        prices["bw"]=prices["bw"]!.GetValue<int>()+17;
        await Send("PUT","admin/print-tariffs/"+emptyCut,new{version=0,rates=prices});
        await Send("PUT","admin/print-tariffs/"+emptyCut,new{version=0,rates=prices},409,"TARIFF_CONFLICT");
        var newer=await Send("POST","quote",newCutSpec);
        check(newer["total"]!.GetValue<long>()>adjusted["total"]!.GetValue<long>() && newer["tariffVersion"]!.GetValue<int>()==1,"selected cut uses its own explicit version regardless of catalog size factor");
        prices["bw"]=-1;await Send("PUT","admin/print-tariffs/"+emptyCut,new{version=1,rates=prices},400);
        prices.Remove("setup");await Send("PUT","admin/print-tariffs/"+emptyCut,new{version=1,rates=prices},400);
        foreach(var (code,increase) in new[]{("vaziri",27),("a4",49)})
        {
            var selected=host.Data.CatalogItems.Values.Single(x=>x.Kind=="bookSizes" && x.Code==code);
            var current=await Send("GET","admin/print-tariffs/"+selected.Id);
            var localRates=current["rates"]!.DeepClone().AsObject();
            localRates["bw"]=original["rates"]!["bw"]!.GetValue<int>()+increase;
            await Send("PUT","admin/print-tariffs/"+selected.Id,new{version=1,rates=localRates});
            var comparison=await Send("POST","quote",spec with {size=code});
            check(comparison["subtotal"]!.GetValue<long>()-adjusted["subtotal"]!.GetValue<long>()==increase*spec.pages*spec.quantity,"exact per-cut unit for "+code+" without a second size multiplier");
        }
        var valid=host.Data.CatalogItems.Values.First(i=>i.Kind=="bookSizes");var metadata=valid.Metadata.DeepClone().AsObject();metadata["width"]=20;
        check(PrintCatalogRules.Validate(valid.Kind,valid.Code,valid.Label,valid.PricingRule,metadata)["width"]!.GetValue<int>()==20,"domain catalog validation accepts typed numeric JsonValues");
        check(ChapGiti.Infrastructure.Persistence.Ef.EfRows.Stamp(new DateTime(2026,11,17,12,30,0,DateTimeKind.Unspecified))=="2026-11-17T12:30:00.000Z","SQL datetime2 UTC timestamps do not drift through the local timezone");
        void Invalid(Action action,string name){try{action();throw new InvalidOperationException("Invalid domain operation accepted.");}catch(RuleViolation){check(true,name);}}
        metadata["unit"]="inch";Invalid(()=>PrintCatalogRules.Validate(valid.Kind,valid.Code,valid.Label,valid.PricingRule,metadata),"unsupported measurement conversion cannot silently change formulas");
        Invalid(()=>ArticlePublication.Transition(null,"published"),"new article must start as a draft");Invalid(()=>ArticlePublication.Transition("archived","published"),"archived article requires an explicit return to draft");
        foreach(var text in new[]{"<script>alert(1)</script>","[unsafe](javascript:alert)","![external](https://external.invalid/image.png)"})Invalid(()=>ArticlePublication.ValidateContent(text),"unsafe markdown/HTML is rejected");
        ArticlePublication.ValidateContent("## Heading\n\n**Safe** text and [link](https://example.test).");check(true,"defined markdown remains available without raw HTML");
        var article=new JsonObject{["slug"]="new-magazine-guide",["title"]="راهنمای آزمایشی چاپ",["excerpt"]="خلاصه مقاله آزمایشی برای بررسی مجله.",["content"]="## عنوان بخش\n\nمتن آزمایشی کافی برای انتشار مقاله و بررسی حریم خصوصی پیش‌نویس.",["category"]="چاپ",["status"]="draft",["coverAssetId"]="",["coverAlt"]="",["authorName"]="",["seoTitle"]="",["seoDescription"]=""};
        host.Data.FailNextMagazineSave=true;await Send("POST","admin/articles",article,409);check(host.Data.MagazineArticles.Count==0,"failed first article creates no partial content or fake seeded guides");
        var saved=await Send("POST","admin/articles",article,201);var id=saved["id"]!.ToString();await Send("GET","public/articles/new-magazine-guide",status:404);
        check((await Send("GET","public/articles"))["total"]!.GetValue<long>()==0,"public list excludes drafts");
        var edit=(await Send("GET","admin/articles/"+id)).AsObject();edit["status"]="published";await Send("PUT","admin/articles/"+id,edit);await Send("PUT","admin/articles/"+id,edit,409,"ARTICLE_VERSION_CONFLICT");
        var detail=await Send("GET","public/articles/new-magazine-guide");var published=detail["article"]!["publishedAt"]!.ToString();check(detail["article"]!["authorName"]==null,"unknown public authors are not invented");
        var list=await Send("GET","public/articles");check(list["items"]!.AsArray().Count==1&&list["items"]![0]!["content"]==null,"public cards project summaries without article bodies");
        edit=(await Send("GET","admin/articles/"+id)).AsObject();edit["title"]="عنوان ویرایش‌شده";await Send("PUT","admin/articles/"+id,edit);check(host.Data.MagazineArticles[id].Slug=="new-magazine-guide"&&host.Data.MagazineArticles[id].PublishedAt==published,"title changes preserve slug and first publication date");
        edit=(await Send("GET","admin/articles/"+id)).AsObject();edit["slug"]="changed-magazine-guide";await Send("PUT","admin/articles/"+id,edit);check((await Send("GET","public/articles/new-magazine-guide"))["isAlias"]!.GetValue<bool>(),"explicit slug changes create a published-only canonical alias");
        var other=article.DeepClone().AsObject();other["slug"]="new-magazine-guide";await Send("POST","admin/articles",other,409,"ARTICLE_SLUG_EXISTS");
        edit=(await Send("GET","admin/articles/"+id)).AsObject();edit["status"]="archived";await Send("PUT","admin/articles/"+id,edit);await Send("GET","public/articles/changed-magazine-guide",status:404);await Send("GET","public/articles/new-magazine-guide",status:404);
        check((await Send("GET","public/magazine/sitemap")).AsArray().Count==0,"sitemap excludes archived and unpublished content");
        await Send("POST","admin/seed");check(host.Data.MagazineArticles.Count==1,"legacy seed endpoint never generates production articles");
        check(host.Data.MagazineArticles[id].Content==article["content"]!.ToString()&&host.Data.MagazineArticles[id].PublishedAt==published,"archive preserves original article body and recorded publication time");
        check(host.Fixture.Sender.Sends==0,"catalog/magazine tests send no real or simulated OTP");
        var prepared=await Send("POST","admin/articles/prepare-drafts");
        check(prepared["inserted"]!.GetValue<int>()==5 && host.Data.MagazineArticles.Values.Count(a=>a.Status=="draft" && a.PublishedAt==null)==5,"five complete editorial guides are prepared exclusively as drafts");
        var guide=host.Data.MagazineArticles.Values.Single(a=>a.Slug=="choose-book-trim");
        foreach(var draft in host.Data.MagazineArticles.Values.Where(a=>a.Status=="draft"))
            check(draft.Content.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length>170 && draft.Content.Contains("## ") && draft.Content.Contains("### ") && draft.Content.Contains("](/") && draft.SeoTitle.Length>4 && draft.SeoDescription.Length is >20 and <=180,"complete Persian editorial guide with headings, internal links and SEO: "+draft.Slug);
        var editorCopy=(await Send("GET","admin/articles/"+guide.Id)).AsObject();editorCopy["slug"]="custom-trim-guide";editorCopy["title"]="عنوان منتخب ویراستار";
        await Send("PUT","admin/articles/"+guide.Id,editorCopy);
        var repeated=await Send("POST","admin/articles/prepare-drafts");
        check(repeated["skipped"]!.GetValue<int>()==5 && host.Data.MagazineArticles[guide.Id].Title=="عنوان منتخب ویراستار","idempotent preparation preserves edited titles, slugs and historical aliases");
        await Send("GET","public/articles/custom-trim-guide",status:404);
        var namedSitemap=article.DeepClone().AsObject();namedSitemap["slug"]="sitemap";
        var sitemapSaved=await Send("POST","admin/articles",namedSitemap,201);var sitemapId=sitemapSaved["id"]!.ToString();
        var sitemapDraft=(await Send("GET","admin/articles/"+sitemapId)).AsObject();sitemapDraft["status"]="published";await Send("PUT","admin/articles/"+sitemapId,sitemapDraft);
        check((await Send("GET","public/articles/sitemap"))["article"]!["slug"]!.ToString()=="sitemap","the valid historical slug sitemap cannot collide with metadata routing");
        check((await Send("GET","public/magazine/sitemap")).AsArray().Count==1,"separate sitemap projection still returns only published article metadata");
    }
}
