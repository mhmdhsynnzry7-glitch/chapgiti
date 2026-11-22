using System.Text.Json.Nodes;
using ChapGiti.Application.Abstractions;
using ChapGiti.Infrastructure.Persistence.Ef;
using ChapGiti.Infrastructure.Persistence.Ef.Models;
using Microsoft.EntityFrameworkCore;
namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class CutTariffRepository(SqlStore db) : ICutTariffRepository
{
    public async Task<bool> Available() =>
        await db.One("SELECT OBJECT_ID('dbo.Cut_Tariff_Versions','U') AS id") is { } row && row["id"] != null;

    public async Task<CutTariffRevision?> Latest(string cutId)
    {
        var row = await db.Context.Set<CutTariffVersion>().AsNoTracking().Where(r => r.CutId == cutId)
            .OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        return row == null ? null : new(cutId, row.Version, EfRows.Stamp(row.UpdatedAt)!, JsonNode.Parse(row.Rates)!.AsObject());
    }

    public async Task Append(CutTariffRevision revision) => await db.Insert(new CutTariffVersion
    {
        CutId = revision.CutId, Version = revision.Version, UpdatedAt = EfRows.Date(revision.UpdatedAt), Rates = revision.Rates.ToJsonString()
    });
}
