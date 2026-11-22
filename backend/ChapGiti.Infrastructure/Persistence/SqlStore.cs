using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using ChapGiti.Application.Realtime;
using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace ChapGiti.Infrastructure.Persistence;

// One scoped EF context owns the connection/UoW. The command adapter remains only for
// reviewed SQL Server locks, atomic output/JSON operations and immutable legacy migrations.
internal sealed class SqlStore(IConfiguration config, CommittedUpdates? updates = null, ChapGitiDbContext? context = null) : IAsyncDisposable, IUnitOfWork, IOtpSqlExecutor
{
    public ChapGitiDbContext Context { get; } = context ?? new(new DbContextOptionsBuilder<ChapGitiDbContext>()
        .UseSqlServer(config.GetConnectionString("SqlServer"), sql => sql.UseCompatibilityLevel(150)).AddInterceptors(EfConnectionOptionsInterceptor.Instance).Options);
    private IDbContextTransaction? transaction;
    private bool? administratorEmailSupported;
    private bool? supportOrderLinks;
    private bool? orderNotes;
    private bool? quoteWorkflow;
    private bool? notifications;
    private bool? corrections;
    private bool? orderViews;
    public async Task<bool> SupportsCorrections() => corrections ??= (await One("SELECT CASE WHEN OBJECT_ID('dbo.Order_Corrections') IS NOT NULL AND COL_LENGTH('dbo.Orders','approved_file_id') IS NOT NULL AND COL_LENGTH('dbo.Files','correction_id') IS NOT NULL AND COL_LENGTH('dbo.Files','file_version') IS NOT NULL THEN 1 ELSE 0 END supported"))!.Num("supported") == 1;
    public async Task<bool> SupportsOrderViews() => orderViews ??= (await One("SELECT CASE WHEN OBJECT_ID('dbo.Order_Views') IS NOT NULL THEN 1 ELSE 0 END supported"))!.Num("supported") == 1;
    public bool InTransaction => Context.Database.CurrentTransaction != null;
    public async Task<bool> SupportsNotifications() => notifications ??= (await One("SELECT CASE WHEN OBJECT_ID('dbo.Notifications') IS NOT NULL THEN 1 ELSE 0 END supported"))!.Num("supported") == 1;
    public async Task<bool> SupportsQuoteWorkflow() => quoteWorkflow ??= (await One("SELECT CASE WHEN OBJECT_ID('dbo.Order_Quotes') IS NOT NULL AND OBJECT_ID('dbo.Order_Commands') IS NOT NULL AND COL_LENGTH('dbo.Orders','workflow_version') IS NOT NULL THEN 1 ELSE 0 END supported"))!.Num("supported") == 1;
    public async Task<bool> SupportsOrderLinks() => supportOrderLinks ??= (await One("SELECT CASE WHEN COL_LENGTH('dbo.Tickets','order_id') IS NULL THEN 0 ELSE 1 END supported"))!.Num("supported") == 1;
    public async Task<bool> SupportsOrderNotes() => orderNotes ??= (await One("SELECT CASE WHEN OBJECT_ID('dbo.Order_Notes') IS NULL THEN 0 ELSE 1 END supported"))!.Num("supported") == 1;
    public async Task<bool> SupportsAdministratorEmail()
    {
        if (administratorEmailSupported.HasValue) return administratorEmailSupported.Value;
        var row = await One("""
            SELECT CASE WHEN COL_LENGTH('dbo.Admin_Users','login_email') IS NOT NULL
                AND COL_LENGTH('dbo.Admin_Users','effective_email') IS NOT NULL THEN 1 ELSE 0 END supported
            """);
        administratorEmailSupported = row!.Num("supported") == 1;
        return administratorEmailSupported.Value;
    }
    public string ConnectionString => config.GetConnectionString("SqlServer") ?? throw new InvalidOperationException("SQL Server connection is missing.");
    public async Task<SqlConnection> Connection()
    {
        var connection = (SqlConnection)Context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await Context.Database.OpenConnectionAsync();
            // Required for the unique computed effective-email index (migration 004).
            using var options = connection.CreateCommand();
            options.CommandText = """
                SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
                SET CONCAT_NULL_YIELDS_NULL ON; SET QUOTED_IDENTIFIER ON; SET NUMERIC_ROUNDABORT OFF;
                """;
            await options.ExecuteNonQueryAsync();
        }
        return connection;
    }
    private async Task<SqlCommand> Command(string sql, object?[] values)
    {
        var cmd = (await Connection()).CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = Context.Database.CurrentTransaction == null ? null : (SqlTransaction)Context.Database.CurrentTransaction.GetDbTransaction();
        cmd.CommandTimeout = 30;
        for (int i = 0; i < values.Length; i++)
        {
            var value = values[i];
            if (value is string text && Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$"))
                value = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            var parameter = new SqlParameter("@p" + i, value ?? DBNull.Value);
            if (value is DateTime)
                parameter.SqlDbType = SqlDbType.DateTime2;
            cmd.Parameters.Add(parameter);
        }
        return cmd;
    }
    public async Task<List<Dictionary<string, object?>>> Query(string sql, params object?[] values)
    {
        using var cmd = await Command(sql, values);
        using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                if (value is DateTime date)
                    value = DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
                row[reader.GetName(i)] = value;
            }
            rows.Add(row);
        }
        return rows;
    }
    public async Task<Dictionary<string, object?>?> One(string sql, params object?[] values) => (await Query(sql, values)).FirstOrDefault();
    public async Task<int> Execute(string sql, params object?[] values)
    {
        using var cmd = await Command(sql, values);
        return await cmd.ExecuteNonQueryAsync();
    }
    public Task<T> Transaction<T>(Func<Task<T>> action) => Transaction(action, IsolationLevel.Serializable);
    private async Task<T> Transaction<T>(Func<Task<T>> action, IsolationLevel isolation)
    {
        if (transaction != null)
            return await action();
        async Task<T> Persist()
        {
            await Connection();
            transaction = await Context.Database.BeginTransactionAsync(isolation);
            try
            {
                var result = await action();
                await transaction.CommitAsync();
                return result;
            }
            catch { await transaction.RollbackAsync(); throw; }
            finally { await transaction.DisposeAsync(); transaction = null; Context.ChangeTracker.Clear(); }
        }
        // Commit and transaction disposal finish before any network delivery begins.
        return updates == null ? await Persist() : await updates.Transaction(Persist);
    }
    public async ValueTask DisposeAsync()
    {
        if (transaction != null)
            await transaction.DisposeAsync();
        if (context == null) await Context.DisposeAsync();
    }
    public async Task<int> Insert<T>(T entity) where T : class
    {
        Context.Add(entity);
        try { return await Context.SaveChangesAsync(); }
        catch (DbUpdateException error) when (error.InnerException is SqlException sql)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(sql).Throw(); throw; }
        finally { Context.ChangeTracker.Clear(); }
    }
}
