using System.Text.Json.Nodes;
using ChapGiti.Application.Abstractions;
using ChapGiti.Domain.Printing;
namespace ChapGiti.AuthenticationTests;

internal sealed class MemoryCutTariffs : ICutTariffRepository
{
    private readonly Dictionary<(string Id,int Version),CutTariffRevision> revisions = new();
    public MemoryCutTariffs(MemoryApiData data)
    {
        var fixture = data.Root["rates"]!.AsObject();
        // These established test fixtures model existing administrator-approved versions.
        data.Settings.TryAdd("rates",fixture.ToJsonString());
        foreach (var cut in data.CatalogItems.Values.Where(i => i.Kind == "bookSizes"))
        {
            var rates = new JsonObject(CutTariff.Keys.Select(k => KeyValuePair.Create(k,fixture[k]?.DeepClone())));
            revisions[(cut.Id,1)] = new(cut.Id,1,"2026-01-01T00:00:00Z",rates);
        }
    }
    public Task<bool> Available() => Task.FromResult(true);
    public Task<CutTariffRevision?> Latest(string id) => Task.FromResult(revisions.Values.Where(row => row.CutId == id).MaxBy(row => row.Version));
    public Task DiscardInitialProposal(string id) { if (revisions.GetValueOrDefault((id,1))?.IsProposed == true) revisions.Remove((id,1)); return Task.CompletedTask; }
    public Task Append(CutTariffRevision row) { revisions.Add((row.CutId,row.Version),row); return Task.CompletedTask; }
}
