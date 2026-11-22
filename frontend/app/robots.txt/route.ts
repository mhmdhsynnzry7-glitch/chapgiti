import { indexed, origin } from "@/lib/server";
export const dynamic = "force-dynamic";
export async function GET() {
  return new Response(
    indexed()
      ? "User-agent: *\nAllow: /\nDisallow: /admin\nDisallow: /api/\nDisallow: /order\nDisallow: /track\nSitemap: " +
          origin() +
          "/sitemap.xml\n"
      : "User-agent: *\nDisallow: /\n",
    { headers: { "Content-Type": "text/plain; charset=utf-8" } },
  );
}
