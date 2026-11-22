using ChapGiti.Application.Notifications;
namespace ChapGiti.Application.Orders;

public sealed class OrderInboxFacts
{
    public string Id {get;set;} = "";
    public string Code {get;set;} = "";
    public string Customer {get;set;} = "";
    public string Phone {get;set;} = "";
    public string Service {get;set;} = "";
    public string Title {get;set;} = "";
    public string Status {get;set;} = "";
    public DateTime CreatedAt {get;set;}
    public DateTime UpdatedAt {get;set;}
    public long Estimate {get;set;}
    public long? FinalPrice {get;set;}
    public string? QuoteState {get;set;}
    public long? QuoteAmount {get;set;}
    public int? QuoteRevision {get;set;}
    public string? Correction {get;set;}
    public bool InformationRequiresFile {get;set;}
    public string? InformationRequest {get;set;}
    public bool TermsIssued {get;set;}
    public bool TermsAccepted {get;set;}
    public bool FileApproved {get;set;}
    public bool Unread {get;set;}
}
public sealed record InboxQuery(NotificationRecipient Recipient,string Statuses,string Search,string Attention,int Skip,int Take,bool Oldest=false,string? Id=null);
public sealed record OrderInboxPage(List<Dictionary<string,object?>> Items,long Total);
public interface IOrderInboxRepository { Task<OrderInboxPage> Page(InboxQuery request); }

public sealed class OrderInboxMatch
{
    public OrderInboxFacts Fact {get;set;} = null!;
    public string Action {get;set;} = "";
}
public static class OrderInbox
{
    private static readonly string[] CustomerActions = ["submit-file","supply-information","accept-price","accept-terms"];
    private static readonly Dictionary<string,string> Labels = new()
    {
        ["new"]="بررسی درخواست جدید", ["review"]="بررسی فایل و پیشنهاد قیمت", ["submit-file"]="ارسال نسخهٔ اصلاح‌شده",
        ["supply-information"]="تکمیل اطلاعات درخواستی", ["review-file"]="بررسی اصلاحیهٔ دریافتی", ["revision"]="بازنگری پیشنهاد قیمت", ["accept-price"]="بررسی و تأیید قیمت",
        ["approve-file"]="بررسی و تأیید نسخهٔ جاری فایل", ["issue-terms"]="اعلام شرایط چاپ و تحویل", ["accept-terms"]="بررسی شرایط نهایی تحویل",
        ["produce"]="بررسی آمادگی و شروع تولید", ["production"]="ثبت مرحلهٔ بعدی تولید", ["deliver"]="ثبت تحویل سفارش", ["none"]="اقدامی لازم نیست"
    };

    // This projection is translated to SQL before filtering/pagination and is also used by isolated tests.
    public static IQueryable<OrderInboxMatch> Query(IQueryable<OrderInboxFacts> facts,InboxQuery request)
    {
        var stages = request.Statuses.Split(',',StringSplitOptions.RemoveEmptyEntries);
        facts=facts.Where(f=>(request.Id==null || f.Id==request.Id) && (stages.Length==0 || stages.Contains(f.Status)));
        var trackingSearch=request.Search.ToUpperInvariant();
        if (request.Search!="") facts=facts.Where(f=>f.Code.Contains(trackingSearch) || f.Title.Contains(request.Search) || f.Customer.Contains(request.Search) || f.Phone.Contains(request.Search));
        var rows=facts.Select(f=>new OrderInboxMatch {Fact=f,Action=
            f.Status=="completed" || f.Status=="cancelled" ? "none" :
            f.Status=="printing" || f.Status=="binding" ? "production" : f.Status=="ready" ? "deliver" :
            f.Correction=="submitted" ? "review-file" :
            f.Correction=="requested" ? "submit-file" :
            f.InformationRequest!=null && f.InformationRequest!="" ? (f.InformationRequiresFile ? "submit-file" : "supply-information") :
            f.Correction=="rejected" ? "review" :
            f.Status=="awaiting_customer_approval" ? "accept-price" :
            f.QuoteState=="revision_requested" ? "revision" :
            f.Status=="new" ? "new" :
            f.QuoteState!="accepted" && f.Status!="confirmed" ? "review" :
            !f.FileApproved ? "approve-file" : !f.TermsIssued ? "issue-terms" : !f.TermsAccepted ? "accept-terms" : "produce" });
        var attention=request.Attention=="mine"?request.Recipient.Kind:request.Attention;
        return attention switch {
            ""=>rows,"new"=>rows.Where(x=>x.Fact.Status=="new"),"unread"=>rows.Where(x=>x.Fact.Unread),
            "customer"=>rows.Where(x=>CustomerActions.Contains(x.Action)),
            "admin"=>rows.Where(x=>x.Action!="none" && !CustomerActions.Contains(x.Action)),
            _=>throw new UseCaseException(ErrorKind.Invalid,"فیلتر اقدام سفارش معتبر نیست.")
        };
    }
    public static object Attention(OrderInboxMatch row,string scope)
    {
        var actor=row.Action=="none"?"none":CustomerActions.Contains(row.Action)?"customer":"admin";
        return new {code=row.Action,actor,label=Labels[row.Action],required=actor==scope,unread=row.Fact.Unread};
    }
    public static Dictionary<string,object?> Present(OrderInboxMatch row,string scope)
    {
        var f=row.Fact;
        var result=new Dictionary<string,object?> {
            ["id"]=f.Id,["code"]=f.Code,["title"]=f.Title,["service"]=f.Service,["status"]=f.Status,
            ["created_at"]=DateTime.SpecifyKind(f.CreatedAt,DateTimeKind.Utc),["updated_at"]=DateTime.SpecifyKind(f.UpdatedAt,DateTimeKind.Utc),
            ["estimate"]=f.Estimate,["final_price"]=f.FinalPrice,["information_request"]=f.InformationRequest,
            ["quote_state"]=f.QuoteState,["quote_amount"]=f.QuoteAmount,["quote_revision"]=f.QuoteRevision,["attention"]=Attention(row,scope)
        };
        if(scope=="admin"){result["phone"]=f.Phone;result["customer"]=f.Customer;}
        return result;
    }
}
