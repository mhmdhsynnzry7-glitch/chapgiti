using ChapGiti.Application.Catalog;

namespace ChapGiti.Application.Features;

public sealed class PrintCatalogService(IPrintCatalogRepository repository, IUnitOfWork unitOfWork, SessionService sessions,
    ICutTariffRepository tariffs, ContentService content, IContentDefaults defaults)
{
    private IReadOnlyList<PrintCatalogItem>? active;
    public Task<bool> Available() => repository.Available();
    public async Task<IReadOnlyList<PrintCatalogItem>> Items(bool all = false)
    {
        if (!all && active != null) return active;
        if (!await Available())
            throw new UseCaseException(ErrorKind.Unavailable, "تنظیمات چاپ هنوز در دیتابیس فعال نشده است.", code: "PRINT_CATALOG_UNAVAILABLE");
        return all ? await repository.List(true) : active ??= await repository.List(false);
    }
    public async Task<PrintPricing> Pricing()
    {
        var items = await Items();
        var groups = new JsonObject();
        foreach (var kind in PrintCatalogRules.PricingRules.Keys)
        {
            var options = items.Where(i => i.Kind == kind).ToArray();
            if (options.Length == 0)
                throw new UseCaseException(ErrorKind.Unavailable, "گزینه‌های چاپ کامل نیست؛ با چاپخانه تماس بگیرید.", code: "PRINT_CATALOG_EMPTY");
            try
            {
                groups[kind] = new JsonObject(options.Select(i => new KeyValuePair<string, JsonNode?>(i.Code, PrintCatalogRules.Validate(i.Kind, i.Code, i.Label, i.PricingRule, i.Metadata))));
            }
            catch (RuleViolation) { throw new UseCaseException(ErrorKind.Unavailable, "پیکربندی یکی از گزینه‌های چاپ معتبر نیست؛ با چاپخانه تماس بگیرید.", code: "PRINT_CATALOG_INVALID"); }
        }
        return new PrintPricing(new PrintCatalog(groups));
    }
    public async Task<JsonObject> Snapshot(JsonObject spec)
    {
        var items = await Items();
        var selected = new List<(string Kind, string Code)> { ("bookSizes", spec["size"]!.GetValue<string>()) };
        if (spec["details"] is JsonObject d)
        {
            foreach (var (field, kind) in new[] { ("printType", "textPrints"), ("bwPaper", "bookPapers"), ("colorPaper", "bookPapers"), ("bindingStyle", "bookBindings"), ("coverPrint", "coverPrints"), ("coverPaper", "coverPapers"), ("coating", "coverCoatings") })
                selected.Add((kind, d[field]!.GetValue<string>()));
            foreach (var code in d["extras"]!.AsArray())
                selected.Add(("finishingServices", code!.GetValue<string>()));
        }
        var options = selected.Select(s => items.Single(i => i.Kind == s.Kind && i.Code == s.Code))
            .DistinctBy(i => i.Id).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        return new JsonObject { ["version"] = 1, ["options"] = new JsonArray(options.Select(i =>
            (JsonNode)new JsonObject { ["id"] = i.Id, ["kind"] = i.Kind, ["code"] = i.Code, ["label"] = i.Label,
                ["version"] = i.Version, ["metadata"] = i.Metadata.DeepClone() }).ToArray()) };
    }
    public async Task ValidateSelections(JsonObject spec)
    {
        var items = await Items();
        void Require(string kind, JsonNode? node)
        {
            var code = node is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
            var item = items.SingleOrDefault(i => i.Kind == kind && i.Code == code);
            if (item == null)
                throw new UseCaseException(ErrorKind.Invalid, "یکی از گزینه‌های چاپ نامعتبر یا غیرفعال شده؛ فهرست را تازه کنید.", code: "CATALOG_SELECTION_UNAVAILABLE");
            if (spec["catalogIds"] != null && (spec["catalogIds"] is not JsonObject ids || ids[kind + ":" + code] is not JsonValue id || !id.TryGetValue<string>(out var selectedId) || selectedId != item.Id))
                throw new UseCaseException(ErrorKind.Invalid, "شناسه گزینه با کد آن یکسان نیست.", code: "CATALOG_SELECTION_INVALID");
        }
        Require("bookSizes", spec["size"]);
        if (spec["details"] is JsonObject d)
        {
            foreach (var (field, kind) in new[] { ("printType", "textPrints"), ("bwPaper", "bookPapers"), ("colorPaper", "bookPapers"), ("bindingStyle", "bookBindings"), ("coverPrint", "coverPrints"), ("coverPaper", "coverPapers"), ("coating", "coverCoatings") })
                Require(kind, d[field]);
            if (d["extras"] is JsonArray extras)
            foreach (var code in extras)
                Require("finishingServices", code);
        }
    }
    public async Task LockForSubmission()
    {
        foreach (var kind in PrintCatalogRules.PricingRules.Keys.Order(StringComparer.Ordinal)) await repository.LockKind(kind);
        active = null;
    }
    public async Task<UseCaseResult> Delete(string id, int version, Dictionary<string,object?> admin)
    {
        var candidate = (await Items(true)).SingleOrDefault(i => i.Id == id) ?? throw new UseCaseException(ErrorKind.NotFound, "گزینه پیدا نشد.");
        return await unitOfWork.Transaction(async () =>
        {
            await repository.LockKind(candidate.Kind);
            var item = await repository.Lock(id) ?? throw new UseCaseException(ErrorKind.NotFound, "گزینه پیدا نشد.");
            var tariff = item.Kind == "bookSizes" && await tariffs.Available() ? await tariffs.Latest(id) : null;
            if (item.IsDefault || tariff is { IsProposed: false } || await repository.IsReferenced(item))
                throw new UseCaseException(ErrorKind.Conflict, "گزینهٔ پیش‌فرض یا استفاده‌شده قابل حذف نیست. گزینهٔ جایگزین را پیش‌فرض و این گزینه را غیرفعال کنید.", code:"CATALOG_DELETE_PROTECTED");
            if (tariff is { IsProposed: true, Version: 1 }) await tariffs.DiscardInitialProposal(id);
            if (!await repository.Delete(id, version)) throw new UseCaseException(ErrorKind.Conflict, "نسخهٔ گزینه تغییر کرده است.", code:"CATALOG_VERSION_CONFLICT");
            await sessions.Audit(admin.Str("email"), "حذف گزینه چاپ " + item.Code);
            return UseCaseResult.Json(new {ok=true});
        });
    }
    public async Task<UseCaseResult> Save(string? id, JsonObject body, Dictionary<string, object?> admin)
    {
        await Items(true);
        var kind = Input.Text(body, "kind", 1, 40);
        var code = Input.Text(body, "code", 1, 80);
        var label = Input.Text(body, "label", 2, 160);
        var rule = Input.Text(body, "pricingRule", 1, 50);
        var metadata = PrintCatalogRules.Validate(kind, code, label, rule, Input.Object(body["metadata"]));
        var order = Input.Number(body, "displayOrder", 0, 10000);
        var enabled = Input.Flag(body, "active");
        var isDefault = Input.Flag(body, "isDefault");
        if (!enabled && isDefault)
            throw new UseCaseException(ErrorKind.Invalid, "گزینه پیش‌فرض باید فعال باشد.", code: "CATALOG_DEFAULT_INACTIVE");
        var itemId = id ?? Input.NewId();
        var now = Input.Now();
        await unitOfWork.Transaction(async () =>
        {
            await repository.LockKind(kind);
            var old = id == null ? null : await repository.Lock(id);
            if (id != null && old == null)
                throw new UseCaseException(ErrorKind.NotFound, "گزینه پیدا نشد.");
            if (old != null && (old.Kind != kind || old.Code != code))
                throw new UseCaseException(ErrorKind.Invalid, "کد و گروه گزینه پس از ایجاد قابل تغییر نیست.", code: "CATALOG_CODE_IMMUTABLE");
            var version = old == null ? (int?)null : (int)Input.Number(body, "version", 1, int.MaxValue);
            if (old != null && old.Version != version)
                throw new UseCaseException(ErrorKind.Conflict, "این گزینه تغییر کرده است؛ فهرست را دوباره بارگذاری کنید.", code: "CATALOG_VERSION_CONFLICT");
            if (await repository.CodeExists(kind, code, id))
                throw new UseCaseException(ErrorKind.Conflict, "این کد قبلاً ثبت شده است.", code: "CATALOG_CODE_EXISTS");
            if (old?.IsDefault == true && !isDefault && !(await repository.List(true)).Any(i => i.Kind == kind && i.Id != id && i.Active && i.IsDefault))
                throw new UseCaseException(ErrorKind.Invalid, "ابتدا گزینه دیگری را پیش‌فرض کنید.", code: "CATALOG_DEFAULT_REQUIRED");
            if (isDefault)
                await repository.ClearDefault(kind, itemId, now);
            var item = new PrintCatalogItem(itemId, kind, code, label, rule, metadata, (int)order, enabled, isDefault, (version ?? 0) + 1, old?.CreatedAt ?? now, now);
            if (!await repository.Save(item, version))
                throw new UseCaseException(ErrorKind.Conflict, "گزینه هم‌زمان تغییر کرده است.", code: "CATALOG_VERSION_CONFLICT");
            if (kind == "bookSizes" && enabled && !metadata["isCustom"]!.GetValue<bool>() && await tariffs.Latest(itemId) == null)
            {
                var basis = (JsonObject)defaults.Root["rates"]!.DeepClone();
                if (await content.StoredRates() is { } stored)
                    foreach (var key in CutTariff.Keys.Where(stored.ContainsKey)) basis[key] = stored[key]?.DeepClone();
                var proposal = CutTariff.Propose(metadata, basis);
                await tariffs.Append(new(itemId, 1, now, proposal, IsProposed: true));
            }
            await sessions.Audit(admin.Str("email"), "ویرایش تنظیمات چاپ: " + kind + "/" + code);
            return true;
        });
        active = null;
        return UseCaseResult.Json(new
        {
            id = itemId,
            ok = true
        }, created: id == null);
    }
}
