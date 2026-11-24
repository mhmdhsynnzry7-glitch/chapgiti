import { requireCustomerProfile } from "@/lib/customer";
import { backendApi } from "@/lib/backend";
import type { TrackingResult } from "@/lib/api-contracts";
import CustomerTickets from "@/components/customer-tickets";
import { redirect } from "next/navigation";
import PageIntro from "@/components/page-intro";
export const metadata = {title:"پشتیبانی", robots:{index:false,follow:false}};
export default async function Page({searchParams}:{searchParams:Promise<{order?:string}>}) {
  await requireCustomerProfile("/tickets");
  const {order} = await searchParams;
  const related = order && /^[a-f0-9-]{36}$/i.test(order) ? (await backendApi<TrackingResult>("customer/orders/"+order)) : undefined;
  if (related?.openTicketId) redirect("/tickets/"+related.openTicketId);
  return <div className="container page-body"><PageIntro title="پشتیبانی" description="درخواست خود را ثبت کنید یا گفت‌وگو را ادامه دهید."/><CustomerTickets order={related?.order}/></div>;
}
