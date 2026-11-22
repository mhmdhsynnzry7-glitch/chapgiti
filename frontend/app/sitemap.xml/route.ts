import { publicServices, origin, indexed } from "@/lib/server";
import { backendApi } from "@/lib/backend";
export const dynamic = "force-dynamic";
export async function GET() {
  const services = await publicServices();
  const articles = indexed()
    ? await backendApi<{ slug: string; updatedAt: string }[]>(
        "public/magazine/sitemap",
      )
    : [];
  const paths = indexed()
    ? [
        "",
        "/services",
        "/calculator",
        "/contact",
        "/privacy",
        "/blog",
        ...articles.map((article) => "/blog/" + article.slug),
        ...services.map((s) => "/services/" + s.slug),
      ]
    : [];
  const escape = (s: string) =>
    s
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll('"', "&quot;");
  return new Response(
    '<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">' +
      paths
        .map((path) => "<url><loc>" + escape(origin() + path) + "</loc></url>")
        .join("") +
      "</urlset>",
    { headers: { "Content-Type": "application/xml; charset=utf-8" } },
  );
}
