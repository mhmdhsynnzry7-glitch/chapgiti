namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

public sealed class PrintCatalogOption
{
    public string Id { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string PricingRule { get; set; } = null!;
    public string Metadata { get; set; } = null!;
    public int DisplayOrder
    {
        get; set;
    }
    public bool Active
    {
        get; set;
    }
    public bool IsDefault
    {
        get; set;
    }
    public int Version
    {
        get; set;
    }
    public DateTime CreatedAt
    {
        get; set;
    }
    public DateTime UpdatedAt
    {
        get; set;
    }
}
