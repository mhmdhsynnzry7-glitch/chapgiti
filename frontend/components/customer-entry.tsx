import { Check, Smartphone, UserRound } from "lucide-react";
import type { CustomerProfile } from "@/lib/customer";
import CustomerAuthForm from "@/components/customer-auth-form";
import CustomerProfileForm from "@/components/customer-profile-form";

export default function CustomerEntry({
  next, customer, recovery = false, initialMethod = "password",
}: {
  next: string;
  customer?: CustomerProfile;
  recovery?: boolean;
  initialMethod?: "password" | "register";
}) {
  const destination = next.startsWith("/order") ? "ثبت سفارش"
    : next.startsWith("/calculator") ? "محاسبه هزینه"
    : next.startsWith("/tickets") || next.startsWith("/contact") ? "ارتباط با چاپخانه" : "حساب کاربری";

  return <section className="customer-entry">
    <div className="entry-visual">
      <span className="eyebrow">حساب مشتریان چاپ گیتی</span>
      <h2>یک حساب،<br />از برآورد تا سفارش.</h2>
      <p>هزینهٔ چاپ را محاسبه کن و درخواست‌ها، پیشنهادها و مراحل سفارش را در یک حساب دنبال کن.</p>
      <div className="entry-book"><i /><b>هر کتاب،<br />یک شروع<br />تازه است.</b><small>چاپ گیتی</small></div>
      <div className="entry-steps">
        <span className="done"><Smartphone size={17} /> تأیید و ورود</span>
        <span className={customer ? "done" : ""}><UserRound size={17} /> مشخصات شما</span>
        <span><Check size={17} /> {destination}</span>
      </div>
    </div>
    {customer
      ? <CustomerProfileForm next={next} customer={customer} />
      : <CustomerAuthForm key={`${recovery}:${initialMethod}:${next}`} next={next} recovery={recovery} initialMethod={initialMethod} />}
  </section>;
}
