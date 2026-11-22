using System.Text.Json.Nodes;

namespace ChapGiti.Infrastructure.Content;

public sealed class ContentDefaults(IHostEnvironment environment) : IContentDefaults
{
    public JsonObject Root
    {
        get;
    } = JsonNode.Parse(
        File.ReadAllText(Path.Combine(environment.ContentRootPath, "Data", "defaults.json")))!.AsObject();

    public JsonNode Copy(string key) => Root[key]!.DeepClone();
}
