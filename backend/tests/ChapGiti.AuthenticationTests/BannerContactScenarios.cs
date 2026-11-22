using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ChapGiti.AuthenticationTests;

internal static class BannerContactScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var host=await TestApiHost.Start();
        using var client=new HttpClient(new HttpClientHandler{UseCookies=true}){BaseAddress=new Uri(host.Origin)};
        client.DefaultRequestHeaders.Add("Origin",host.Origin);
        async Task<JsonNode> Send(string method,string route,object? body=null,int status=200){
            using var request=new HttpRequestMessage(new HttpMethod(method),"/api/"+route);if(body!=null)request.Content=JsonContent.Create(body);
            using var response=await client.SendAsync(request);check((int)response.StatusCode==status,"banner/contact HTTP "+method+" "+route+" -> "+status);
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        }
        await Send("POST","auth/login",new{username="admin",password=AuthFixture.Password});
        var banners=(await Send("GET","admin/banners")).AsArray();var existing=banners.ToJsonString();
        var banner=banners[0]!.DeepClone().AsObject();banner["id"]="no-cta-test";banner["title"]="A banner without a button";banner["label"]="";banner["link"]="";banners.Insert(0,banner);
        await Send("PUT","admin/banners",banners);
        var publicContent=await Send("GET","public/bootstrap");
        check(publicContent["banners"]![0]!["label"]!.ToString()==""&&publicContent["banners"]![0]!["link"]!.ToString()=="","banner contract supports no CTA without placeholder values");
        foreach(var pair in new[]{("Label",""),("","/services"),("Label","javascript:bad"),("Label","//evil.example")}){
            var bad=banners.DeepClone().AsArray();bad[0]!["label"]=pair.Item1;bad[0]!["link"]=pair.Item2;await Send("PUT","admin/banners",bad,400);
        }
        var savedBanners=(await Send("GET","admin/banners")).AsArray();savedBanners.RemoveAt(0);
        var original=JsonNode.Parse(existing)!.AsArray();
        check(original.Count==savedBanners.Count&&original.Select((item,index)=>item!.AsObject().All(field=>JsonNode.DeepEquals(field.Value,savedBanners[index]![field.Key]))).All(same=>same),"optional CTA writes preserve every existing banner field");
        var settings=await Send("GET","admin/settings");var rates=settings["rates"]!.ToJsonString();var site=settings["site"]!.DeepClone().AsObject();
        var core=string.Join("|",new[]{"brand","phone","email","address","hours"}.Select(k=>site[k]?.ToString()));
        site["instagram"]="https://www.instagram.com/cms_giti_demo";site["telegram"]="https://t.me/cms_giti_demo";site["bale"]="https://ble.ir/cms_giti_demo";
        await Send("PUT","admin/settings",new{site,rates=settings["rates"]!.DeepClone()});
        var stored=await Send("GET","admin/settings");
        check(stored["site"]!["bale"]!.ToString()=="https://ble.ir/cms_giti_demo"&&stored["rates"]!.ToJsonString()==rates,"social links persist independently without tariff changes");
        check(string.Join("|",new[]{"brand","phone","email","address","hours"}.Select(k=>stored["site"]![k]?.ToString()))==core,"social settings preserve phone/address/hours and existing identity");
        var legacy=site.DeepClone().AsObject();foreach(var key in new[]{"instagram","telegram","bale"})legacy.Remove(key);
        await Send("PUT","admin/settings",new{site=legacy,rates=settings["rates"]!.DeepClone()});
        check((await Send("GET","admin/settings"))["site"]!["telegram"]!.ToString()==site["telegram"]!.ToString(),"older settings clients cannot erase omitted social links");
        foreach(var invalid in new[]{"http://t.me/test","javascript:alert(1)","https://t.me.evil.example/test","https://user:secret@t.me/test","https://t.me/test?url=evil","https://t.me/test#fragment","https://t.me:444/test","https://t.me/a%2fb"}){
            var bad=site.DeepClone().AsObject();bad["telegram"]=invalid;await Send("PUT","admin/settings",new{site=bad,rates=settings["rates"]!.DeepClone()},400);
        }
        var wrongPlatform=site.DeepClone().AsObject();wrongPlatform["bale"]="https://t.me/test";await Send("PUT","admin/settings",new{site=wrongPlatform,rates=settings["rates"]!.DeepClone()},400);
        var cleared=site.DeepClone().AsObject();cleared["bale"]="";await Send("PUT","admin/settings",new{site=cleared,rates=settings["rates"]!.DeepClone()});
        check((await Send("GET","public/bootstrap"))["site"]!["bale"]!.ToString()=="","explicitly clearing a social link removes only that configured value");
        check(host.Fixture.Sender.Sends==0,"banner/contact tests never invoke real SMS");
    }
}
