using System.Text.Json.Nodes;
using ChapGiti.Application.Abstractions;
using ChapGiti.Domain.Printing;
namespace ChapGiti.AuthenticationTests;

internal sealed class MemoryCutTariffs : ICutTariffRepository
{
    private readonly Dictionary<string,CutTariffRevision> revisions = new();
    public MemoryCutTariffs(MemoryApiData data)
    {
        var fixture = data.Root["rates"]!.AsObject();
        // Only test fixtures have preconfigured prices. Newly created sizes are left unconfigured.
        data.Settings.TryAdd("rates",fixture.ToJsonString());
        foreach (var cut in data.CatalogItems.Values.Where(i => i.Kind == "bookSizes"))
        {
            var rates = new JsonObject(CutTariff.Keys.Select(k => KeyValuePair.Create(k,fixture[k]?.DeepClone())));
            revisions[cut.Id] = new(cut.Id,1,"2026-01-01T00:00:00Z",rates);
        }
    }
    public Task<bool> Available() => Task.FromResult(true);
    public Task<CutTariffRevision?> Latest(string id) => Task.FromResult(revisions.GetValueOrDefault(id));
    public Task Append(CutTariffRevision row) { revisions[row.CutId]=row; return Task.CompletedTask; }
}
