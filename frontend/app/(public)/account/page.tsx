import { NavigationBadge } from "@/components/notification-center";
import Link from "next/link";
import { requireCustomerProfile } from "@/lib/customer";
import CustomerOrders from "@/components/customer-orders";
import CustomerLogout from "@/components/customer-logout";
import { Calculator,ShoppingBag,Bell,UserRound,ArrowUpLeft,ClipboardList } from "lucide-react";
export const metadata={ title:"حساب کاربری",robots:{index:false,follow:false} };
export default async function Page() {
  const customer=await requireCustomerProfile("/account");
  return <div className="container page-body account-page">
    <section className="account-welcome"><div><span className="eyebrow">حساب مشتریان چاپ گیتی</span><h1>{customer.firstName}، خوش آمدی.</h1><p>از اینجا محاسبه هزینه، سفارش‌ها و اعلان‌ها را دنبال کن.</p></div><CustomerLogout/></section>
    <div className="account-service-grid">
      <Link prefetch={false} href="/calculator"><Calculator size={27}/><h2>محاسبه هزینه چاپ</h2><p>مشخصات کتاب و برآورد قیمت</p><ArrowUpLeft size={19}/></Link>
      <Link prefetch={false} href="/order"><ShoppingBag size={27}/><h2>ثبت سفارش</h2><p>ثبت درخواست چاپ و ارسال فایل</p><ArrowUpLeft size={19}/></Link>
      <Link prefetch={false} href="/account/orders"><ClipboardList size={27}/><h2>سفارش‌های من <NavigationBadge kind="orders" label="سفارش دیده‌نشده"/></h2><p>همهٔ سفارش‌ها و آخرین وضعیت آن‌ها</p><ArrowUpLeft size={19}/></Link>
      <Link prefetch={false} href="/tickets"><Bell size={27}/><h2>پشتیبانی <NavigationBadge kind="support" label="پیام پشتیبانی خوانده‌نشده"/></h2><p>درخواست‌ها و گفت‌وگوها</p></Link>
    </div>
    <div className="account-content-grid">
      <section className="panel"><div className="panel-title"><h2>مشخصات من</h2><UserRound size={20}/></div>
        <p>{customer.firstName} {customer.lastName}</p><p dir="ltr" className="account-phone">{customer.phone}</p>
        <Link prefetch={false} className="text-link" href="/customer/profile?next=/account">ویرایش مشخصات ↖</Link>
      </section>
      <CustomerOrders />
    </div>
  </div>;
}
