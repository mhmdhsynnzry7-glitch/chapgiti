import { formatToman } from "@/lib/money";
import { date } from "@/lib/pricing";
import type { OrderQuote } from "@/lib/api-contracts";
const states: Record<string,string> = {sent:"منتظر تصمیم مشتری",accepted:"تأییدشده توسط مشتری",revision_requested:"درخواست اصلاح",declined:"انصراف مشتری",superseded:"نسخهٔ جایگزین‌شده"};
export default function QuoteHistory({ quotes }: { quotes: OrderQuote[] }) {
  if (!quotes.length) return null;
  return <section className="quote-history" aria-label="پیشنهادهای قیمت چاپخانه"><h3>پیشنهاد قیمت چاپخانه</h3>
    {[...quotes].reverse().map(quote=><article className="order-quote" key={quote.id}>
      <div className="panel-title"><h4>پیشنهاد نسخهٔ {quote.revision.toLocaleString("fa-IR")}</h4><span>{states[quote.state] || quote.state}</span></div>
      <p className="proposal-amount">{formatToman(quote.amount_toman)}</p>
      <dl><div><dt>خدمات شامل قیمت</dt><dd>{quote.scope_text}</dd></div>{quote.delivery_terms && <div><dt>شرایط تحویل</dt><dd>{quote.delivery_terms}</dd></div>}{quote.exclusions_text && <div><dt>خارج از قیمت</dt><dd>{quote.exclusions_text}</dd></div>}</dl>
      <time dateTime={quote.sent_at}>ارسال: {date(quote.sent_at)}</time>
      {quote.accepted_at && <p>تأیید مشتری: <time dateTime={quote.accepted_at}>{date(quote.accepted_at)}</time></p>}
      {quote.decision_reason && <p>توضیح تصمیم: {quote.decision_reason}</p>}
    </article>)}
  </section>;
}
