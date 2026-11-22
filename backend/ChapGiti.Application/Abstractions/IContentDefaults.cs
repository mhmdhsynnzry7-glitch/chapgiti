using System.Text.Json.Nodes;
namespace ChapGiti.Application.Abstractions;

public interface IContentDefaults
{
    JsonObject Root
    {
        get;
    }
    JsonNode Copy(string key);
}
