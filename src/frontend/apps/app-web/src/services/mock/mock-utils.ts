import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-react-core/types";

/**
 * 通用 mock 工具，强类型 + 与现有 ApiResponse / PagedResult 信封对齐。
 *
 * 设计原则：
 * - 所有 mock 必须返回 `Promise<T>` 或 `Promise<PagedResult<T>>`，与真实 service 接口签名一致。
 * - 内置 `MOCK_DELAY_MS` 模拟网络延时，便于排查 loading / 状态切换问题。
 * - 不直接抛错；如需测试错误分支，请显式调用 `mockReject()`。
 */

export const MOCK_DELAY_MS = 200;

export function genMockTraceId(): string {
  return `mock-${Math.random().toString(36).slice(2, 10)}`;
}

function sleep(ms: number): Promise<void> {
  if (typeof window === "undefined") {
    return Promise.resolve();
  }
  return new Promise(resolve => setTimeout(resolve, ms));
}

/** 直接返回数据（已经在外层使用方包装为 ApiResponse 的情况下使用）。 */
export async function mockResolve<T>(data: T, delay = MOCK_DELAY_MS): Promise<T> {
  await sleep(delay);
  return data;
}

/** 包装为成功的 `ApiResponse<T>`。供需要透传 ApiResponse 的服务调用方使用。 */
export async function mockApiResponse<T>(data: T, delay = MOCK_DELAY_MS): Promise<ApiResponse<T>> {
  await sleep(delay);
  return {
    success: true,
    code: "SUCCESS",
    message: "OK",
    traceId: genMockTraceId(),
    data
  };
}

/** 标准分页：在内存中按 `pageIndex/pageSize` 切片。 */
export async function mockPaged<T>(
  source: T[],
  request: PagedRequest,
  delay = MOCK_DELAY_MS
): Promise<PagedResult<T>> {
  await sleep(delay);
  const pageIndex = request.pageIndex ?? 1;
  const pageSize = request.pageSize ?? 10;
  const start = (pageIndex - 1) * pageSize;
  return {
    pageIndex,
    pageSize,
    total: source.length,
    items: source.slice(start, start + pageSize)
  };
}

/** 制造错误响应（仅在显式需要测试错误分支时使用）。 */
export async function mockReject(
  code: "VALIDATION_ERROR" | "UNAUTHORIZED" | "FORBIDDEN" | "NOT_FOUND" | "SERVER_ERROR" = "SERVER_ERROR",
  message = "mock failure",
  delay = MOCK_DELAY_MS
): Promise<never> {
  await sleep(delay);
  throw Object.assign(new Error(message), { code, traceId: genMockTraceId() });
}

/**
 * 简易模糊匹配，用于 keyword 过滤。统一行为，避免每个 mock 自行实现。
 */
export function matchKeyword(value: string | undefined | null, keyword?: string): boolean {
  if (!keyword) {
    return true;
  }
  if (!value) {
    return false;
  }
  return value.toLowerCase().includes(keyword.toLowerCase());
}
