using ChapGiti.Application.Common;
using ChapGiti.Application.Features;
using ChapGiti.Application.Orders;

namespace ChapGiti.AuthenticationTests;

internal sealed class MemoryOrderInbox(MemoryApiData data) : IOrderInboxRepository
{
    public async Task<OrderInboxPage> Page(InboxQuery request)
    {
        if (request.Id == null && data.FailNextOrderList)
        {
            data.FailNextOrderList = false;
            throw new UseCaseException(ErrorKind.Unavailable, "دریافت سفارش‌ها موقتاً امکان‌پذیر نیست.");
        }
        if (request.Id == null && data.OrderListDelayMs > 0) await Task.Delay(data.OrderListDelayMs);
        var recipient = request.Recipient;
        var views = new MemoryOrderViewRepository(data);
        long.TryParse(data.Settings.GetValueOrDefault("order_seen_baseline_sequence"), out var baseline);
        var facts = new List<OrderInboxFacts>();
        foreach (var row in data.Orders.Values.Where(r => recipient.Kind == "admin" || r.Str("customer_id") == recipient.Id))
        {
            var id = row.Str("id");
            var correction = data.CorrectionRows.Values.Where(c => c.OrderId == id).MaxBy(c => c.Revision);
            var quote = data.QuoteRows.Values.Where(q => q.Str("order_id") == id).MaxBy(q => q.Num("revision"));
            var terms = string.IsNullOrEmpty(row.Str("spec")) ? null : OrderProductionReadiness.CurrentConditions(row, correction);
            var viewed = data.OrderViews.GetValueOrDefault((id, recipient.Kind, recipient.Id));
            DateTime.TryParse(row.Str("created_at"), out var created);
            DateTime.TryParse(row.Str("updated_at"), out var updated);
            facts.Add(new() {
                Id=id, Code=row.Str("code"), Title=row.Str("title"), Customer=row.Str("customer"), Phone=row.Str("phone"),
                Service=row.Str("service"), Status=row.Str("status"), CreatedAt=created, UpdatedAt=updated==default ? created : updated,
                Estimate=row.Num("estimate"), FinalPrice=row.GetValueOrDefault("final_price")==null ? null : row.Num("final_price"),
                InformationRequiresFile=Convert.ToBoolean(row.GetValueOrDefault("information_requires_file")),
                InformationRequest=row.GetValueOrDefault("information_request")?.ToString(), Correction=correction?.State,
                QuoteState=quote?.Str("state"), QuoteRevision=quote==null ? null : (int)quote.Num("revision"), QuoteAmount=quote?.Num("amount_toman"),
                FileApproved=row.GetValueOrDefault("approved_file_id")!=null, TermsIssued=terms!=null, TermsAccepted=terms?["acceptedAt"]!=null,
                Unread=await views.LatestSequence(id, recipient)>Math.Max(viewed, baseline)
            });
        }
        var matches=OrderInbox.Query(facts.AsQueryable(), request);
        var sorted=request.Oldest ? matches.OrderBy(x=>x.Fact.CreatedAt).ThenBy(x=>x.Fact.Id) : matches.OrderByDescending(x=>x.Fact.CreatedAt).ThenByDescending(x=>x.Fact.Id);
        return new(sorted.Skip(request.Skip).Take(request.Take).AsEnumerable().Select(x=>OrderInbox.Present(x, recipient.Kind)).ToList(), matches.LongCount());
    }
}
