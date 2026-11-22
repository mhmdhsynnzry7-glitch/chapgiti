using Microsoft.Data.SqlClient;

namespace ChapGiti.Infrastructure.Persistence;

// Only for standalone, idempotent notification queries/updates. SQL rolls back the
// deadlock victim; retrying inside a business transaction would be unsafe.
internal static class SqlDeadlockRetry
{
    public static async Task<T> Run<T>(Func<Task<T>> operation, bool inTransaction)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await operation(); }
            catch (SqlException error) when (error.Number == 1205 && !inTransaction && attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)));
            }
        }
    }
}
