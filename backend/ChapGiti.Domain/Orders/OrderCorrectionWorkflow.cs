using ChapGiti.Domain.Common;

namespace ChapGiti.Domain.Orders;

public static class OrderCorrectionWorkflow
{
    public static bool Active(string state) => state is "requested" or "submitted";
    public static bool BeforeProduction(string status) => status is "review" or "awaiting_customer_approval" or "confirmed";
    public static void RequireRequestable(string status)
    {
        if (!BeforeProduction(status)) throw new RuleViolation("درخواست اصلاح فقط پیش از شروع تولید و پس از بازکردن سفارش مجاز است.");
    }
    public static void RequireSubmission(string state)
    {
        if (state != "requested") throw new RuleViolation("این درخواست اصلاح دیگر آمادهٔ دریافت پاسخ نیست؛ آخرین وضعیت را دریافت کنید.");
    }
    public static void RequireReview(string state, string decision, string reason)
    {
        if (state != "submitted") throw new RuleViolation("فقط اصلاحیهٔ ارسال‌شده قابل بازبینی است.");
        if (decision is not ("approve" or "reject")) throw new RuleViolation("نتیجهٔ بازبینی معتبر نیست.");
        if (decision == "reject" && reason.Trim().Length < 2) throw new RuleViolation("دلیل رد اصلاحیه را وارد کنید.");
    }
}

public sealed record ProductionProblem(string Code, string Message);

public static class OrderProductionRules
{
    public static ProductionProblem? CorrectionProblem(string? latestState, bool informationPending) => latestState switch
    {
        "requested" => new("ORDER_CORRECTION_REQUIRED", "اصلاحیهٔ درخواستی هنوز ارسال نشده است."),
        "submitted" => new("ORDER_CORRECTION_REVIEW_REQUIRED", "اصلاحیهٔ مشتری هنوز به تأیید چاپخانه نرسیده است."),
        "rejected" or "superseded" => new("ORDER_CORRECTION_NOT_APPROVED", "آخرین اصلاحیه تأیید نشده است؛ اصلاحیهٔ جایگزین باید بررسی و تأیید شود."),
        _ when informationPending => new("ORDER_INFORMATION_REQUIRED", "درخواست اطلاعات یا فایل تکمیلی هنوز تعیین تکلیف نشده است."),
        _ => null
    };
}
