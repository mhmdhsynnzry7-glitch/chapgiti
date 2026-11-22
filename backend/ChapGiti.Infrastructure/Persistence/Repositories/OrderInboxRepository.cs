using ChapGiti.Application.Orders;
using Microsoft.EntityFrameworkCore;
namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class OrderInboxRepository(SqlStore db) : IOrderInboxRepository
{
    internal IQueryable<OrderInboxMatch> Matches(InboxQuery request)
    {
        var admin = request.Recipient.Kind=="admin";
        var actor = request.Recipient.Id;
        var facts=db.Context.Database.SqlQuery<OrderInboxFacts>($"""
            SELECT o.id Id,o.code Code,o.customer Customer,o.phone Phone,o.service Service,o.title Title,o.status Status,
              o.created_at CreatedAt,o.updated_at UpdatedAt,o.estimate Estimate,o.final_price FinalPrice,o.information_request InformationRequest,COALESCE(o.information_requires_file,CONVERT(bit,0)) InformationRequiresFile,
              q.state QuoteState,q.amount_toman QuoteAmount,q.revision QuoteRevision,c.state Correction,
              CONVERT(bit,CASE WHEN o.approved_file_id IS NOT NULL THEN 1 ELSE 0 END) FileApproved,
              CONVERT(bit,CASE WHEN terms.issued=1 THEN 1 ELSE 0 END) TermsIssued,
              CONVERT(bit,CASE WHEN terms.issued=1 AND terms.accepted IS NOT NULL THEN 1 ELSE 0 END) TermsAccepted,
              CONVERT(bit,CASE WHEN notice.seq>COALESCE(v.seen_through_sequence,0) AND notice.seq>COALESCE(baseline.seq,0) THEN 1 ELSE 0 END) Unread
            FROM dbo.Orders o
            OUTER APPLY (SELECT TOP(1) state,amount_toman,revision,id FROM dbo.Order_Quotes WHERE order_id=o.id ORDER BY revision DESC) q
            OUTER APPLY (SELECT TOP(1) state,id FROM dbo.Order_Corrections WHERE order_id=o.id ORDER BY revision DESC) c
            OUTER APPLY (SELECT TOP(1) j.[value] doc FROM OPENJSON(o.spec,'$.deliveryConditions') j ORDER BY CONVERT(int,j.[key]) DESC) lastTerms
            OUTER APPLY (SELECT 1 issued,JSON_VALUE(lastTerms.doc,'$.acceptedAt') accepted
               WHERE JSON_VALUE(lastTerms.doc,'$.supersededAt') IS NULL AND JSON_VALUE(lastTerms.doc,'$.fileId')=o.approved_file_id
               AND JSON_VALUE(lastTerms.doc,'$.quoteId')=q.id AND COALESCE(JSON_VALUE(lastTerms.doc,'$.correctionId'),'')=COALESCE(c.id,'')) terms
            OUTER APPLY (SELECT MAX(n.sequence) seq FROM dbo.Notifications n WHERE n.entity_kind='order' AND n.entity_id=o.id
               AND (({admin}=1 AND n.admin_email={actor}) OR ({admin}=0 AND n.customer_id={actor}))) notice
            LEFT JOIN dbo.Order_Views v ON v.order_id=o.id AND (({admin}=1 AND v.admin_email={actor}) OR ({admin}=0 AND v.customer_id={actor}))
            OUTER APPLY (SELECT TRY_CONVERT(bigint,[value]) seq FROM dbo.Settings WHERE [key]='order_seen_baseline_sequence') baseline
            WHERE {admin}=1 OR o.customer_id={actor}
            """);
        return OrderInbox.Query(facts,request);
    }

    public async Task<OrderInboxPage> Page(InboxQuery request)
    {
        var rows=Matches(request);
        long? total=request.Id == null ? await rows.LongCountAsync() : null;
        var sorted=request.Oldest?rows.OrderBy(r=>r.Fact.CreatedAt).ThenBy(r=>r.Fact.Id):rows.OrderByDescending(r=>r.Fact.CreatedAt).ThenByDescending(r=>r.Fact.Id);
        var page=await sorted.Skip(request.Skip).Take(request.Take).ToListAsync();
        return new(page.Select(x=>OrderInbox.Present(x,request.Recipient.Kind)).ToList(),total ?? page.Count);
    }
}
