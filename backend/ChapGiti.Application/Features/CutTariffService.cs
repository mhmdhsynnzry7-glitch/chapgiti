namespace ChapGiti.Application.Features;

public sealed class CutTariffService(ICutTariffRepository tariffs, IPrintCatalogRepository options, PrintCatalogService catalog,
    IUnitOfWork work, ContentService content, SessionService sessions)
{
    private const string Missing = "تعرفه این نوع چاپ برای قطع انتخاب‌شده هنوز تنظیم نشده است.";

    private async Task RequireStorage()
    {
        if (!await tariffs.Available()) throw new UseCaseException(ErrorKind.Unavailable, "تعرفه‌های قطع هنوز روی این سرور فعال نشده‌اند.", code:"CUT_TARIFF_UNAVAILABLE");
    }

    public async Task<UseCaseResult> Get(string id)
    {
        await RequireStorage();
        var cut = (await catalog.Items(true)).SingleOrDefault(x => x.Id.Equals(id,StringComparison.OrdinalIgnoreCase) && x.Kind == "bookSizes")
            ?? throw new UseCaseException(ErrorKind.NotFound,"قطع کتاب پیدا نشد.");
        var current = await tariffs.Latest(id);
        return UseCaseResult.Json(new { cutId = cut.Id, cut.Code, cut.Label, version = current?.Version ?? 0, updatedAt = current?.UpdatedAt, rates = current?.Rates, isProposed = current?.IsProposed ?? false });
    }

    public async Task<UseCaseResult> Save(string id, JsonObject body, Dictionary<string,object?> admin)
    {
        await RequireStorage();
        var expected = Input.Number(body,"version",0,int.MaxValue-1);
        var rates = CutTariff.Validate(Input.Object(body["rates"]));
        var revision = await work.Transaction(async () =>
        {
            await options.LockKind("bookSizes");
            var cut = await options.Lock(id);
            if (cut is not { Kind: "bookSizes", Active: true }) throw new UseCaseException(ErrorKind.Invalid,"قطع انتخاب‌شده فعال نیست.");
            var current = await tariffs.Latest(id);
            if ((current?.Version ?? 0) != expected) throw new UseCaseException(ErrorKind.Conflict,"تعرفه در نشست دیگری تغییر کرده است. نسخهٔ تازه را دریافت کنید.",code:"TARIFF_CONFLICT");
            if (current?.IsProposed == true && body["approveProposal"]?.GetValueKind() != System.Text.Json.JsonValueKind.True)
                throw new UseCaseException(ErrorKind.Conflict, "برای فعال‌شدن تعرفه، نرخ پیشنهادی را صریحاً تأیید کنید.", code: "TARIFF_APPROVAL_REQUIRED");
            if (current is { IsProposed: false } && JsonNode.DeepEquals(current.Rates,rates)) return current;
            var next = new CutTariffRevision(id,checked((int)expected+1),Input.Now(),rates);
            await tariffs.Append(next);
            await sessions.Audit(admin.Str("email"),"ثبت تعرفهٔ قطع " + cut.Code + " نسخه " + next.Version);
            return next;
        });
        return UseCaseResult.Json(revision);
    }

    public async Task<JsonObject> Estimate(PrintPricing pricing, JsonObject spec, bool includeSnapshot = false)
    {
        await RequireStorage();
        var selections = await catalog.Items();
        var cut = selections.Single(x => x.Kind == "bookSizes" && x.Code == spec["size"]!.GetValue<string>());
        var revision = await tariffs.Latest(cut.Id) ?? throw new UseCaseException(ErrorKind.Conflict,Missing,code:"CUT_TARIFF_NOT_CONFIGURED");
        if (revision.IsProposed)
            throw new UseCaseException(ErrorKind.Conflict, "تعرفهٔ این قطع در انتظار تأیید چاپخانه است. می‌توانید قطع دیگری انتخاب کنید یا با چاپخانه تماس بگیرید.", code: "CUT_TARIFF_APPROVAL_REQUIRED");
        var rates = CutTariff.Validate(revision.Rates);
        var finishing = await content.StoredRates();
        var selectedFinishes=(spec["details"]?["extras"] as JsonArray)?.Select(value=>value!.GetValue<string>()).ToHashSet() ?? [];
        foreach (var option in selections.Where(i => i.Kind == "finishingServices"))
        {
            var key = option.Metadata["rateKey"]?.GetValue<string>() ?? option.Code;
            if (finishing?[key] != null) rates[key] = finishing[key]!.DeepClone();
            else if (selectedFinishes.Contains(option.Code))
                throw new UseCaseException(ErrorKind.Conflict,"تعرفهٔ خدمت تکمیلی «"+option.Label+"» هنوز تنظیم نشده است.",code:"FINISHING_TARIFF_NOT_CONFIGURED");
        }
        var discount = await content.Discount();
        var quote = pricing.Quote(spec,rates,discount);
        if (includeSnapshot) quote["tariffSnapshot"] = new JsonObject { ["cutId"]=cut.Id,["version"]=revision.Version,
            ["effectiveAt"]=revision.UpdatedAt,["rates"]=rates.DeepClone(),["discount"]=System.Text.Json.JsonSerializer.SerializeToNode(discount) };
        quote["tariffVersion"] = revision.Version;
        return quote;
    }
}
