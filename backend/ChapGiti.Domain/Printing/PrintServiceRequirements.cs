using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace ChapGiti.Domain.Printing;

public static class PrintServiceRequirements
{
    // Stable order-service identifiers; marketing CMS slugs remain independent.
    public static readonly FrozenDictionary<string, string> Services = new Dictionary<string, string>
    {
        ["book-printing"] = "چاپ کتاب", ["publishing"] = "چاپ و خدمات نشر",
        ["cover-design"] = "چاپ و طراحی جلد", ["page-layout"] = "چاپ و صفحه‌آرایی",
        ["catalog-printing"] = "کاتالوگ و نشریه"
    }.ToFrozenDictionary();
    public static bool RequiredForSavedOrder(JsonObject spec) => spec["requiresPdf"] is JsonValue value && value.TryGetValue<bool>(out var required) && required;
}
