using ChapGiti.Application.Orders;
using ChapGiti.Domain.Orders;
using ChapGiti.Domain.Printing;

namespace ChapGiti.Application.Features;

public sealed record ProductionReadiness(bool CanStart, ProductionProblem[] Problems, string? ApprovedFileId);

public sealed class OrderProductionReadiness(IOrderCorrectionRepository corrections, IOrderQuoteRepository quotes, OrderPdfFiles pdfs)
{
    public async Task<ProductionReadiness> Assess(Dictionary<string, object?> order, long? proposedPrice = null)
    {
        var problems = new List<ProductionProblem>();
        var id = order.Str("id"); var modern = order.Num("workflow_version") >= 2;
        var history = await corrections.Corrections(id); var latest = history.LastOrDefault();
        var correctionProblem = OrderProductionRules.CorrectionProblem(latest?.State, order.Str("information_request") != "");
        if (correctionProblem != null) problems.Add(correctionProblem);
        if (order.Str("status") != "confirmed" && !(order.Str("status") == "review" && !modern))
            problems.Add(new("CUSTOMER_APPROVAL_REQUIRED", "سفارش هنوز آمادهٔ ورود به چاپ نیست."));
        var price = proposedPrice ?? (order.GetValueOrDefault("final_price") == null ? null : (long?)order.Num("final_price"));
        if (price == null) problems.Add(new("FINAL_PRICE_REQUIRED", "قیمت تأییدشدهٔ سفارش مشخص نیست."));
        var quote = (await quotes.Quotes(id)).LastOrDefault();
        if (quote?.Str("state") != "accepted" || quote.Num("amount_toman") != price || quote.Str("accepted_by_customer") != order.Str("customer_id"))
            problems.Add(new("CUSTOMER_APPROVAL_REQUIRED", "آخرین پیشنهاد قیمت باید توسط مالک سفارش تأیید شود."));
        string? fileId = null;
        if (correctionProblem == null)
        {
            try { fileId = (await SelectFile(order, latest))?.Id; }
            catch (UseCaseException error) when (error.Code is "ORDER_FILE_REQUIRED" or "ORDER_APPROVED_FILE_INVALID" or "ORDER_FILE_UNAVAILABLE" or "PDF_CONTENT_INVALID")
            { problems.Add(new(error.Code!, error.Message)); }
        }
        var conditions = CurrentConditions(order, latest);
        var acceptance = conditions?["acceptanceId"]?.ToString();
        var receipt = acceptance != null ? await quotes.LockCommand(acceptance) : null;
        if (conditions == null || conditions["acceptedAt"] == null || conditions["fileId"]?.ToString() != fileId ||
            conditions["quoteId"]?.ToString() != quote?.Str("id") || receipt?.Str("kind") != "accept-terms" ||
            receipt.Str("order_id") != id || receipt.Str("actor") != "customer:" + order.Str("customer_id"))
            problems.Add(new("FINAL_TERMS_REQUIRED", "مشتری باید آخرین شرایط چاپ و تحویل را برای فایل تأییدشده بپذیرد."));
        return new(problems.Count == 0, problems.ToArray(), fileId);
    }
    public async Task<ProductionReadiness> Ensure(Dictionary<string, object?> order, long? proposedPrice = null)
    {
        var result = await Assess(order, proposedPrice);
        if (!result.CanStart) throw new UseCaseException(ErrorKind.Conflict, result.Problems[0].Message, code: result.Problems[0].Code);
        return result;
    }
    // Called inside the order command transaction. A correction can be reviewed as
    // part of advancement only when the administrator identifies the submitted file.
    public async Task<OrderFileRecord> ReviewForAdvance(Dictionary<string, object?> order, string? reviewedId, string adminId)
    {
        var id = order.Str("id");
        var latest = (await corrections.Corrections(id)).LastOrDefault();
        var submitted = latest?.State == "submitted" && reviewedId != null;
        if (!submitted)
        {
            var problem = OrderProductionRules.CorrectionProblem(latest?.State, order.Str("information_request") != "");
            if (problem != null) throw new UseCaseException(ErrorKind.Conflict, problem.Message, code: problem.Code);
        }
        var files = await corrections.Files(id);
        var candidate = latest == null ? PrimaryFileId(order, files) : latest.FileId ?? latest.BaseFileId;
        var file = files.SingleOrDefault(f => f.Id == candidate);
        if (file == null) throw new UseCaseException(ErrorKind.Conflict, "فایل اصلی در دسترس نیست.", code: "ORDER_FILE_REQUIRED");
        if ((reviewedId != null && reviewedId != file.Id) ||
            (latest?.RequiresNewPdf == true && file.RevisionId != latest.Id))
            throw new UseCaseException(ErrorKind.Conflict, "نسخهٔ فایل تغییر کرده است.", code: "ORDER_APPROVED_FILE_INVALID");
        await pdfs.ValidateStored(file);
        if (submitted && latest != null)
        {
            var reviewed = latest with { State = "approved", ApprovedFileId = file.Id,
                ReviewedAt = Input.Now(), ReviewedByAdmin = adminId, Version = latest.Version + 1 };
            if (!await corrections.SaveCorrection(latest, reviewed))
                throw new UseCaseException(ErrorKind.Conflict, "نسخهٔ اصلاحیه تغییر کرده است.", code: "CORRECTION_VERSION_CONFLICT");
            await quotes.SetInformation(id, null, false);
        }
        await corrections.SetApprovedFile(id, file.Id);
        return file;
    }

