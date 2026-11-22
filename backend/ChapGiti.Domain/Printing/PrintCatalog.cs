using System.Text.Json.Nodes;
namespace ChapGiti.Domain.Printing;

public sealed class PrintCatalog(JsonObject options)
{
    public JsonObject Options { get; } = (JsonObject)options.DeepClone();
}
