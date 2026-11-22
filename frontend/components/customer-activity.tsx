"use client";
import ManagementTable, { DateSort, PageSizeControl } from "./management-table";
import Pagination from "./pagination";
import Link from "next/link";
import { useState } from "react";
import { useApiResource } from "@/hooks/use-api-resource";
import { statuses } from "@/lib/pricing";
const types: Record<string,string> = { login: "ورود موفق", order_created: "ثبت سفارش", ticket_created: "ثبت درخواست پشتیبانی", order_status: "تغییر وضعیت سفارش" };
type Activity = { id: string; type: string; occurred_at: string; customer_name: string; phone: string; actor: string; order_id: string | null; ticket_id: string | null; order_code: string | null; subject: string | null; status: string | null };
type ActivityPage = { items: Activity[]; total: number; page: number; pageSize: number };
export default function CustomerActivity() {
  const [oldestFirst,setOldestFirst]=useState(false);
  const [query,setQuery] = useState(""), [search,setSearch] = useState(""), [type,setType] = useState(""), [from,setFrom] = useState(""), [until,setUntil] = useState(""), [page,setPage] = useState(1), [pageSize,setPageSize] = useState(20);
  const params = new URLSearchParams({ q: search, type, from, until, page:String(page), pageSize:String(pageSize), oldestFirst:String(oldestFirst) });
  const { data,loading,error,reload } = useApiResource<ActivityPage>("admin/activity?" + params, true);
  return <section><div className="admin-heading"><div><h2>تاریخچه فعالیت کاربران</h2><p>رویدادهای ثبت‌شدهٔ مشتریان؛ تاریخچه مدیریت در بخش جداگانه قرار دارد.</p></div><button className="button button-outline button-sm" onClick={reload}>تازه‌سازی</button></div>
    <form className="activity-filters" onSubmit={event => { event.preventDefault(); setSearch(query); setPage(1); }}>
      <DateSort oldest={oldestFirst} change={v=>{setOldestFirst(v);setPage(1);}}/>
          <label className="field">جست‌وجوی نام یا موبایل<input value={query} maxLength={100} onChange={event=>setQuery(event.target.value)} /></label>
      <label className="field">نوع فعالیت<select value={type} onChange={event=>{setType(event.target.value);setPage(1);}}><option value="">همه فعالیت‌ها</option>{Object.entries(types).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label>
      <label className="field">از تاریخ (میلادی)<input type="date" min="2000-01-01" max="2100-12-31" value={from} onChange={event=>{setFrom(event.target.value);setPage(1);}} /></label>
      <label className="field">تا تاریخ (میلادی)<input type="date" min="2000-01-01" max="2100-12-31" value={until} onChange={event=>{setUntil(event.target.value);setPage(1);}} /></label><button className="button button-outline">جست‌وجو</button>
    </form><p className="settings-note">فقط سوابق موجود نمایش داده می‌شوند. تمدید نشست ورود تازه نیست؛ خروج قابل اتکا در سوابق فعلی ثبت نشده است. روزهای فیلتر بر اساس ساعت تهران هستند.</p>
    {loading && <p role="status">در حال دریافت فعالیت‌ها…</p>}{error && <p role="alert" className="error-message">{error}<button onClick={reload}>تلاش دوباره</button></p>}
    {data && <><ManagementTable remote title="فعالیت کاربران" refresh={reload} busy={loading}
      columns={["مشتری","فعالیت","انجام‌دهنده","تاریخ و ساعت"]} rows={data.items.map(item => ({
        id:item.type+item.id, values:[item.customer_name?.trim() || item.phone || "ثبت نشده",
          [types[item.type], item.status && statuses[item.status]].filter(Boolean).join(" · "),
          item.actor === "customer" ? item.customer_name || item.phone : item.actor || "ثبت نشده",
          new Date(item.occurred_at).toLocaleString("fa-IR",{timeZone:"Asia/Tehran"})],
        actions:item.order_id ? <Link href={"/admin/orders/"+item.order_id}>{item.subject} · {item.order_code}</Link>
          : item.ticket_id ? <Link href={"/admin/tickets/"+item.ticket_id}>{item.subject}</Link> : "—",
      }))}/>
    <PageSizeControl value={pageSize} change={n=>{setPageSize(n);setPage(1);}}/>
    <Pagination className="user-pagination" page={page} pageSize={data.pageSize} total={data.total} noun="رویداد" loading={loading} onPageChange={setPage} /></>}
  </section>;
}

