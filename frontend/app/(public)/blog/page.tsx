import Link from "next/link";
import ArticleCard from "@/components/magazine-card";
import { magazinePage } from "@/lib/magazine-server";
import { seo } from "@/lib/seo";
export const dynamic = "force-dynamic";
export const generateMetadata = () => seo(
  "مجله چاپ گیتی",
  "راهنماهای چاپ، آماده‌سازی کتاب و تجربه‌های نشر در مجله چاپ گیتی.",
  "/blog",
);
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const search = await searchParams,
    category =
      typeof search.category === "string" ? search.category.slice(0, 60) : "",
    page =
      typeof search.page === "string" && /^\d{1,5}$/.test(search.page)
        ? Math.max(1, Number(search.page))
        : 1;
  let result;
  try {
    result = await magazinePage("", category, page);
  } catch {
    return (
      <section className="container magazine-shell">
        <h1>مجله چاپ گیتی</h1>
        <p className="error-message" role="alert">
          دریافت مقاله‌ها انجام نشد. کمی بعد دوباره تلاش کنید.
        </p>
        <Link prefetch={false} className="button button-outline" href="/blog">
          تلاش دوباره
        </Link>
      </section>
    );
  }
  const href = (next: number, cat = category) =>
      "/blog?" + new URLSearchParams({ category: cat, page: String(next) }),
    pages = Math.max(1, Math.ceil(result.total / result.pageSize));
  return (
    <section className="container magazine-shell">
      <header className="magazine-heading">
        <span className="eyebrow">دانش چاپ، برای داستان شما</span>
        <h1>مجله چاپ گیتی</h1>
        <p>
          از آماده‌سازی فایل تا انتخاب کاغذ و صحافی؛ خواندنی‌هایی برای تصمیم‌های
          بهتر.
        </p>
      </header>
      <nav className="magazine-categories" aria-label="دسته‌بندی مجله">
        <Link
          prefetch={false}
          className={!category ? "selected" : ""}
          href={href(1, "")}
        >
          همه مقاله‌ها
        </Link>
        {result.categories.map((c) => (
          <Link
            prefetch={false}
            className={category === c ? "selected" : ""}
            key={c}
            href={href(1, c)}
          >
            {c}
          </Link>
        ))}
      </nav>
      {result.items.length ? (
        <div className="magazine-grid">
          {result.items.map((article) => (
            <ArticleCard key={article.id} article={article} />
          ))}
        </div>
      ) : (
        <div className="panel magazine-empty" role="status">
          <h2>
            {category
              ? "مقاله‌ای در این دسته پیدا نشد"
              : "هنوز مقاله‌ای منتشر نشده است"}
          </h2>
          <p>برای خواندنی‌های تازه دوباره به ما سر بزنید.</p>
          {(category) && (
            <Link prefetch={false} href="/blog">
              نمایش همه مقاله‌ها
            </Link>
          )}
        </div>
      )}
      <nav className="giti-pagination" aria-label="صفحه‌بندی مجله">
        {page > 1 && (
          <Link
            prefetch={false}
            className="button button-outline button-sm"
            href={href(page - 1)}
          >
            صفحه قبل
          </Link>
        )}
        <span>
          صفحه {page.toLocaleString("fa-IR")} از {pages.toLocaleString("fa-IR")}
        </span>
        {page < pages && (
          <Link
            prefetch={false}
            className="button button-outline button-sm"
            href={href(page + 1)}
          >
            صفحه بعد
          </Link>
        )}
      </nav>
    </section>
  );
}
