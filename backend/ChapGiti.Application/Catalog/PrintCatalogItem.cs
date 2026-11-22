using System.Text.Json.Nodes;

namespace ChapGiti.Application.Catalog;

public sealed record PrintCatalogItem(string Id,string Kind,string Code,string Label,string PricingRule,JsonObject Metadata,int DisplayOrder,bool Active,bool IsDefault,int Version,string CreatedAt,string UpdatedAt);
public sealed record PrintCatalogSnapshot(IReadOnlyList<PrintCatalogItem> Items);