    internal static string SpecificationVersion(JsonObject source)
    {
        var specification = (JsonObject)source.DeepClone();
        specification.Remove("deliveryConditions");
        specification.Remove("quoteSpecifications");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(specification.ToJsonString())));
    }

    internal static void RequireQuotedSpecification(JsonObject spec, string quoteId)
    {
        var recorded = spec["quoteSpecifications"]?[quoteId]?["specificationVersion"]?.ToString();
        if (recorded != null && recorded != SpecificationVersion(spec))
            throw new UseCaseException(ErrorKind.Conflict, "مشخصات سفارش با پیشنهاد مالی تفاوت دارد؛ بررسی تجاری جداگانه لازم است.", code: "QUOTED_SPECIFICATION_CHANGED");
    }

    public static JsonObject? CurrentConditions(Dictionary<string, object?> order, OrderCorrection? latest)
    {
        var conditions = (JsonNode.Parse(order.Str("spec"))?["deliveryConditions"] as JsonArray)?.LastOrDefault() as JsonObject;
        return conditions != null && conditions["specificationVersion"]?.ToString() == SpecificationVersion(JsonNode.Parse(order.Str("spec"))!.AsObject()) && conditions["supersededAt"] == null && conditions["fileId"]?.ToString() == order.Str("approved_file_id")
            && conditions["correctionId"]?.ToString() == latest?.Id ? conditions : null;
    }
    public async Task<OrderFileRecord> RequireApprovedFile(Dictionary<string, object?> order)
    {
        var latest = (await corrections.Corrections(order.Str("id"))).LastOrDefault();
        var problem = OrderProductionRules.CorrectionProblem(latest?.State, order.Str("information_request") != "");
        if (problem != null) throw new UseCaseException(ErrorKind.Conflict, problem.Message, code: problem.Code);
        return (await SelectFile(order, latest))!;
    }
    private async Task<OrderFileRecord?> SelectFile(Dictionary<string, object?> order, OrderCorrection? latest)
    {
        var files = await corrections.Files(order.Str("id"));
        var selected = order.Str("approved_file_id");
        if (latest != null)
        {
            if (latest.State != "approved" || selected != (latest.ApprovedFileId ?? ""))
                throw new UseCaseException(ErrorKind.Conflict, "فایل تأییدشده باید متعلق به آخرین اصلاحیهٔ تأییدشده باشد.", code: "ORDER_APPROVED_FILE_INVALID");
            selected = latest.ApprovedFileId ?? "";
        }

        if (latest == null && selected != "" && selected != PrimaryFileId(order, files))
            throw new UseCaseException(ErrorKind.Conflict, "فایل اصلی سفارش باید تأیید شود.", code:"ORDER_APPROVED_FILE_INVALID");
        var file = files.SingleOrDefault(item => item.Id == selected);
        if (selected != "" && file == null) throw new UseCaseException(ErrorKind.Conflict, "مرجع فایل تأییدشده معتبر نیست.", code: "ORDER_APPROVED_FILE_INVALID");
        if (file == null)
            throw new UseCaseException(ErrorKind.Conflict, "فایل PDF تولید باید صریحاً تأیید شود.", code: "ORDER_FILE_REQUIRED");
        if (file != null) await pdfs.ValidateStored(file);
        return file;
    }
    internal static string? PrimaryFileId(Dictionary<string, object?> order, IReadOnlyList<OrderFileRecord> files) =>
        JsonNode.Parse(order.Str("spec"))?["primaryFileId"]?.ToString() ?? files.LastOrDefault(f=>f.RevisionId == null)?.Id;

}
