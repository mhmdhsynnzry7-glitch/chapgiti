"use client";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { usePathname } from "next/navigation";
import { catalogOptions, type CatalogItem } from "@/lib/print-catalog";
import type { PrintOptions } from "@/lib/book-options";
type State = {
  items: CatalogItem[];
  options: PrintOptions | null;
  loading: boolean;
  error: string;
  reload: () => void;
};
const Context = createContext<State | null>(null);
export function PrintCatalogProvider({ children }: { children: ReactNode }) {
  const path = usePathname();
  return (
    <CatalogState
      key={path}
      enabled={path === "/calculator" || path === "/order"}
    >
      {children}
    </CatalogState>
  );
}
function CatalogState({
  children,
  enabled,
}: {
  children: ReactNode;
  enabled: boolean;
}) {
  const [data, setData] = useState<{
      items: CatalogItem[];
      options: PrintOptions;
    } | null>(null),
    [error, setError] = useState("");
  const controller = useRef<AbortController | null>(null);
  // Keep the last catalog while revalidating; an error disables submission rather than dropping a draft.
  const reload = useCallback(() => {
    if (!enabled) return;
    controller.current?.abort();
    const request = new AbortController();
    controller.current = request;
    void fetch("/api/public/print-catalog", {
      cache: "no-store",
      signal: request.signal,
    })
      .then(async (response) => {
        const json = await response.json();
        if (!response.ok)
          throw new Error(json.error || "دریافت تنظیمات چاپ انجام نشد.");
        if (!Array.isArray(json.items))
          throw new Error("پاسخ تنظیمات چاپ معتبر نیست.");
        const items = json.items as CatalogItem[];
        return { items, options: catalogOptions(items) };
      })
      .then((value) => {
        if (!request.signal.aborted) {
          setData((previous) =>
            JSON.stringify(previous?.items) === JSON.stringify(value.items)
              ? previous
              : value,
          );
          setError("");
        }
      })
      .catch((cause) => {
        if (!request.signal.aborted)
          setError(
            cause instanceof Error
              ? cause.message
              : "ارتباط با چاپخانه برقرار نشد.",
          );
      });
  }, [enabled]);
  useEffect(() => {
    if (!enabled) return;
    reload();
    const visible = () => {
      if (!document.hidden) reload();
    };
    window.addEventListener("focus", visible);
    document.addEventListener("visibilitychange", visible);
    window.addEventListener("chap-print-catalog-changed", visible);
    const timer = setInterval(visible, 60000),
      channel =
        "BroadcastChannel" in window
          ? new BroadcastChannel("chap-print-catalog")
          : null;
    if (channel) channel.onmessage = visible;
    return () => {
      controller.current?.abort();
      clearInterval(timer);
      channel?.close();
      window.removeEventListener("focus", visible);
      document.removeEventListener("visibilitychange", visible);
      window.removeEventListener("chap-print-catalog-changed", visible);
    };
  }, [enabled, reload]);
  return (
    <Context
      value={{
        items: data?.items ?? [],
        options: data?.options ?? null,
        error,
        loading: !data && !error,
        reload,
      }}
    >
      {children}
    </Context>
  );
}
export function usePrintCatalog() {
  const value = useContext(Context);
  if (!value)
    throw new Error("The print form requires its SQL catalog provider.");
  return value;
}
export function CatalogStatus() {
  const state = usePrintCatalog();
  return (
    <div
      className={state.error ? "error-message" : "notice"}
      role={state.error ? "alert" : "status"}
    >
      {state.error || "در حال دریافت گزینه‌های چاپ…"}
      {state.error && (
        <button
          type="button"
          className="button button-sm button-outline"
          onClick={state.reload}
        >
          تلاش دوباره
        </button>
      )}
    </div>
  );
}
