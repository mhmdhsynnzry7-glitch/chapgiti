/** A logical first page also exists for an empty result set. */
export function paginationState(page: number, pageSize: number, total: number) {
  const size = Number.isFinite(pageSize) && pageSize >= 1 ? Math.floor(pageSize) : 20;
  const count = Number.isFinite(total) && total > 0 ? Math.floor(total) : 0;
  const pages = Math.max(1, Math.ceil(count / size));
  const requested = Number.isFinite(page) ? Math.floor(page) : 1;
  const current = Math.min(pages, Math.max(1, requested));
  return { current, pages, total: count, previous: Math.max(1, current - 1), next: Math.min(pages, current + 1) };
}
