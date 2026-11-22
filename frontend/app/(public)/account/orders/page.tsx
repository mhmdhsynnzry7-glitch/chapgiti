import Link from "next/link";
import { requireCustomerProfile } from "@/lib/customer";
import PageIntro from "@/components/page-intro";
import CustomerOrders from "@/components/customer-orders";
export const metadata = { title: "سفارش‌های من", robots: { index: false, follow: false } };
export default async function Page() {
  await requireCustomerProfile("/account/orders");
  return <div className="container page-body customer-orders-page">
    <Link prefetch={false} className="text-link" href="/account">بازگشت به حساب کاربری ↖</Link>
    <PageIntro title="سفارش‌های من" description="همهٔ سفارش‌ها، قیمت و آخرین وضعیت آن‌ها را اینجا دنبال کنید." eyebrow="حساب کاربری" />
    <CustomerOrders showTitle={false} />
  </div>;
}
