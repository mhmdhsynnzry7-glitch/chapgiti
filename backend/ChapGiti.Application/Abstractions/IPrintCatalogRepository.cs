using ChapGiti.Application.Catalog;

namespace ChapGiti.Application.Abstractions;

public interface IPrintCatalogRepository
{
    Task<bool> Available();
    Task<IReadOnlyList<PrintCatalogItem>> List(bool includeInactive);
    Task<PrintCatalogItem?> Lock(string id);
    Task LockKind(string kind);
    Task<bool> CodeExists(string kind,string code,string? exceptId);
    Task ClearDefault(string kind,string exceptId,string now);
    Task<bool> IsReferenced(PrintCatalogItem item);
    Task<bool> Delete(string id, int version);
    Task<bool> Save(PrintCatalogItem item,int? expectedVersion);
}
