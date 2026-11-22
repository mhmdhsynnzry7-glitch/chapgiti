"use client";
import { DateSort, PageSizeControl } from "./management-table";
import TicketTable from "./ticket-table";
import Pagination from "./pagination";
import { useState } from "react";
import Link from "next/link";
import TicketThread from "./ticket-thread";
import { useApiResource } from "@/hooks/use-api-resource";
import { ticketStatuses, type TicketPage, type Ticket, type TicketMessage } from "@/lib/tickets";
function Conversation({ id }: { id: string }) {
  const { data, loading, error, reload } = useApiResource<{ ticket: Ticket; messages: TicketMessage[] }>("admin/tickets/" + id);
  return <>{loading && <p role="status">در حال دریافت گفتگو…</p>}{error && <p role="alert" className="error-message">{error}<button onClick={reload}>تلاش دوباره</button></p>}{data && <TicketThread key={id} initial={data} admin />}</>;
}
export default function AdminTickets({ initialId }: { initialId?: string }) {
  const [oldestFirst,setOldestFirst]=useState(false);
  const [pageSize,setPageSize]=useState(20);
  const [query, setQuery] = useState(""), [search, setSearch] = useState(""), [status, setStatus] = useState(""), [page, setPage] = useState(1);
  const { data, loading, error, reload } = useApiResource<TicketPage>("admin/tickets?" + new URLSearchParams({ q: search, status, page: String(page), pageSize: String(pageSize), oldestFirst:String(oldestFirst) }), true);
  if (initialId) return <><Link className="text-link" prefetch={false} href="/admin/tickets">بازگشت به فهرست</Link><Conversation id={initialId} /></>;
  return <><div className="admin-heading"><div><h2>تیکت و پشتیبانی</h2><p>گفتگوهای مشتریان و پیگیری سفارش‌ها</p></div></div>
    <form className="user-filters" onSubmit={event => { event.preventDefault(); setSearch(query); setPage(1); }}>
      <DateSort oldest={oldestFirst} change={v=>{setOldestFirst(v);setPage(1);}}/>
          <label className="field">جست‌وجوی مشتری یا موضوع<input value={query} maxLength={100} onChange={event => setQuery(event.target.value)} /></label>
      <label className="field">وضعیت گفتگو<select value={status} onChange={event => { setStatus(event.target.value); setPage(1); }}><option value="">همه گفتگوها</option>{Object.entries(ticketStatuses).map(([value,label]) => <option key={value} value={value}>{label}</option>)}</select></label><button className="button button-outline">جست‌وجو</button>
    </form>
    {loading && <p role="status">در حال دریافت گفتگوها…</p>}{error && <p role="alert" className="error-message">{error}<button onClick={reload}>تلاش دوباره</button></p>}
    {data && <><TicketTable admin items={data.items} busy={loading} reload={reload}/><PageSizeControl value={pageSize} change={n=>{setPageSize(n);setPage(1);}}/>
          <Pagination className="user-pagination" page={page} pageSize={data.pageSize} total={data.total} noun="گفتگو" loading={loading} onPageChange={setPage} /></>}
  </>;
}
