namespace ChapGiti.Application.Features;

public sealed class ContentService(
    IContentRepository repository,
    IContentDefaults defaults)
{
    public async Task<QuantityDiscount> Discount()
    {
        var row = await repository.ReadSetting("quantityDiscount");
        return row == null ? QuantityDiscount.Standard : ParseDiscount(Input.Object(JsonNode.Parse(row.Str("value"))));
    }
    public static QuantityDiscount ParseDiscount(JsonObject body)
    {
        if (body["active"] is not JsonValue v || !v.TryGetValue<bool>(out var active))
            throw new UseCaseException(ErrorKind.Invalid, "وضعیت تخفیف معتبر نیست.");
        return new(active, (int)Input.Number(body, "threshold", 0, 10000), (int)Input.Number(body, "percentage", 0, 100));
    }
    public async Task<JsonObject?> StoredRates()
    {
        var row = await repository.ReadRates();
        return row == null ? null : JsonNode.Parse(row.Str("value"))?.AsObject();
    }

    public async Task<JsonObject> OrderRequirements()
    {
        var saved = await repository.ReadSetting("orderRequirements");
        if (saved != null) return OrderSubmissionPolicy.Validate(JsonNode.Parse(saved.Str("value")));
        return new JsonObject(ChapGiti.Domain.Printing.PrintServiceRequirements.Services.Keys
            .Select(key => new KeyValuePair<string, JsonNode?>(key, JsonValue.Create(false))));
    }
    public async Task<JsonNode> Read(string key, string defaultKey)
    {
        var row = await repository.ReadSetting(key);
        if (row == null)
            return defaults.Copy(defaultKey);
        var value = JsonNode.Parse(row.Str("value"))!;
        if (value is JsonObject data && defaults.Root[defaultKey] is JsonObject fallback)
        {
            var merged = (JsonObject)fallback.DeepClone();
            foreach (var item in data)
                merged[item.Key] = item.Value?.DeepClone();
            return merged;
        }
        return value;
    }
    public Task Save(string key, JsonNode value) => repository.SaveSetting(key, value.ToJsonString());
    public async Task<JsonArray> Services(bool includeInactive = false)
    {
        var services = (await Read("services", "services")).AsArray().Select(Input.Object);
        return new JsonArray(services.Where(s => includeInactive || Input.Flag(s, "active"))
            .OrderBy(s => Input.Number(s, "displayOrder", 0, 10000))
            .ThenBy(s => s["id"]!.GetValue<string>(), StringComparer.Ordinal)
            .Select(s => s.DeepClone()).ToArray());
    }
    public async Task<JsonObject> Service(string slug)
    {
        var services = await Services();
        return services.OfType<JsonObject>().FirstOrDefault(service =>
            service["slug"]!.GetValue<string>() == slug || service["aliases"]!.AsArray().Any(alias => alias!.GetValue<string>() == slug))
            ?? throw new UseCaseException(ErrorKind.NotFound, "خدمت مورد نظر پیدا نشد.");
    }
    public async Task<object> Bootstrap()
    {
        var site = await Read("site", "settings");
        var banners = (await Read("banners", "banners")).AsArray()
            .Where(item => item?["active"]?.GetValue<bool>() == true && item["kind"]?.GetValue<string>() != "portfolio")
            .Select(item =>
            {
                var banner = item!.DeepClone();
                if (Regex.IsMatch(Uri.UnescapeDataString(banner["link"]?.GetValue<string>() ?? ""), @"^/portfolio(?:[/?#]|$)", RegexOptions.IgnoreCase))
                {
                    banner["link"] = "";
                    banner["label"] = "";
                }
                return banner;
            }).ToArray();
        return new
        {
            site,
            orderRequirements = PrintServiceRequirements.Services.Keys.ToDictionary(key => key, _ => true),
            articles = Array.Empty<object>(),
            banners,
            portfolio = Array.Empty<object>(),
            services = await Services()
        };
    }
}
