import { requireCustomerProfile } from "@/lib/customer";
import { NotificationCenter } from "@/components/notification-center";
import PageIntro from "@/components/page-intro";

export const metadata = { title: "اعلان‌ها", robots: { index: false, follow: false } };
export default async function Page() {
  await requireCustomerProfile("/account/notifications");
  return <div className="container page-body"><PageIntro eyebrow="حساب کاربری" title="اعلان‌ها" description="پیشنهادها و پیشرفت سفارش را دنبال کنید." /><NotificationCenter /></div>;
}
