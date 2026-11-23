namespace ChapGiti.Infrastructure.Persistence.Ef.Models;

internal sealed class CutTariffVersion
{
    public required string CutId { get; set; }
    public bool IsProposed { get; set; }
    public int Version { get; set; }
    public DateTime UpdatedAt { get; set; }
    public required string Rates { get; set; }
}
