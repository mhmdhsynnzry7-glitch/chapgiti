"use client";
import ManagementTable, { DateSort, PageSizeControl } from "./management-table";
import Link from "next/link";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { useRef, useState, type FormEvent } from "react";
import { Plus, Save, Eye } from "lucide-react";
import { useApiResource } from "@/hooks/use-api-resource";
import { useUnsavedForm } from "@/hooks/use-unsaved-form";
import { api } from "@/lib/api-client";
import { sessionFetch } from "@/lib/session-client";
import {
  articleStatuses,
  blankArticle,
  type MagazinePage,
  type MagazineArticle,
  type ArticleStatus,
} from "@/lib/magazine";
import { date } from "@/lib/pricing";
import Pagination from "./pagination";
import ArticleContent from "./article-content";
type Props = {
  initialId?: string;
  notify: (message: string, error?: boolean) => void;
};
export default function AdminMagazine(props: Props) {
  return props.initialId ? (
    <ArticleLoader {...props} id={props.initialId} />
  ) : (
    <ArticleList notify={props.notify} />
  );
}
function ArticleList({ notify }: { notify: Props["notify"] }) {
  const [oldestFirst,setOldestFirst]=useState(false);
  const [pageSize,setPageSize]=useState(20);
  const [q, setQ] = useState(""),
    [search, setSearch] = useState(""),
    [status, setStatus] = useState(""),
    [category, setCategory] = useState(""),
    [page, setPage] = useState(1),
    [busy, setBusy] = useState(false);
  const { data, error, loading, reload } = useApiResource<MagazinePage>(
    "admin/articles?" +
      new URLSearchParams({
        q: search,
        status,
        category,
        page: String(page),
        pageSize: String(pageSize), oldestFirst:String(oldestFirst),
      }),
    true,
  );
  async function prepareDrafts() {
    setBusy(true);
    try {
      const result = await api<{inserted:number;skipped:number}>("admin/articles/prepare-drafts", {method:"POST"});
      notify(result.inserted ? result.inserted.toLocaleString("fa-IR")+" پیش‌نویس آماده شد." : "پیش‌نویس تازه‌ای برای افزودن وجود ندارد.");
      reload();
    } catch (cause) {
      notify(cause instanceof Error ? cause.message : "آماده‌سازی پیش‌نویس انجام نشد.", true);
    } finally { setBusy(false); }
  }
  async function transition(id: string, state: ArticleStatus) {
    if (busy) return;
    setBusy(true);
    try {
      const article = await api<MagazineArticle>("admin/articles/" + id);
      await api("admin/articles/" + id, {
        method: "PUT",
        body: JSON.stringify({ ...article, status: state }),
      });
      notify("وضعیت مقاله ذخیره شد.");
      reload();
    } catch (cause) {
      notify(cause instanceof Error ? cause.message : "ذخیره انجام نشد.", true);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section>
      <div className="admin-heading">
        <div>
          <h2>مجله چاپ گیتی</h2>
          <p>پیش‌نویس‌ها، مقاله‌های منتشرشده و بایگانی</p>
        </div>
        <button type="button" className="button button-outline button-sm" disabled={busy} onClick={prepareDrafts}>آماده‌سازی راهنماهای چاپ</button>
        <Link href="/admin/articles/new" className="button button-sm">
          <Plus size={17} />
          مقاله جدید
        </Link>
      </div>
      <form
        className="magazine-admin-filters panel"
        onSubmit={(e) => {
          e.preventDefault();
          setSearch(q);
          setPage(1);
        }}
      >
        
        <DateSort oldest={oldestFirst} change={v=>{setOldestFirst(v);setPage(1);}}/>
          <label className="field">
          جست‌وجوی مقاله
          <input
            type="search"
            maxLength={160}
            value={q}
            onChange={(e) => setQ(e.target.value)}
          />
        </label>
        <label className="field">
          وضعیت
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">همه وضعیت‌ها</option>
            {Object.entries(articleStatuses).map(([key, label]) => (
              <option value={key} key={key}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label className="field">
          دسته‌بندی
          <select
            value={category}
            onChange={(e) => {
              setCategory(e.target.value);
              setPage(1);
            }}
          >
            <option value="">همه دسته‌ها</option>
            {data?.categories.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </label>
        <button className="button button-outline">جست‌وجو</button>
      </form>
      {loading && <p role="status">در حال دریافت مقاله‌ها…</p>}
      {error && (
        <p className="error-message" role="alert">
          {error}
          <button onClick={reload}>تلاش دوباره</button>
        </p>
      )}
      {data && (
        <>
          <div className="table-wrap">
            <ManagementTable remote title="مقاله‌های مجله" columns={["عنوان","دسته","وضعیت","تاریخ انتشار","آخرین ویرایش"]} rows={data.items.map(a=>({id:a.id,values:[a.title,a.category,articleStatuses[a.status],a.publishedAt?date(a.publishedAt):"—",date(a.updatedAt)],actions:<div className="article-row-actions">
                        <Link
                          className="button button-sm button-outline"
                          href={"/admin/articles/" + a.id}
                          aria-label={"ویرایش " + a.title}
                        >
                          ویرایش
                        </Link>
                        {a.status === "draft" && (
                          <button
                            className="text-link"
                            disabled={busy}
                            onClick={() => void transition(a.id, "published")}
                          >
                            انتشار
                          </button>
                        )}
                        {a.status === "published" && (
                          <>
                            <button
                              className="text-link"
                              disabled={busy}
                              onClick={() => void transition(a.id, "draft")}
                            >
                              لغو انتشار
                            </button>
                            <button
                              className="text-link"
                              disabled={busy}
                              onClick={() => void transition(a.id, "archived")}
                            >
                              بایگانی
                            </button>
                          </>
                        )}
                        {a.status === "archived" && (
                          <button
                            className="text-link"
                            disabled={busy}
                            onClick={() => void transition(a.id, "draft")}
                          >
                            بازگشت به پیش‌نویس
                          </button>
                        )}
                      </div>}))}/>
          </div>
          {!data.items.length && (
            <p className="panel" role="status">
              مقاله‌ای پیدا نشد. یک پیش‌نویس تازه بسازید.
            </p>
          )}
          <PageSizeControl value={pageSize} change={n=>{setPageSize(n);setPage(1);}}/>
          <Pagination
            page={page}
            pageSize={data.pageSize}
            total={data.total}
            noun="مقاله"
            onPageChange={setPage}
          />
        </>
      )}
    </section>
  );
}
function ArticleLoader({
  id,
  notify,
}: {
  id: string;
  notify: Props["notify"];
}) {
  const { data, error, loading, reload } = useApiResource<MagazineArticle>(
    "admin/articles/" + id,
    false,
    undefined,
    id !== "new",
  );
  if (id === "new")
    return <ArticleEditor key="new" initial={blankArticle()} notify={notify} />;
  if (error)
    return (
      <p className="error-message" role="alert">
        {error}
        <button onClick={reload}>تلاش دوباره</button>
      </p>
    );
  if (loading || !data) return <p role="status">در حال دریافت مقاله…</p>;
  return <ArticleEditor key={id} initial={data} notify={notify} />;
}
function ArticleEditor({
  initial,
  notify,
}: {
  initial: MagazineArticle;
  notify: Props["notify"];
}) {
  const router = useRouter(),
    [draft, setDraft] = useState(initial),
    [baseline, setBaseline] = useState(JSON.stringify(initial)),
    [busy, setBusy] = useState(false),
    [uploading, setUploading] = useState(false),
    [error, setError] = useState(""),
    [preview, setPreview] = useState(false);
  const sending = useRef(false);
  useUnsavedForm(JSON.stringify(draft) !== baseline);
  const change = (patch: Partial<MagazineArticle>) =>
    setDraft((current) => ({ ...current, ...patch }));
  async function upload(file: File) {
    if (uploading || busy) return;
    if (
      file.size > 5 * 1024 * 1024 ||
      !["image/png", "image/jpeg", "image/webp"].includes(file.type)
    ) {
      setError("تصویر PNG، JPG یا WebP تا ۵ مگابایت انتخاب کنید.");
      return;
    }
    setUploading(true);
    setError("");
    try {
      const res = await sessionFetch("/api/admin/banner-assets", {
          method: "POST",
          headers: { "Content-Type": file.type },
          body: file,
        }),
        json = await res.json();
      if (!res.ok) throw new Error(json.error || "بارگذاری انجام نشد.");
      const id = String(json.url).split("/").pop()!;
      change({ coverAssetId: id });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "بارگذاری انجام نشد.");
    } finally {
      setUploading(false);
    }
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (sending.current || uploading) return;
    const status =
      ((event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement)
        ?.value || draft.status;
    sending.current = true;
    setBusy(true);
    setError("");
    try {
      const result = await api<{ id: string }>(
        "admin/articles" + (draft.id ? "/" + draft.id : ""),
        {
          method: draft.id ? "PUT" : "POST",
          body: JSON.stringify({ ...draft, status }),
        },
      );
      const saved = await api<MagazineArticle>("admin/articles/" + result.id);
      setDraft(saved);
      setBaseline(JSON.stringify(saved));
      notify("مقاله ذخیره شد.");
      if (!draft.id) router.replace("/admin/articles/" + result.id);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "مقاله ذخیره نشد.");
    } finally {
      sending.current = false;
      setBusy(false);
    }
  }
  return (
    <section className="article-editor">
      <div className="admin-heading">
        <div>
          <h2>{draft.id ? "ویرایش مقاله" : "مقاله جدید"}</h2>
          <p>
            <span className={"article-status " + draft.status}>
              {articleStatuses[draft.status]}
            </span>{" "}
            · پیش‌نویس‌ها فقط در پنل مدیریت دیده می‌شوند.
          </p>
        </div>
        <Link className="text-link" href="/admin/articles">
          بازگشت به مجله
        </Link>
      </div>
      <form onSubmit={save} aria-busy={busy || uploading}>
        <fieldset
          disabled={busy || uploading}
          className="article-editor-fields"
        >
          <section className="panel">
            <h3>اطلاعات اصلی</h3>
            <div className="form-grid">
              <label className="field full">
                عنوان مقاله
                <input
                  name="title"
                  maxLength={180}
                  value={draft.title}
                  onChange={(e) => change({ title: e.target.value })}
                />
              </label>
              <label className="field">
                نشانی مقاله
                <input
                  aria-label="نشانی مقاله"
                  aria-describedby="article-slug-help"
                  name="slug"
                  dir="ltr"
                  required
                  maxLength={120}
                  pattern="[a-z0-9]+(-[a-z0-9]+)*"
                  value={draft.slug}
                  onChange={(e) => change({ slug: e.target.value })}
                />
                <small id="article-slug-help">
                  تغییر عنوان نشانی را عوض نمی‌کند. نشانی قدیمی پس از تغییر صریح
                  هدایت می‌شود.
                </small>
              </label>
              <label className="field">
                دسته‌بندی
                <input
                  aria-label="دسته‌بندی"
                  aria-describedby="article-category-help"
                  name="category"
                  maxLength={60}
                  value={draft.category}
                  onChange={(e) => change({ category: e.target.value })}
                />
                <small id="article-category-help">
                  همان دسته‌بندی متنی مقاله‌های قبلی؛ حذف دسته، مقاله‌ای را حذف
                  نمی‌کند.
                </small>
              </label>
              <label className="field full">
                خلاصه مقاله
                <textarea
                  name="excerpt"
                  maxLength={400}
                  rows={3}
                  value={draft.excerpt}
                  onChange={(e) => change({ excerpt: e.target.value })}
                />
              </label>
            </div>
          </section>
          <section className="panel">
            <div className="admin-heading">
              <h3>محتوای مقاله</h3>
              <button
                className="button button-sm button-outline"
                type="button"
                onClick={() => setPreview((v) => !v)}
              >
                <Eye size={17} />
                {preview ? "ویرایش متن" : "پیش‌نمایش متن"}
              </button>
            </div>
            <p className="settings-note">
              متن و Markdown امن: تیتر با ## یا ###، فهرست با -، تأکید با
              **متن**، نقل‌قول با &gt; و جدول. HTML و اسکریپت اجرا نمی‌شوند.
            </p>
            {preview ? (
              <div className="article-editor-preview">
                <ArticleContent content={draft.content} />
              </div>
            ) : (
              <label className="field">
                متن مقاله
                <textarea
                  className="article-body-input"
                  name="content"
                  maxLength={40000}
                  rows={16}
                  value={draft.content}
                  onChange={(e) => change({ content: e.target.value })}
                />
              </label>
            )}
          </section>
          <section className="panel">
            <h3>تصویر شاخص</h3>
            <div className="article-image-editor">
              {draft.coverAssetId && (
                <Image
                  unoptimized
                  src={"/api/banner-assets/" + draft.coverAssetId}
                  alt={draft.coverAlt || "پیش‌نمایش تصویر"}
                  width={520}
                  height={300}
                />
              )}
              <div>
                <label className="field">
                  بارگذاری تصویر شاخص
                  <input
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) void upload(file);
                      e.target.value = "";
                    }}
                  />
                </label>
                <label className="field">
                  متن جایگزین تصویر
                  <input
                    name="coverAlt"
                    maxLength={250}
                    required={
                      !!draft.coverAssetId && draft.status === "published"
                    }
                    value={draft.coverAlt}
                    onChange={(e) => change({ coverAlt: e.target.value })}
                  />
                </label>
                {draft.coverAssetId && (
                  <>
                    <button
                      type="button"
                      className="text-link"
                      onClick={() =>
                        change({ coverAssetId: null, coverAlt: "" })
                      }
                    >
                      حذف تصویر از مقاله
                    </button>
                    <p className="settings-note">
                      برای تصویر در متن:{" "}
                      <code dir="ltr">
                        ![توضیح تصویر](/api/banner-assets/{draft.coverAssetId})
                      </code>
                    </p>
                  </>
                )}
              </div>
            </div>
          </section>
          <section className="panel">
            <h3>تنظیمات انتشار</h3>
            <label className="field">
              نام عمومی نویسنده (اختیاری)
              <input
                aria-label="نام عمومی نویسنده (اختیاری)"
                aria-describedby="article-author-help"
                name="authorName"
                maxLength={120}
                value={draft.authorName ?? ""}
                onChange={(e) => change({ authorName: e.target.value || null })}
              />
              <small id="article-author-help">
                فقط نام مورد تأیید برای نمایش عمومی؛ نویسندهٔ تاریخی نامشخص را
                خالی نگه دارید.
              </small>
            </label>
            <p>
              {draft.publishedAt
                ? "اولین انتشار ثبت‌شده: " + date(draft.publishedAt)
                : "تاریخ انتشار ثبت نشده است."}
            </p>
            <p className="settings-note">
              ذخیرهٔ معمولی تاریخ اولین انتشار را عوض نمی‌کند. برای انتشار،
              عنوان، خلاصه، دسته‌بندی و حداقل ۳۰ کاراکتر متن لازم است.
            </p>
          </section>
        </fieldset>
        {uploading && <p role="status">در حال بارگذاری تصویر…</p>}
        {error && (
          <p className="error-message" role="alert">
            {error}
          </p>
        )}
        <div className="article-editor-actions panel">
          <button
            className="button"
            disabled={busy || uploading}
            value={draft.id ? draft.status : "draft"}
          >
            <Save size={17} />
            {busy
              ? "در حال ذخیره…"
              : draft.id
                ? "ذخیره تغییرات"
                : "ذخیره پیش‌نویس"}
          </button>
          {draft.id && draft.status === "draft" && (
            <button
              className="button button-outline"
              disabled={busy || uploading}
              value="published"
            >
              انتشار مقاله
            </button>
          )}
          {draft.id && draft.status === "published" && (
            <>
              <button
                className="button button-outline"
                disabled={busy || uploading}
                value="draft"
              >
                لغو انتشار
              </button>
              <button
                className="button button-outline"
                disabled={busy || uploading}
                value="archived"
              >
                بایگانی
              </button>
              <Link
                prefetch={false}
                href={"/blog/" + draft.slug}
                target="_blank"
                rel="noopener"
                className="text-link"
              >
                مشاهده مقاله
              </Link>
            </>
          )}
          {draft.status === "archived" && (
            <button
              className="button button-outline"
              disabled={busy || uploading}
              value="draft"
            >
              بازگشت به پیش‌نویس
            </button>
          )}
          <Link href="/admin/articles" className="text-link">
            انصراف
          </Link>
        </div>
      </form>
    </section>
  );
}
