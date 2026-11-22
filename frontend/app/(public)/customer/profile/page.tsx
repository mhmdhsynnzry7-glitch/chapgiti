import { redirect } from "next/navigation";
import CustomerEntry from "@/components/customer-entry";
import { currentCustomer, safeCustomerNext } from "@/lib/customer";
import { cookies } from "next/headers";
export const metadata = { title: "تکمیل مشخصات مشتری", robots: { index: false, follow: false } };
export default async function Page({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams, next = safeCustomerNext(params.next);
  const customer = await currentCustomer();
  if (!customer && (await cookies()).has("giti_customer_refresh")) redirect("/session/renew?next=" + encodeURIComponent("/customer/profile?next=" + encodeURIComponent(next)));
  if (!customer || (!customer.hasAccount && !customer.phoneVerified)) redirect("/customer?change=1&next=" + encodeURIComponent(next));
  return <div className="container customer-page"><CustomerEntry next={next} customer={customer} /></div>;
}
