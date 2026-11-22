import Image from "next/image";
import Link from "next/link";
import { ArrowUpLeft, BookOpen } from "lucide-react";
import { date } from "@/lib/pricing";
import type { MagazineCard } from "@/lib/magazine";
export default function ArticleCard({ article }: { article: MagazineCard }) {
  return (
    <article className="magazine-card">
      <Link
        prefetch={false}
        href={"/blog/" + article.slug}
        className="magazine-card-link"
      >
        <div className="magazine-card-image">
          {article.coverUrl ? (
            <Image
              unoptimized
              src={article.coverUrl}
              alt={article.coverAlt}
              width={720}
              height={450}
            />
          ) : (
            <div className="magazine-cover-placeholder" aria-hidden="true">
              <BookOpen size={54} />
              <span>چاپ گیتی</span>
            </div>
          )}
        </div>
        <div className="magazine-card-body">
          <span className="magazine-category">{article.category}</span>
          <h2>{article.title}</h2>
          <p>{article.excerpt}</p>
          <div className="magazine-card-meta">
            {article.publishedAt && (
              <time dateTime={article.publishedAt}>
                {date(article.publishedAt)}
              </time>
            )}
            <span>
              خواندن مقاله <ArrowUpLeft size={16} />
            </span>
          </div>
        </div>
      </Link>
    </article>
  );
}
