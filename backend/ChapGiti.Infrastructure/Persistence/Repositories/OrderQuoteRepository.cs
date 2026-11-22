using ChapGiti.Infrastructure.Persistence.Ef;
using Microsoft.EntityFrameworkCore;
using Models = ChapGiti.Infrastructure.Persistence.Ef.Models;

namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class OrderQuoteRepository(SqlStore db) : IOrderQuoteRepository
{
    public Task<bool> SupportsQuoteWorkflow() => db.SupportsQuoteWorkflow();
    public Task<List<Dictionary<string,object?>>> Quotes(string orderId) =>
        db.Context.OrderQuotes.AsNoTracking().Where(q=>q.OrderId==orderId).OrderBy(q=>q.Revision).Rows(db.Context);
    // Receipt lookup must retain a serializable range lock for same-key concurrent requests.
    public Task<Dictionary<string,object?>?> LockCommand(string id) =>
        db.One("SELECT * FROM dbo.Order_Commands WITH(UPDLOCK,HOLDLOCK) WHERE id=@p0",id);
    public async Task InsertCommand(string id,string orderId,string actor,string kind,string hash,string now) =>
        await db.Insert(new Models.OrderCommand{Id=id,OrderId=orderId,Actor=actor,Kind=kind,PayloadHash=hash,CreatedAt=EfRows.Date(now)});
    public async Task InsertQuote(string id,string orderId,int revision,long amount,string scope,string delivery,string exclusions,string admin,string now) =>
        await db.Insert(new Models.OrderQuote{Id=id,OrderId=orderId,Revision=revision,AmountToman=amount,ScopeText=scope,DeliveryTerms=delivery,
            ExclusionsText=exclusions,CreatedByAdmin=admin,SentAt=EfRows.Date(now),State="sent",DecisionReason="",Version=1});
    public Task<int> DecideQuote(string id,string state,string reason,string? customer,string now,long version)
    {
        var at=EfRows.Date(now);
        return db.Context.OrderQuotes.Where(q=>q.Id==id&&q.State=="sent"&&q.Version==version).ExecuteUpdateAsync(set=>set
            .SetProperty(q=>q.State,state).SetProperty(q=>q.DecisionReason,reason).SetProperty(q=>q.DecidedAt,at)
            .SetProperty(q=>q.AcceptedAt,state=="accepted"?(DateTime?)at:null).SetProperty(q=>q.AcceptedByCustomer,state=="accepted"?customer:null)
            .SetProperty(q=>q.Version,q=>q.Version+1));
    }
    public async Task SetInformation(string orderId,string? message,bool requiresFile) =>
        await db.Context.Orders.Where(o=>o.Id==orderId).ExecuteUpdateAsync(set=>set.SetProperty(o=>o.InformationRequest,message).SetProperty(o=>o.InformationRequiresFile,requiresFile));
    public async Task AdoptProposalWorkflow(string orderId) =>
        await db.Context.Orders.Where(o=>o.Id==orderId).ExecuteUpdateAsync(set=>set.SetProperty(o=>o.WorkflowVersion,(byte)2));
}
