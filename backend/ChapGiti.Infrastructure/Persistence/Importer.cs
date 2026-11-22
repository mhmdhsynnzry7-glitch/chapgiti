using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
namespace ChapGiti.Infrastructure.Persistence;

internal static class Importer
{
    public static readonly string[] Tables = ["settings", "admin_users", "admin_usernames", "customer_registrations", "customer_accounts", "orders", "events", "files", "articles", "messages", "sms_logs", "audit_logs", "banner_assets", "tickets", "ticket_messages"];
    public static async Task Apply(SqlStore db, string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Invalid SQLite export.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        await db.Transaction(async () =>
        {
            if (await db.One("SELECT source_hash FROM dbo.Import_History WITH(UPDLOCK,HOLDLOCK) WHERE source_hash=@p0", hash) != null)
                return true;
            foreach (var table in Tables)
            {
                // SQL009 owns this single activation marker even on a fresh installation.
                var ownedMarker=table=="settings"?" WHERE [key]<>N'order_seen_baseline_sequence'":"";
                var count = await db.One("SELECT COUNT_BIG(*) n FROM dbo.[" + table + "] WITH(UPDLOCK,HOLDLOCK)"+ownedMarker);
                if (count!.Num("n") != 0)
                    throw new InvalidOperationException("Target database must be empty; existing SQL Server data was not changed.");
            }
            foreach (var table in Tables)
            {
                var rows = root[table] as JsonArray ?? new JsonArray();
                var columnRows = await db.Query("SELECT [name] FROM sys.columns WHERE object_id=OBJECT_ID(@p0)", "dbo." + table);
                var allowed = columnRows.Select(x => x.Str("name")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in rows)
                {
                    var row = (JsonObject)Input.Object(entry).DeepClone();
                    if(table=="settings"&&row["key"]?.ToString()=="order_seen_baseline_sequence")continue;
                    // Legacy exports predate EF magazine metadata and workflow versioning.
                    // Preserve actual publication flags/prices without inventing authors or dates.
                    if(table=="articles"&&allowed.Contains("publication_status")&&!row.ContainsKey("publication_status"))
                        row["publication_status"]=Convert.ToBoolean(Value(row["published"]))?"published":"draft";
                    if(table=="orders"&&allowed.Contains("workflow_version")&&!row.ContainsKey("workflow_version"))
                        row["workflow_version"]=1;
                    var columns = row.Select(x => x.Key).ToArray();
                    if (columns.Any(x => !allowed.Contains(x)))
                        throw new InvalidOperationException("Unknown column in export: " + table);
                    var sql = "INSERT dbo.[" + table + "] (" + string.Join(",", columns.Select(x => "[" + x + "]")) + ") VALUES(" + string.Join(",", columns.Select((_, i) => "@p" + i)) + ")";
                    var values = columns.Select(key => Value(row[key])).ToArray();
                    await db.Execute(sql, values);
                }
            }
            await db.Execute("INSERT dbo.Import_History VALUES(@p0,@p1)", hash, Input.Now());
            return true;
        });
    }
    private static object? Value(JsonNode? node)
    {
        if (node == null)
            return null;
        var value = node.AsValue();
        if (value.TryGetValue<string>(out var text))
            return text;
        if (value.TryGetValue<long>(out var integer))
            return integer;
        if (value.TryGetValue<bool>(out var flag))
            return flag;
        if (value.TryGetValue<double>(out var number))
            return number;
        throw new InvalidOperationException("Unsupported export value.");
    }
}
