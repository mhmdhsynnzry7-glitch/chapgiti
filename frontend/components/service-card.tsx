import Link from "next/link";
import Image from "next/image";
import { ServiceIcon, ArrowUpLeft } from "./ui";
import { consultationUrl, type ServiceRecord } from "@/lib/services";
export default function ServiceCard({ service }: { service: ServiceRecord }) {
  return <article className={"service-card cms-service-card " + service.color}>
    {service.image ? <Image unoptimized width={420} height={220} src={service.image} alt={service.title} className="service-card-image" />
      : <span className="service-icon"><ServiceIcon name={service.icon} /></span>}
    <h2><Link prefetch={false} href={"/services/" + service.slug}>{service.title}</Link></h2>
    {service.subtitle && <p className="service-subtitle">{service.subtitle}</p>}
    <p>{service.description}</p>
    <div className="service-card-actions"><Link prefetch={false} className="button button-sm" href={consultationUrl()}>درخواست مشاوره <ArrowUpLeft size={17} /></Link>
      <Link prefetch={false} className="text-link" href={"/services/" + service.slug}>جزئیات خدمت</Link></div>
  </article>;
}
