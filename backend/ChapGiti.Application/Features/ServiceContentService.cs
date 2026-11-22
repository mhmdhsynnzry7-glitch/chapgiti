namespace ChapGiti.Application.Features;

public sealed class ServiceContentService(
    ContentService content,
    IAdminContentRepository repository,
    IUnitOfWork unitOfWork,
    SessionService sessions)
{
    private static readonly HashSet<string> Fields = ["slug", "title", "subtitle", "description", "fullDescription", "image", "icon", "color", "features", "sections", "seoTitle", "seoDescription", "displayOrder", "active", "version", "updatedAt"];

    public async Task<UseCaseResult> List() => UseCaseResult.Json(await content.Services(true));

    public Task<UseCaseResult> Delete(string id, int version, Dictionary<string, object?> admin) =>
        unitOfWork.Transaction(async () =>
        {
            await repository.LockSetting("services");
            var items = await content.Services(true);
            var item = items.OfType<JsonObject>().SingleOrDefault(i => i["id"]?.ToString() == id)
                ?? throw new UseCaseException(ErrorKind.NotFound, "خدمت پیدا نشد.");
            if (item["version"]!.GetValue<int>() != version)
                throw new UseCaseException(ErrorKind.Conflict, "نسخهٔ خدمت تغییر کرده است.", code:"SERVICE_VERSION_CONFLICT");
            var slugs = item["aliases"]!.AsArray().Select(a=>a!.ToString()).Append(item["slug"]!.ToString()).Append(id);
            foreach (var slug in slugs)
                if (PrintServiceRequirements.Services.ContainsKey(slug) || await repository.ServiceInUse(slug))
                    throw new UseCaseException(ErrorKind.Conflict, "این خدمت در سفارش‌ها کاربرد دارد. آن را غیرفعال کنید.", code:"SERVICE_IN_USE");
            items.Remove(item);
            await content.Save("services", items);
            await sessions.Audit(admin.Str("email"), "حذف خدمت " + item["slug"]);
            return UseCaseResult.Json(new {ok=true});
        });

    public async Task<UseCaseResult> Save(string? id, JsonObject body, Dictionary<string, object?> user)
    {
        id = id?.ToLowerInvariant();
        if (body.Any(field => !Fields.Contains(field.Key)))
            throw new UseCaseException(ErrorKind.Invalid, "فیلدهای خدمت معتبر نیستند.");
        var saved = await Validate(body);
        var serviceId = id ?? Input.NewId();
        await unitOfWork.Transaction(async () =>
        {
            await repository.LockSetting("services");
            var items = await content.Services(true);
            var current = items.OfType<JsonObject>().FirstOrDefault(item => item["id"]!.GetValue<string>() == serviceId);
            if (id != null && current == null)
                throw new UseCaseException(ErrorKind.NotFound, "خدمت پیدا نشد.");
            if (id == null && items.Count >= 60)
                throw new UseCaseException(ErrorKind.Invalid, "حداکثر ۶۰ خدمت قابل تعریف است.");
            if (current != null && Input.Number(body, "version", 1, int.MaxValue - 1) != Input.Number(current, "version", 1, int.MaxValue - 1))
                throw new UseCaseException(ErrorKind.Conflict, "این خدمت تغییر کرده است؛ فهرست را تازه کنید.", code: "SERVICE_VERSION_CONFLICT");

            foreach (var key in new[] {"seoTitle","seoDescription"})
                if (!body.ContainsKey(key) && current?[key] != null) saved[key] = current[key]!.DeepClone();
            var slug = saved["slug"]!.GetValue<string>();
            var aliases = current?["aliases"]?.AsArray().Select(alias => alias!.GetValue<string>()).ToList() ?? [];
            if (current != null && current["slug"]!.GetValue<string>() != slug)
                aliases.Add(current["slug"]!.GetValue<string>());
            aliases = aliases.Where(alias => alias != slug).Distinct(StringComparer.Ordinal).ToList();
            if (aliases.Count > 20)
                throw new UseCaseException(ErrorKind.Invalid, "نشانی این خدمت بیش از حد تغییر کرده است.");
            var reserved = aliases.Append(slug).ToHashSet(StringComparer.Ordinal);
            if (items.OfType<JsonObject>().Where(item => item != current).Any(item =>
                reserved.Contains(item["slug"]!.GetValue<string>()) || item["aliases"]!.AsArray().Any(alias => reserved.Contains(alias!.GetValue<string>()))))
                throw new UseCaseException(ErrorKind.Conflict, "نشانی خدمت یا نشانی قبلی آن قبلاً استفاده شده است.", code: "SERVICE_SLUG_CONFLICT");

            saved["updatedAt"] = Input.Now();
            saved["id"] = serviceId;
            saved["aliases"] = new JsonArray(aliases.Select(alias => (JsonNode?)JsonValue.Create(alias)).ToArray());
            saved["version"] = current == null ? 1 : (int)Input.Number(current, "version", 1, int.MaxValue - 1) + 1;
            if (current != null) items.Remove(current);
            items.Add(saved.DeepClone());
            await content.Save("services", items);
            await sessions.Audit(user.Str("email"), (id == null ? "ایجاد خدمت " : "ویرایش خدمت ") + saved["title"]!.GetValue<string>());
            return true;
        });
        return UseCaseResult.Json(new { ok = true, id = serviceId, service = saved }, created: id == null);
    }

    public async Task<UseCaseResult> Reorder(JsonObject body, Dictionary<string, object?> user)
    {
        if (body.Count != 1 || body["items"] is not JsonArray requested || requested.Count > 60)
            throw new UseCaseException(ErrorKind.Invalid, "ترتیب خدمات معتبر نیست.");
        await unitOfWork.Transaction(async () =>
        {
            await repository.LockSetting("services");
            var current = await content.Services(true);
            if (requested.Count != current.Count)
                throw new UseCaseException(ErrorKind.Conflict, "فهرست خدمات تغییر کرده است؛ صفحه را تازه کنید.");
            var byId = current.OfType<JsonObject>().ToDictionary(item => item["id"]!.GetValue<string>());
            var ordered = new JsonArray();
            foreach (var raw in requested)
            {
                var entry = Input.Object(raw);
                var id = Input.Id(entry, "id");
                if (!byId.Remove(id, out var item) || Input.Number(entry, "version", 1, int.MaxValue - 1) != Input.Number(item, "version", 1, int.MaxValue - 1))
                    throw new UseCaseException(ErrorKind.Conflict, "فهرست یا نسخهٔ خدمات تغییر کرده است؛ دوباره دریافت کنید.");
                if (Input.Number(item, "displayOrder", 0, 10000) != ordered.Count)
                {
                    item["updatedAt"] = Input.Now();
                    item["displayOrder"] = ordered.Count;
                    item["version"] = (int)Input.Number(item, "version", 1, int.MaxValue - 1) + 1;
                }
                ordered.Add(item.DeepClone());
            }
            await content.Save("services", ordered);
            await sessions.Audit(user.Str("email"), "تغییر ترتیب خدمات");
            return true;
        });
        return UseCaseResult.Json(new { ok = true });
    }

    private async Task<JsonObject> Validate(JsonObject body)
    {
        var slug = Input.Text(body, "slug", 1, 100);
        if (!Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            throw new UseCaseException(ErrorKind.Invalid, "نشانی خدمت باید با حروف کوچک انگلیسی، عدد و خط تیره نوشته شود.");
        var icon = Input.Text(body, "icon", 1, 16);
        var color = Input.Text(body, "color", 1, 16);
        if (!new[] { "book", "feather", "palette", "layout", "pen", "layers" }.Contains(icon) ||
            !new[] { "blue", "orange", "sage", "lilac", "sand", "rose" }.Contains(color))
            throw new UseCaseException(ErrorKind.Invalid, "آیکون یا رنگ خدمت معتبر نیست.");
        var image = Input.Text(body, "image", 0, 500);
        if (image != "")
        {
            var asset = Regex.Match(image, "^/api/banner-assets/([a-f0-9-]{36})$");
            if (asset.Success)
            {
                if (!Guid.TryParseExact(asset.Groups[1].Value, "D", out _) || await repository.FindAsset(asset.Groups[1].Value) == null)
                    throw new UseCaseException(ErrorKind.Invalid, "تصویر بارگذاری‌شده پیدا نشد.");
            }
            else
                throw new UseCaseException(ErrorKind.Invalid, "تصویر خدمت را از همین پنل بارگذاری کنید.");
        }
        if (body["features"] is not JsonArray features || features.Count > 12 ||
            body["sections"] is not JsonArray sections || sections.Count > 8)
            throw new UseCaseException(ErrorKind.Invalid, "ویژگی‌ها یا بخش‌های توضیح خدمت معتبر نیستند.");
        var cleanFeatures = new JsonArray();
        foreach (var feature in features)
            cleanFeatures.Add(Input.Text(new JsonObject { ["feature"] = feature?.DeepClone() }, "feature", 1, 160));
        var cleanSections = new JsonArray();
        foreach (var raw in sections)
        {
            var section = Input.Object(raw);
            cleanSections.Add(new JsonObject { ["heading"] = Input.Text(section, "heading", 2, 100), ["body"] = Input.Text(section, "body", 10, 5000) });
        }
        return new JsonObject
        {
            ["slug"] = slug, ["title"] = Input.Text(body, "title", 2, 120),
            ["subtitle"] = Input.Text(body, "subtitle", 0, 180), ["description"] = Input.Text(body, "description", 10, 600),
            ["fullDescription"] = Input.Text(body, "fullDescription", 0, 12000), ["image"] = image,
            ["icon"] = icon, ["color"] = color, ["features"] = cleanFeatures, ["sections"] = cleanSections,
            ["seoTitle"] = Input.Text(body, "seoTitle", 0, 100), ["seoDescription"] = Input.Text(body, "seoDescription", 0, 180),
            ["displayOrder"] = (int)Input.Number(body, "displayOrder", 0, 10000), ["active"] = Input.Flag(body, "active")
        };
    }
}
