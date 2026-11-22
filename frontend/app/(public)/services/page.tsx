import PageIntro from "@/components/page-intro";
import ServiceCard from "@/components/service-card";
import { publicServices } from "@/lib/server";
import { seo } from "@/lib/seo";
export async function generateMetadata() {
  return seo("خدمات چاپ و نشر کتاب", "چاپ کتاب، طراحی جلد، صفحه‌آرایی، ویرایش و خدمات نشر متناسب با نیاز اثر شما.", "/services");
}
export default async function Page() {
  const services = await publicServices();
  return <div className="container page-body">
    <PageIntro title="از اولین کلمه تا آخرین صفحه" description="برای هر قدم از آماده‌سازی و تولید کتاب، خدمت مناسب را انتخاب کنید." eyebrow="خدمات چاپ گیتی" />
    <div className="service-page-grid">{services.map(service => <ServiceCard key={service.id} service={service} />)}</div>
    {!services.length && <p className="panel" role="status">در حال حاضر خدمتی برای نمایش منتشر نشده است.</p>}
  </div>;
}
