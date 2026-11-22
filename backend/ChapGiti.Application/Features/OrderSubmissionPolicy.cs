using ChapGiti.Domain.Printing;

namespace ChapGiti.Application.Features;

public sealed class OrderSubmissionPolicy
{
    public bool RequiresPdf(string service)
    {
        if (!PrintServiceRequirements.Services.ContainsKey(service))
            throw new UseCaseException(ErrorKind.Invalid, "خدمت چاپ انتخاب‌شده معتبر نیست.", code: "ORDER_SERVICE_INVALID");
        return true;
    }
    public static JsonObject Validate(JsonNode? node)
    {
        var values = Input.Object(node);
        if (values.Any(pair => !PrintServiceRequirements.Services.ContainsKey(pair.Key)))
            throw new UseCaseException(ErrorKind.Invalid, "خدمت ناشناخته در تنظیمات مدارک چاپ وجود دارد.", code: "ORDER_REQUIREMENTS_INVALID");
        var result = new JsonObject();
        foreach (var service in PrintServiceRequirements.Services.Keys)
        {
            if (values[service] is not JsonValue value || !value.TryGetValue<bool>(out var required))
                throw new UseCaseException(ErrorKind.Invalid, "الزام فایل هر خدمت باید مشخص باشد.", code: "ORDER_REQUIREMENTS_INVALID");
            result[service] = required;
        }
        return result;
    }
}
