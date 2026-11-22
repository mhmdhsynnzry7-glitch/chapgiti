using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class ContentRepository(SqlStore db) : IContentRepository
{
    public Task<Dictionary<string,object?>?> ReadSetting(string key) =>
        db.Context.Settings.AsNoTracking().Where(s=>s.Key==key).Select(s=>new{s.Value}).FirstRow(db.Context);
    public Task<int> SaveSetting(string key,string json) => db.Transaction(async()=>{
        // A range lock keeps create/update atomic even for previously absent settings.
        var existing=await db.One("SELECT [key] FROM dbo.Settings WITH(UPDLOCK,HOLDLOCK) WHERE [key]=@p0",key);
        return existing==null?await db.Insert(new Models.Setting{Key=key,Value=json}):
            await db.Context.Settings.Where(s=>s.Key==key).ExecuteUpdateAsync(set=>set.SetProperty(s=>s.Value,json));
    });
    public Task<Dictionary<string,object?>?> ReadRates()=>ReadSetting("rates");
}
