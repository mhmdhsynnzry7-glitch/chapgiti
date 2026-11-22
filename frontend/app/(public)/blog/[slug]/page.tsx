import Link from "next/link";
import Image from "next/image";
import { notFound, permanentRedirect } from "next/navigation";
import { magazineDetail } from "@/lib/magazine-server";
import { BackendError } from "@/lib/backend";
import { seo } from "@/lib/seo";
import { origin } from "@/lib/server";
import { date } from "@/lib/pricing";
import ArticleContent from "@/components/article-content";
import ArticleCard from "@/components/magazine-card";
export const dynamic = "force-dynamic";
type Props = { params: Promise<{ slug: string }> };
export async function generateMetadata({ params }: Props) {
  try {
    const { article: a } = await magazineDetail((await params).slug);
    const meta = seo(
      a.title,
      a.excerpt || a.seoDescription,
      "/blog/" + a.slug,
    );
    return {
      ...meta,
      openGraph: {
        ...meta.openGraph,
        type: "article",
        ...(a.publishedAt ? { publishedTime: a.publishedAt } : {}),
        ...(a.authorName ? { authors: [a.authorName] } : {}),
        ...(a.coverAssetId
          ? { images: [origin() + "/api/banner-assets/" + a.coverAssetId] }
          : {}),
      },
    };
  } catch {
    return {
      title: "مقاله در دسترس نیست",
      robots: { index: false, follow: false },
    };
  }
}
export default async function Page({ params }: Props) {
  const { slug } = await params;
  let result;
  try {
    result = await magazineDetail(slug);
  } catch (error) {
    if (error instanceof BackendError && error.status === 404) notFound();
    throw error;
  }
  const a = result.article;
  if (result.isAlias) permanentRedirect("/blog/" + a.slug);
  const structured = {
    "@context": "https://schema.org",
    "@type": "Article",
    headline: a.title,
    description: a.excerpt,
    url: origin() + "/blog/" + a.slug,
    ...(a.publishedAt ? { datePublished: a.publishedAt } : {}),
    ...(a.updatedAt ? { dateModified: a.updatedAt } : {}),
    ...(a.authorName
      ? { author: { "@type": "Person", name: a.authorName } }
      : {}),
    ...(a.coverAssetId
      ? { image: origin() + "/api/banner-assets/" + a.coverAssetId }
      : {}),
  };
  return (
    <div className="container magazine-detail">
      <nav className="article-breadcrumbs" aria-label="مسیر صفحه">
        <Link prefetch={false} href="/">
          خانه
        </Link>
        <span>/</span>
        <Link prefetch={false} href="/blog">
          مجله چاپ گیتی
        </Link>
        <span>/</span>
        <span>{a.category}</span>
      </nav>
      <article className="magazine-article">
        <header>
          <span className="magazine-category">{a.category}</span>
          <h1>{a.title}</h1>
          <p className="article-lead">{a.excerpt}</p>
          <div className="article-meta">
            {a.publishedAt && (
              <time dateTime={a.publishedAt}>{date(a.publishedAt)}</time>
            )}
            {a.authorName && <span>{a.authorName}</span>}
          </div>
        </header>
        {a.coverAssetId && (
          <Image
            className="article-cover"
            unoptimized
            src={"/api/banner-assets/" + a.coverAssetId}
            alt={a.coverAlt}
            width={1200}
            height={700}
          />
        )}
        <ArticleContent content={a.content} />
        <footer className="article-back">
          <Link prefetch={false} className="text-link" href="/blog">
            بازگشت به مجله
          </Link>
        </footer>
      </article>
      {result.related.length > 0 && (
        <section className="related-articles">
          <h2>خواندنی‌های مرتبط</h2>
          <div className="magazine-grid">
            {result.related.map((article) => (
              <ArticleCard key={article.id} article={article} />
            ))}
          </div>
        </section>
      )}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(structured).replaceAll("<", "\\u003c"),
        }}
      />
    </div>
  );
}
