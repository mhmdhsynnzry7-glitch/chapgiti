namespace ChapGiti.Application.Common;

public static class ReadModelExtensions
{
    public static string Str(this Dictionary<string, object?> row, string key) => Convert.ToString(row.GetValueOrDefault(key)) ?? "";
    public static long Num(this Dictionary<string, object?> row, string key) => Convert.ToInt64(row.GetValueOrDefault(key) ?? 0);
    public static string Json(object? value) => System.Text.Json.JsonSerializer.Serialize(value);
}
