import { requireCustomerProfile } from "@/lib/customer";
import PageIntro from "@/components/page-intro";
import { TrackingForm } from "@/components/forms";
export const metadata = {
  title: "پیگیری سفارش",
  robots: { index: false, follow: false },
};
export default async function Page() {
  await requireCustomerProfile("/track");
  return (
    <div className="container page-body">
      <PageIntro
        title="از کتابتان خبر بگیرید"
        description="با کد پیگیری، مرحله فعلی و تاریخچه سفارش را ببینید."
        eyebrow="پیگیری سفارش"
      />
      <TrackingForm />
    </div>
  );
}
