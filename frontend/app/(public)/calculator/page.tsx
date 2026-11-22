import PageIntro from "@/components/page-intro";
import BookCalculator from "@/components/book-calculator";
import { requireCustomerProfile } from "@/lib/customer";
import { seo } from "@/lib/seo";
export async function generateMetadata() {
  return seo("محاسبه آنلاین هزینه چاپ کتاب", "محاسبه جزئی هزینه چاپ متن، کاغذ، جلد، صحافی و خدمات تکمیلی کتاب.", "/calculator");
}
export default async function Page() {
  const customer = await requireCustomerProfile("/calculator");
  return <div className="container page-body advanced-calculator-page">
    <PageIntro title="محاسبه هزینه تقریبی چاپ کتاب" description="از کاغذ متن تا آخرین جزئیات جلد؛ مشخصات را انتخاب کنید و برآورد هزینه را ببینید." eyebrow="هر انتخاب، یک کتاب متفاوت" />
    <BookCalculator customer={customer} />
  </div>;
}
