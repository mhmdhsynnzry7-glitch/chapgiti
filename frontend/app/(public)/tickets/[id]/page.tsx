import Link from "next/link";
import type { TicketThreadResult } from "@/lib/api-contracts";
import { notFound } from "next/navigation";
import { requireCustomerProfile } from "@/lib/customer";
import { backendApi,BackendError } from "@/lib/backend";
import TicketThread from "@/components/ticket-thread";
export const metadata={title:"گفت‌وگوی پشتیبانی",robots:{index:false,follow:false}};
export default async function Page({params}:{params:Promise<{id:string}>}){
  const {id}=await params;
  if(!/^[a-f0-9-]{36}$/.test(id)) notFound();
  await requireCustomerProfile("/tickets/"+id);
  let data:TicketThreadResult;
  try { data=await backendApi<TicketThreadResult>("tickets/"+id); }
  catch(error){if(error instanceof BackendError&&error.status===404)notFound();throw error;}
  return <div className="container page-body ticket-detail-page"><Link prefetch={false} className="text-link" href="/tickets">بازگشت به تیکت‌های من ↖</Link><TicketThread initial={data}/></div>;
}
