import Link from "next/link";
import { ArrowUpLeft } from "lucide-react";
import type { PortfolioItem } from "@/lib/portfolio";

export default function PortfolioGallery({ items }: { items: PortfolioItem[] }) {
  return <div className="portfolio-grid">{items.map(item =>
    <Link prefetch={false} className={"portfolio-card " + item.theme} key={item.id}
      href={"/portfolio/" + encodeURIComponent(item.id)} aria-label={"مشاهده نمونه‌کار " + item.title}>
      <div className="portfolio-card-image">
        <img src={item.image} alt={item.title + (item.eyebrow ? "؛ " + item.eyebrow : "")} loading="lazy" width={640} height={480} />
        {item.demo && <span className="portfolio-demo">نمونه طراحی</span>}
      </div>
      <div className="portfolio-card-caption"><div><small>{item.eyebrow}</small><h3>{item.title}</h3></div><ArrowUpLeft size={21} /></div>
    </Link>)}</div>;
}
