"use client";

import { useEffect } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { paginationState } from "@/lib/pagination";

type Props = {
  page: number; pageSize: number; total: number; noun: string;
  onPageChange: (page: number) => void; loading?: boolean; className?: string;
};

export default function Pagination({ page, pageSize, total, noun, onPageChange, loading = false, className = "" }: Props) {
  const state = paginationState(page, pageSize, total);
  const current = state.current;
  // A refresh can remove the final page. Fetch the nearest valid page instead
  // of trapping the user on an empty page or resetting a still-valid selection.
  useEffect(() => {
    if (!loading && page !== current) onPageChange(current);
  }, [page, current, loading, onPageChange]);

  return <nav className={"giti-pagination " + className} aria-label={"صفحه‌بندی " + noun} aria-busy={loading}>
    <button type="button" className="button button-outline button-sm" disabled={loading || state.current === 1}
      onClick={() => onPageChange(state.previous)}><ChevronRight size={17} aria-hidden="true" />صفحه قبل</button>
    <span className="pagination-summary" role="status" aria-atomic="true">صفحه <strong aria-current="page">{state.current.toLocaleString("fa-IR")}</strong> از {state.pages.toLocaleString("fa-IR")} · {(state.total ? (state.current - 1) * pageSize + 1 : 0).toLocaleString("fa-IR")} تا {Math.min(state.current * pageSize, state.total).toLocaleString("fa-IR")} از {state.total.toLocaleString("fa-IR")} {noun}</span>
    <button type="button" className="button button-outline button-sm" disabled={loading || state.current === state.pages}
      onClick={() => onPageChange(state.next)}>صفحه بعد<ChevronLeft size={17} aria-hidden="true" /></button>
  </nav>;
}
