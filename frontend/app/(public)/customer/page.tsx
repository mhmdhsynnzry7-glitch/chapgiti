import { redirect } from "next/navigation";
import CustomerEntry from "@/components/customer-entry";
import { currentCustomer, safeCustomerNext } from "@/lib/customer";
export const metadata = { title: "ورود مشتریان", robots: { index: false, follow: false } };
export default async function Page({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams, next = safeCustomerNext(params.next);
  const customer = await currentCustomer();
  if (params.legacy === "1") redirect("/admin/login");
  if (customer && params.change !== "1") {
    if (customer.hasAccount && customer.firstName && customer.lastName) redirect(next);
    if (customer.phoneVerified) redirect("/customer/profile?next=" + encodeURIComponent(next));
  }
  return <div className="container customer-page"><CustomerEntry next={next} initialMethod={params.mode === "register" ? "register" : "password"} /></div>;
}
