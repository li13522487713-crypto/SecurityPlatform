export interface PublishLogFilters {
  source: string;
  kind: string;
  pageIndex: number;
  pageSize: number;
}

export function buildDefaultPublishLogFilters(): PublishLogFilters {
  return {
    source: "",
    kind: "",
    pageIndex: 1,
    pageSize: 20
  };
}

export function normalizePositiveInt(value: string | null | undefined, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}
