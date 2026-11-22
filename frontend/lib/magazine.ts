export type ArticleStatus = "draft" | "published" | "archived";
export const articleStatuses: Record<ArticleStatus, string> = {
  draft: "پیش‌نویس",
  published: "منتشرشده",
  archived: "بایگانی‌شده",
};
export type MagazineArticle = {
  id: string;
  slug: string;
  title: string;
  excerpt: string;
  content: string;
  category: string;
  status: ArticleStatus;
  coverAssetId: string | null;
  coverAlt: string;
  authorName: string | null;
  publishedAt: string | null;
  createdAt: string;
  updatedAt: string;
  seoTitle: string;
  seoDescription: string;
  version: number;
  contentFormat: "markdown-v1";
};
export type MagazineCard = Pick<
  MagazineArticle,
  | "id"
  | "slug"
  | "title"
  | "excerpt"
  | "category"
  | "status"
  | "coverAlt"
  | "authorName"
  | "publishedAt"
  | "updatedAt"
  | "version"
> & { coverUrl: string | null };
export type MagazinePage = {
  items: MagazineCard[];
  total: number;
  page: number;
  pageSize: number;
  categories: string[];
};
export type MagazineDetail = {
  article: MagazineArticle;
  canonicalSlug: string;
  isAlias: boolean;
  related: MagazineCard[];
};
export const blankArticle = (): MagazineArticle => ({
  id: "",
  slug: "",
  title: "",
  excerpt: "",
  content: "",
  category: "",
  status: "draft",
  coverAssetId: null,
  coverAlt: "",
  authorName: null,
  publishedAt: null,
  createdAt: "",
  updatedAt: "",
  seoTitle: "",
  seoDescription: "",
  version: 1,
  contentFormat: "markdown-v1",
});
export function publicLink(value: string) {
  return (
    (/^\/(?!\/)/.test(value) && !/[\\\u0000-\u001f]/.test(value)) ||
    (/^https:\/\//.test(value) &&
      (() => {
        try {
          return !new URL(value).username && !new URL(value).password;
        } catch {
          return false;
        }
      })())
  );
}
