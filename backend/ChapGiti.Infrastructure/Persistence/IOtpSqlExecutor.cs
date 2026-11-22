namespace ChapGiti.Infrastructure.Persistence;

// Narrow infrastructure seam for executing the OTP repository's parameterized commands.
internal interface IOtpSqlExecutor
{
    Task<Dictionary<string, object?>?> One(string sql, params object?[] values);
    Task<int> Execute(string sql, params object?[] values);
}
