using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ChapGiti.Infrastructure.Persistence.Ef;

// Keeps the established detached read-model contracts while persistence uses typed EF entities.
internal static partial class EfRows
{
    public static DateTime Date(string value) => DateTime.Parse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
    public static DateTime? Date(string? value,bool nullable) => value==null?null:Date(value);
    // datetime2 has no timezone/Kind; this application's stored timestamp contract is UTC.
    public static string? Stamp(DateTime? value) => value==null?null:DateTime.SpecifyKind(value.Value,DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ",CultureInfo.InvariantCulture);
    public static Dictionary<string,object?> Row<T>(T value,ChapGitiDbContext context) where T : notnull
    {
        var entity=context.Model.FindEntityType(typeof(T));
        var columns=entity?.GetProperties().ToDictionary(p=>p.Name,p=>p.GetColumnName(StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema()))!);
        return value.GetType().GetProperties().Where(p=>columns==null||columns.ContainsKey(p.Name)).ToDictionary(
            p=>columns?.GetValueOrDefault(p.Name)??Snake().Replace(p.Name,"$1_$2").ToLowerInvariant(),
            p=>p.GetValue(value) is DateTime date?(object?)DateTime.SpecifyKind(date,DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ",CultureInfo.InvariantCulture):p.GetValue(value),StringComparer.OrdinalIgnoreCase);
    }
    public static async Task<List<Dictionary<string,object?>>> Rows<T>(this IQueryable<T> query,ChapGitiDbContext context) where T:notnull =>
        (await query.ToListAsync()).Select(row=>Row(row,context)).ToList();
    public static async Task<Dictionary<string,object?>?> FirstRow<T>(this IQueryable<T> query,ChapGitiDbContext context) where T:class
    {var value=await query.FirstOrDefaultAsync();return value==null?null:Row(value,context);}
    [GeneratedRegex("([a-z0-9])([A-Z])")]private static partial Regex Snake();
}
