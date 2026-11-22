namespace ChapGiti.Application.Features;

public sealed class AdminContentService(
    IAdminContentRepository repository,
    IUnitOfWork unitOfWork,
    SessionService sessions,
    ContentService content,
    IContentDefaults defaults,
    IPrivateFileStore files,
    ISmsNotifier sms)
{
    public async Task<UseCaseResult> Settings()
    {
        return UseCaseResult.Json(new
        {
            site = await content.Read("site", "settings"),
            rates = await content.StoredRates() ?? new JsonObject(),
            orderRequirements = await content.OrderRequirements(),
            sms = sms.Availability
        });
    }

    public async Task<UseCaseResult> UpdateSettings(JsonObject body, Dictionary<string, object?> user)
    {
        if (body["finishingRates"] is JsonObject finishingOnly && !body.ContainsKey("site"))
        {
            var values=ValidatedRates(finishingOnly, finishingOnly:true);
            await unitOfWork.Transaction(async () =>
            {
                await MergeRates(values);
                await sessions.Audit(user.Str("email"), "ویرایش تعرفه‌های خدمات تکمیلی");
                return true;
            });
            return UseCaseResult.Json(new { ok=true });
        }
        var site = Input.Object(body["site"]);
        var rates = body["rates"] as JsonObject;
        var finishing = body["finishingRates"] as JsonObject;
        var saved = new JsonObject();
        foreach (var field in new[] { "brand", "tagline", "phone", "email", "address", "hours", "seoTitle", "seoDescription" })
        {
            var min = field == "brand" ? 2 : field == "seoTitle" ? 5 : field == "seoDescription" ? 20 : 0;
            var max = field switch
            {
                "brand" => 80,
                "tagline" => 120,
                "phone" => 30,
                "email" => 254,
                "address" => 300,
                "hours" => 100,
                "seoTitle" => 100,
                _ => 180
            };
            saved[field] = Input.Text(site, field, min, max);
        }
        if (saved["email"]!.GetValue<string>() != "" && !Regex.IsMatch(saved["email"]!.GetValue<string>(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            throw new UseCaseException(ErrorKind.Invalid, "ایمیل معتبر نیست.");
        var prices=finishing!=null ? ValidatedRates(finishing,true) : rates!=null ? ValidatedRates(rates,false) : new JsonObject();
        var orderRequirements = body.ContainsKey("orderRequirements") ? OrderSubmissionPolicy.Validate(body["orderRequirements"]) : null;
        await unitOfWork.Transaction(async () =>
        {
            await repository.LockSetting("site");
            var existing = (await content.Read("site", "settings")).AsObject();
            foreach (var platform in new[] { "instagram", "telegram", "bale" })
                saved[platform] = site.ContainsKey(platform)
                    ? ContactLinks.Validate(platform, Input.Text(site, platform, 0, 500))
                    : existing[platform]?.DeepClone();
            await content.Save("site", saved);
            if (prices.Count > 0) await MergeRates(prices);
            if (orderRequirements != null) await content.Save("orderRequirements", orderRequirements);
            return true;
        });
        await sessions.Audit(user.Str("email"), "ویرایش تنظیمات و تعرفه‌ها");
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    private JsonObject ValidatedRates(JsonObject input,bool finishingOnly)
    {
        var result=new JsonObject();
        foreach (var key in defaults.Root["rates"]!.AsObject().Select(item=>item.Key).Where(k=>!finishingOnly || !CutTariff.Keys.Contains(k)))
        {
            try { result[key]=Input.Number(input,key,0,10_000_000); }
            catch (RuleViolation) { throw new UseCaseException(ErrorKind.Invalid,"تعرفه‌ها باید عدد صحیح بین صفر و ۱۰٬۰۰۰٬۰۰۰ تومان باشند.",code:"TARIFF_INVALID"); }
        }
        return result;
    }

    private async Task MergeRates(JsonObject values)
    {
        await repository.LockSetting("rates");
        var stored=await content.StoredRates() ?? new JsonObject();
        foreach (var (key,value) in values)
        {
            if (CutTariff.Keys.Contains(key))
            {
                if (stored[key]!=null && !JsonNode.DeepEquals(stored[key],value))
                    throw new UseCaseException(ErrorKind.Conflict,"برای تغییر نرخ چاپ ابتدا قطع کتاب را انتخاب کنید.",code:"CUT_TARIFF_SCOPE_REQUIRED");
                continue;
            }
            stored[key]=value?.DeepClone();
        }
        await content.Save("rates",stored);
    }

    public async Task<UseCaseResult> Banners(bool portfolio)
    {
        return UseCaseResult.Json(await content.Read(portfolio ? "portfolio" : "banners", portfolio ? "portfolio" : "banners"));
    }

    public async Task<UseCaseResult> SaveBanners(bool portfolio, JsonNode? raw, Dictionary<string, object?> user)
    {
        var rows = raw as JsonArray ?? throw new UseCaseException(ErrorKind.Invalid, "فهرست نمونه‌کار یا بنر معتبر نیست.");
        if (rows.Count > (portfolio ? 30 : 12))
            throw new UseCaseException(ErrorKind.Invalid, "تعداد موارد بیش از حد مجاز است.");
        var saved = new JsonArray();
        var ids = new HashSet<string>();
        foreach (var node in rows)
        {
            var item = Input.Object(node);
            var id = Input.Text(item, "id", 1, 50);
            if (!Regex.IsMatch(id, @"^[a-zA-Z0-9_-]{1,50}$") || !ids.Add(id))
                throw new UseCaseException(ErrorKind.Invalid, "شناسه بنر معتبر یا یکتا نیست.");
            var title = Input.Text(item, "title", 3, 120);
            var subtitle = Input.Text(item, "subtitle", 0, 350);
            var eyebrow = Input.Text(item, "eyebrow", 0, 80);
            var label = Input.Text(item, "label", portfolio ? 2 : 0, 60);
            var image = Input.Text(item, "image", 1, 1500);
            var link = Input.Text(item, "link", portfolio ? 1 : 0, 1000);
            static bool Internal(string value) => value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\') && !value.Any(c => c == '\r' || c == '\n');
            if (!Internal(image) || (link != "" && !Internal(link)))
                throw new UseCaseException(ErrorKind.Invalid, "تصویر و لینک باید یک مسیر داخلی سایت باشند.");
            if ((label == "") != (link == "") || (label != "" && label.Length < 2))
                throw new UseCaseException(ErrorKind.Invalid, "برای نمایش دکمه، متن و لینک آن را کامل کنید؛ برای بنر بدون دکمه هر دو را خالی بگذارید.");
            var theme = Input.Text(item, "theme", 1, 12);
            var kind = Input.Text(item, "kind", 1, 12);
            if (!new[] { "navy", "orange", "sage" }.Contains(theme) || !new[] { "promotion", "portfolio" }.Contains(kind))
                throw new UseCaseException(ErrorKind.Invalid, "نوع یا رنگ بنر معتبر نیست.");
            saved.Add(new JsonObject { { "id", id }, { "title", title }, { "subtitle", subtitle }, { "eyebrow", eyebrow }, { "image", image }, { "link", link }, { "label", label }, { "theme", theme }, { "kind", portfolio ? "portfolio" : kind }, { "active", Input.Flag(item, "active") }, { "demo", Input.Flag(item, "demo") } });
        }
        await content.Save(portfolio ? "portfolio" : "banners", saved);
        await sessions.Audit(user.Str("email"), portfolio ? "ویرایش نمونه‌کارها" : "ویرایش بنرهای سایت");
        return UseCaseResult.Json(new
        {
            ok = true
        });
    }

    public async Task<UseCaseResult> UploadImage(IUploadContent upload, Dictionary<string, object?> user)
    {
        var bytes = await upload.Read(5 * 1024 * 1024);
        var mime = UploadRules.ImageMime(bytes) ?? throw new UseCaseException(ErrorKind.UnsupportedMedia, "فقط تصویر PNG، JPG یا WebP مجاز است.");
        var id = Input.NewId();
        await files.WriteImage(id, bytes);
        try
        {
            await repository.InsertAsset(id, mime, bytes.Length, Input.Now());
        }
        catch
        {
            files.DeleteImage(id);
            throw;
        }
        await sessions.Audit(user.Str("email"), "بارگذاری تصویر");
        return UseCaseResult.Json(new
        {
            url = "/api/banner-assets/" + id
        }, created: true);
    }

    // Kept as a compatible no-op endpoint. Content is never populated with invented articles.
    public Task<UseCaseResult> Seed() => Task.FromResult(UseCaseResult.Json(new { ok=true }));
}
