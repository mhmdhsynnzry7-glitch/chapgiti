import Link from "next/link";
import { notFound } from "next/navigation";
import { requireCustomerProfile } from "@/lib/customer";
import { backendApi, BackendError } from "@/lib/backend";
import type { TrackingResult } from "@/lib/api-contracts";
import OrderTrackingDetails from "@/components/order-tracking-details";
export const metadata = { title: "جزئیات و پیگیری سفارش", robots: { index: false, follow: false } };
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!/^[a-f0-9-]{36}$/.test(id)) notFound();
  await requireCustomerProfile("/account/orders/" + id);
  let result: TrackingResult;
  try { result = await backendApi<TrackingResult>("customer/orders/" + id); }
  catch (error) { if (error instanceof BackendError && error.status === 404) notFound(); throw error; }
  return <div className="container page-body customer-order-page"><Link prefetch={false} className="text-link" href="/account/orders">بازگشت به سفارش‌های من ↖</Link>
    <section className="panel"><h1 className="order-detail-title">جزئیات سفارش</h1><OrderTrackingDetails result={result} /><Link href={result.openTicketId ? "/tickets/"+result.openTicketId : "/tickets?order="+id+"#new-ticket"}>{result.openTicketId ? "ادامه گفت‌وگوی سفارش" : "پشتیبانی درباره این سفارش"}</Link></section>
  </div>;
}
