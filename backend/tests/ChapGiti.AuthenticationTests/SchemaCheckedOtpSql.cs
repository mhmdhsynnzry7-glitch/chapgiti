using System.Data;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using ChapGiti.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace ChapGiti.AuthenticationTests;

// Executes the OTP repository's small INSERT/UPDATE/SELECT vocabulary in memory.
// CHECK expressions come from the deployed schema scripts, not from the new codec.
// No SQL connection, operational database or provider is ever used here.
internal sealed class SchemaCheckedOtpSql : IOtpSqlExecutor
{
    private static readonly IReadOnlyDictionary<string, string> Checks = ReadChecks();
    private readonly DataTable table = new() { Locale = CultureInfo.InvariantCulture };

    public SchemaCheckedOtpSql()
    {
        foreach (var name in new[] { "id", "phone", "purpose", "customer_id", "code_hash", "delivery_status" })
            table.Columns.Add(name, typeof(string));
        foreach (var name in new[] { "created_at", "expires", "consumed_at" }) table.Columns.Add(name, typeof(long));
        table.Columns.Add("attempts", typeof(int)).DefaultValue = 0;
        table.Columns["delivery_status"]!.DefaultValue = "pending";
        table.PrimaryKey = [table.Columns["id"]!];
    }

    public Task<int> Execute(string sql, params object?[] values)
    {
        var insert = Regex.Match(sql, @"INSERT\s+dbo.Customer_Otp_Challenges\s*\(([^)]+)\)\s*VALUES\s*\(([^)]+)\)", RegexOptions.Singleline);
        if (insert.Success)
        {
            var columns = insert.Groups[1].Value.Split(',').Select(x => x.Trim()).ToArray();
            var expressions = insert.Groups[2].Value.Split(',');
            var row = table.NewRow();
            for (var n = 0; n < columns.Length; n++) row[columns[n]] = Value(expressions[n].Trim(), values) ?? DBNull.Value;
            table.Rows.Add(row);
            foreach (var (constraint, expression) in Checks)
            {
                if (table.Select("NOT (" + expression + ")").Length == 0) continue;
                table.Rows.Remove(row);
                throw CheckViolation(constraint);
            }
            return Task.FromResult(1);
        }
        var update = Regex.Match(sql, @"UPDATE\s+dbo.Customer_Otp_Challenges\s+SET\s+(.*?)\s+WHERE\s+(.*)", RegexOptions.Singleline);
        if (!update.Success) throw new InvalidOperationException("Unsupported isolated OTP SQL command.");
        var rows = table.Select(Bind(update.Groups[2].Value, values));
        foreach (var row in rows)
            foreach (var assignment in update.Groups[1].Value.Split(','))
            {
                var parts = assignment.Split('=', 2);
                row[parts[0].Trim()] = Value(parts[1].Trim(), values) ?? DBNull.Value;
            }
        return Task.FromResult(rows.Length);
    }

    public Task<Dictionary<string, object?>?> One(string sql, params object?[] values)
    {
        var query = Regex.Match(sql, @"\bWHERE\s+(.*?)(?:\s+ORDER BY\s+(.*))?$", RegexOptions.Singleline);
        if (!query.Success) throw new InvalidOperationException("Unsupported isolated OTP SQL query.");
        var rows = table.Select(Bind(query.Groups[1].Value, values), query.Groups[2].Value.Trim());
        if (sql.Contains("COUNT(*)", StringComparison.Ordinal))
            return Task.FromResult<Dictionary<string, object?>?>(new() { ["n"] = (long)rows.Length });
        return Task.FromResult(rows.Length == 0 ? null : table.Columns.Cast<DataColumn>().ToDictionary(
            column => column.ColumnName, column => rows[0].IsNull(column) ? null : rows[0][column]));
    }

    public void SeedSms(string id, string phone, long now) => Execute("""
        INSERT dbo.Customer_Otp_Challenges(id,phone,purpose,created_at,expires,delivery_status,code_hash)
        VALUES(@p0,@p1,'register',@p2,@p3,'sent',@p4)
        """, id, phone, now, now + 120000, new string('c', 64)).GetAwaiter().GetResult();

    private static IReadOnlyDictionary<string, string> ReadChecks()
    {
        var checks = new Dictionary<string, string>();
        foreach (var file in new[] { "002_customer_otp.sql", "003_unified_authentication.sql" })
        {
            var ddl = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema", file));
            foreach (Match match in Regex.Matches(ddl, @"CONSTRAINT\s+(CK_Otp\w+)\s+CHECK\s*\(([^\r\n]+)\)"))
                checks[match.Groups[1].Value] = Regex.Replace(match.Groups[2].Value,
                    @"(\w+)\s+BETWEEN\s+(\d+)\s+AND\s+(\d+)", "$1 >= $2 AND $1 <= $3");
        }
        if (!checks.ContainsKey("CK_OtpDelivery") || !checks.ContainsKey("CK_OtpPurpose") || !checks.ContainsKey("CK_OtpAttempts"))
            throw new InvalidOperationException("Missing OTP schema constraint fixtures.");
        return checks;
    }

    private static string Bind(string expression, object?[] values) => Regex.Replace(expression, @"@p(\d+)\b", match =>
    {
        var value = values[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)];
        return value switch { null => "NULL", string text => "'" + text.Replace("'", "''") + "'", _ => Convert.ToString(value, CultureInfo.InvariantCulture)! };
    });

    private static object? Value(string expression, object?[] values) => expression.StartsWith("@p", StringComparison.Ordinal)
        ? values[int.Parse(expression[2..], CultureInfo.InvariantCulture)]
        : expression == "NULL" ? null : expression.StartsWith('\'') ? expression[1..^1].Replace("''", "'")
        : long.Parse(expression, CultureInfo.InvariantCulture);

    private static SqlException CheckViolation(string constraint)
    {
        // SqlClient has no public exception constructor. Build an explicitly simulated
        // SQL 547 so the real API middleware/logging path is exercised without DML.
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).OrderBy(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select(parameter => parameter.ParameterType == typeof(int) ? (object)(parameter.Position == 0 ? 547 : 0)
            : parameter.ParameterType == typeof(byte) ? (byte)(parameter.Name!.Contains("class", StringComparison.OrdinalIgnoreCase) ? 16 : 0)
            : parameter.ParameterType == typeof(uint) ? 0U
            : parameter.ParameterType == typeof(string) ? parameter.Name!.Contains("message", StringComparison.OrdinalIgnoreCase)
                ? "Simulated SQL CHECK violation: " + constraint : "isolated"
            : null).ToArray();
        var error = (SqlError)constructor.Invoke(arguments);
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(collection, [error]);
        var factory = typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name == "CreateException" && method.GetParameters()[0].ParameterType == typeof(SqlErrorCollection))
            .OrderBy(method => method.GetParameters().Length).First();
        var factoryArgs = factory.GetParameters().Select(parameter => parameter.Position == 0 ? (object)collection
            : parameter.ParameterType == typeof(string) ? "16.0.0"
            : parameter.ParameterType == typeof(Guid) ? Guid.Empty : null).ToArray();
        return (SqlException)factory.Invoke(null, factoryArgs)!;
    }
}
