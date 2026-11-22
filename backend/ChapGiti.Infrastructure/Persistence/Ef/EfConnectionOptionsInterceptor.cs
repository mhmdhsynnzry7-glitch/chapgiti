using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ChapGiti.Infrastructure.Persistence.Ef;

// SQL004's persisted effective-email index requires these options on every EF-opened connection.
internal sealed class EfConnectionOptionsInterceptor : DbConnectionInterceptor
{
    public static readonly EfConnectionOptionsInterceptor Instance=new();
    private const string Options="SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET QUOTED_IDENTIFIER ON; SET NUMERIC_ROUNDABORT OFF;";
    public override void ConnectionOpened(DbConnection connection,ConnectionEndEventData eventData)
    {using var command=connection.CreateCommand();command.CommandText=Options;command.ExecuteNonQuery();}
    public override async Task ConnectionOpenedAsync(DbConnection connection,ConnectionEndEventData eventData,CancellationToken cancellationToken=default)
    {await using var command=connection.CreateCommand();command.CommandText=Options;await command.ExecuteNonQueryAsync(cancellationToken);}
}
