import "server-only";
import { cache } from "react";
import { backendApi } from "./backend";
import type { MagazineDetail, MagazinePage } from "./magazine";
// React cache deduplicates only this render, while the backend fetch is explicitly no-store.
export const magazineDetail = cache((slug: string) =>
  backendApi<MagazineDetail>("public/articles/" + encodeURIComponent(slug)),
);
export const magazinePage = (q = "", category = "", page = 1) =>
  backendApi<MagazinePage>(
    "public/articles?" +
      new URLSearchParams({ q, category, page: String(page), pageSize: "12" }),
  );
