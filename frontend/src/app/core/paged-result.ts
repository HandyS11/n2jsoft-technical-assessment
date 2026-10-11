export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** At least 1: an empty list still shows as page 1 of 1. */
export function pageCountOf(page: PagedResult<unknown>): number {
  return Math.max(1, Math.ceil(page.totalCount / page.pageSize));
}
