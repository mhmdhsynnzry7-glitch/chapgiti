import Link from "next/link";
import Image from "next/image";
import { notFound, permanentRedirect } from "next/navigation";
import PageIntro from "@/components/page-intro";
import { publicServices } from "@/lib/server";
import { consultationUrl } from "@/lib/services";
import { BookArt, Check, ArrowUpLeft } from "@/components/ui";
import { seo } from "@/lib/seo";
type Props = { params: Promise<{ slug: string }> };
export async function generateMetadata({ params }: Props) {
  const { slug } = await params;
  const service = (await publicServices()).find(item => item.slug === slug || item.aliases.includes(slug));
  return service ? seo(service.title, service.description || service.seoDescription, "/services/" + service.slug)
    : { robots: { index: false, follow: false } };
}
export default async function Page({ params }: Props) {
  const { slug } = await params;
  const service = (await publicServices()).find(item => item.slug === slug || item.aliases.includes(slug));
  if (!service) notFound();
  if (service.slug !== slug) permanentRedirect("/services/" + service.slug);
  return <div className="container page-body">
    <PageIntro title={service.title} description={service.subtitle} eyebrow="خدمات چاپ و نشر" />
    <div className="service-detail"><div>
      <h2>دربارهٔ این خدمت</h2><p className="service-full-description">{service.fullDescription || service.description}</p>
      {service.features.length > 0 && <ul>{service.features.map(feature => <li key={feature}><Check size={18} />{feature}</li>)}</ul>}
      {service.sections.map((section, index) => <section className="service-content-section" key={index}><h3>{section.heading}</h3><p>{section.body}</p></section>)}
      <p className="notice">دامنه خدمت، هزینه و زمان تحویل پس از بررسی درخواست مشخص می‌شود.</p>
      <div className="form-actions"><Link prefetch={false} className="button" href={consultationUrl()}>درخواست مشاوره <ArrowUpLeft size={18} /></Link></div>
    </div>{service.image ? <Image unoptimized className="service-cover-image" src={service.image} width={680} height={520} alt={service.title} /> : <BookArt small />}</div>
  </div>;
}
