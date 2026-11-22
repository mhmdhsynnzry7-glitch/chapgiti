"use client";
import { createTable, getCoreRowModel, getSortedRowModel, type SortingState } from "@tanstack/react-table";
import { useMemo, useState, type ReactNode } from "react";
import { RotateCw } from "lucide-react";
import styles from "./management.module.css";

export type TableRow = {
  emphasis?: boolean;
  id: string;
  values: (string | number)[];
  cells?: ReactNode[];
  actions?: ReactNode;
  status?: string;
};

export function DateSort({oldest, change}: {oldest:boolean; change:(value:boolean)=>void}) {
  return <label>ترتیب تاریخ
    <select aria-label="ترتیب تاریخ" value={oldest ? "oldest" : "newest"} onChange={e=>change(e.target.value === "oldest")}>
      <option value="newest">جدیدترین ابتدا</option><option value="oldest">قدیمی‌ترین ابتدا</option>
    </select>
  </label>;
}
export function PageSizeControl({
  value,
  change,
}: {
  value: number;
  change: (size: number) => void;
}) {
  return (
    <label>
      تعداد در صفحه
      <select aria-label="تعداد در صفحه" value={value} onChange={(e) => change(Number(e.target.value))}>
        {[10, 20, 50].map((n) => (
          <option key={n} value={n}>
            {n.toLocaleString("fa-IR")}
          </option>
        ))}
      </select>
    </label>
  );
}
export default function ManagementTable({
  title,
  columns,
  rows,
  refresh,
  busy,
  remote = false,
}: {
  title: string;
  columns: string[];
  rows: TableRow[];
  refresh?: () => void;
  busy?: boolean;
  remote?: boolean;
}) {
  const [search, setSearch] = useState(""),
    [filter, setFilter] = useState("");
  const [sorting, setSorting] = useState<SortingState>([]);
  const [pageSize, setSize] = useState(20),
    [page, setPage] = useState(1);
  const statuses = [...new Set(rows.map((r) => r.status).filter(Boolean))];
  const data = useMemo(() => {
    if (remote) return rows;
    const query = search.trim().toLocaleLowerCase("fa");
    return rows.filter(row =>
      (!filter || row.status === filter) &&
      row.values.some(value => String(value).toLocaleLowerCase("fa").includes(query)),
    );
  }, [rows, remote, filter, search]);
  const definitions = useMemo(() => columns.map((header, index) => ({
    id: String(index), header, accessorFn: (row: TableRow) => row.values[index],
    sortDescFirst: false,
    sortingFn: (a: {original: TableRow}, b: {original: TableRow}) =>
      String(a.original.values[index]).localeCompare(String(b.original.values[index]), "fa", {numeric:true}),
  })), [columns]);
  const filtered = useMemo(() => {
    if (remote || !sorting.length) return data;
    // Derive a row snapshot. React owns the sort state and no mutable table instance escapes.
    const table = createTable<TableRow>({
      data, columns: definitions, getRowId: row => row.id,
      state: { sorting }, onStateChange: () => {}, renderFallbackValue: null,
      autoResetPageIndex: false,
      getCoreRowModel: getCoreRowModel(), getSortedRowModel: getSortedRowModel(),
    });
    return table.getRowModel().rows.map(row => row.original);
  }, [data, definitions, remote, sorting]);
  const pages = Math.max(1, Math.ceil(filtered.length / pageSize)),
    current = Math.min(page, pages);
  const visible = remote
    ? filtered
    : filtered.slice((current - 1) * pageSize, current * pageSize);
  return (
    <section
      className={styles.collection}
      dir="rtl"
      aria-label={title}
      aria-busy={busy}
    >
      <div className={styles.toolbar} role="toolbar" aria-label={"ابزار " + title}>
        {!remote && (
          <>
            <label>
              جست‌وجو
              <input
                type="search"
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
              />
            </label>
            {statuses.length > 0 && (
              <label>
                وضعیت
                <select aria-label="فیلتر وضعیت"
                  value={filter}
                  onChange={(e) => {
                    setFilter(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">همه</option>
                  {statuses.map((s) => (
                    <option key={s}>{s}</option>
                  ))}
                </select>
              </label>
            )}

          </>
        )}
        {refresh && (
          <button
            type="button"
            aria-label={"تازه‌سازی " + title}
            title="تازه‌سازی"
            disabled={busy}
            onClick={refresh}
          >
            <RotateCw size={16} />
          </button>
        )}
      </div>
      <div
        className={styles.scroll}
        tabIndex={0}
        role="region"
        aria-label={title}
      >
        <table aria-label={title}>
          <thead>
            <tr>
              {columns.map((c, i) => (
                <th
                  key={c}
                  scope="col"
                  aria-sort={
                    sorting[0]?.id === String(i) ? sorting[0].desc ? "descending" : "ascending" : undefined
                  }
                >
                  {remote ? (
                    c
                  ) : (
                    <button
                      type="button"
                      onClick={() => {
                        const id = String(i);
                        setSorting(previous => [{ id, desc: previous[0]?.id === id && !previous[0].desc }]);
                        setPage(1);
                      }}
                    >
                      {c} ↕
                    </button>
                  )}
                </th>
              ))}
              <th scope="col">عملیات</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((r) => (
              <tr key={r.id} data-attention={r.emphasis || undefined}>
                {r.values.map((v, i) => (
                  <td key={columns[i]}>{r.cells?.[i] ?? v}</td>
                ))}
                <td>
                  <div className={styles.actions}>{r.actions}</div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {busy && (
        <div className={styles.skeleton} role="status">
          در حال دریافت…
        </div>
      )}
      {!busy && !visible.length && (
        <p role="status">موردی برای نمایش پیدا نشد.</p>
      )}
      {!remote && (
        <div className={styles.toolbar}>
            <PageSizeControl
              value={pageSize}
              change={(n) => {
                setSize(n);
                setPage(1);
              }}
            />
          <span>{visible.length ? ((current - 1) * pageSize + 1).toLocaleString("fa-IR") : "۰"} تا {Math.min(current * pageSize, filtered.length).toLocaleString("fa-IR")} از {filtered.length.toLocaleString("fa-IR")}</span>
          <button
            type="button"
            disabled={current === 1}
            onClick={() => setPage(current - 1)}
          >
            قبلی
          </button>
          <span>
            {current.toLocaleString("fa-IR")} از {pages.toLocaleString("fa-IR")}
          </span>
          <button
            type="button"
            disabled={current === pages}
            onClick={() => setPage(current + 1)}
          >
            بعدی
          </button>
        </div>
      )}
    </section>
  );
}
