namespace ChapGiti.Application.Abstractions;

public interface IContentRepository
{
    Task<Dictionary<string, object?>?> ReadSetting(string key);
    Task<int> SaveSetting(string key, string json);
    Task<Dictionary<string, object?>?> ReadRates();
}
