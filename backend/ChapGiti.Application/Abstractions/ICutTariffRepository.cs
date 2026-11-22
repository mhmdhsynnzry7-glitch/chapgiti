namespace ChapGiti.Application.Abstractions;

public sealed record CutTariffRevision(string CutId, int Version, string UpdatedAt, JsonObject Rates);

public interface ICutTariffRepository
{
    Task<bool> Available();
    Task<CutTariffRevision?> Latest(string cutId);
    Task Append(CutTariffRevision revision);
}
