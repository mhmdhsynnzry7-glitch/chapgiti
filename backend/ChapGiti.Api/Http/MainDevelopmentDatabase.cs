using Microsoft.Data.SqlClient;

namespace ChapGiti.Api.Http;

internal static class MainDevelopmentDatabase
{
    // Validate before opening a connection, then bind the same local target to Shared Memory.
    internal static bool TryPin(string? connectionString, out string pinned)
    {
        pinned = "";
        try
        {
            var sql = new SqlConnectionStringBuilder(connectionString ?? "");
            var host = sql.DataSource;
            if (host.StartsWith("lpc:", StringComparison.OrdinalIgnoreCase)) host = host[4..];
            string[] localNames = [".", "(local)", "localhost", Environment.MachineName];
            if (!localNames.Contains(host, StringComparer.OrdinalIgnoreCase) || sql.InitialCatalog != "ChapGiti" ||
                sql.UserInstance || sql.AttachDBFilename.Length != 0 || sql.FailoverPartner.Length != 0)
                return false;
            sql.DataSource = "lpc:.";
            pinned = sql.ConnectionString;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or NotSupportedException)
        {
            return false;
        }
    }
}
