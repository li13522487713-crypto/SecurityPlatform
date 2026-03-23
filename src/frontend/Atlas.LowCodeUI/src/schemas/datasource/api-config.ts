/**
 * 【数据源 VI-6.2 API 配置】
 * ApiConfigBuilder：字符串/对象两种 API 配置，Bearer Token 动态注入
 */
import type { AmisSchema } from "@/types/amis";

/** API 配置对象格式 */
export interface ApiConfigOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  url: string;
  headers?: Record<string, string>;
  data?: Record<string, unknown>;
  dataType?: "json" | "form-data" | "form";
  /** 发送前适配器 */
  requestAdaptor?: string;
  /** 接收后适配器 */
  adaptor?: string;
  /** 自动追加 data 映射 */
  sendOn?: string;
  /** 接口缓存时间（毫秒） */
  cache?: number;
  /** 是否通过 responseData 映射响应 */
  responseData?: Record<string, unknown>;
  /** 响应格式映射 */
  replaceData?: boolean;
  /** 附加查询参数 */
  qsOptions?: Record<string, unknown>;
}

/**
 * 创建字符串形式 API 配置
 *
 * @example
 * ```ts
 * apiString("GET", "/api/v1/users")        // => "GET:/api/v1/users"
 * apiString("DELETE", "/api/v1/users/${id}") // => "DELETE:/api/v1/users/${id}"
 * ```
 */
export function apiString(method: string, url: string): string {
  if (method.toUpperCase() === "GET") return url;
  return `${method.toUpperCase()}:${url}`;
}

/**
 * 创建对象形式 API 配置
 *
 * @example
 * ```ts
 * apiConfig({
 *   method: 'POST',
 *   url: '/api/v1/users',
 *   data: { name: '${name}', email: '${email}' },
 *   headers: { 'X-Custom': 'value' },
 * })
 * ```
 */
export function apiConfig(opts: ApiConfigOptions): Record<string, unknown> {
  return {
    method: opts.method ?? "GET",
    url: opts.url,
    ...(opts.headers ? { headers: opts.headers } : {}),
    ...(opts.data ? { data: opts.data } : {}),
    ...(opts.dataType ? { dataType: opts.dataType } : {}),
    ...(opts.requestAdaptor ? { requestAdaptor: opts.requestAdaptor } : {}),
    ...(opts.adaptor ? { adaptor: opts.adaptor } : {}),
    ...(opts.sendOn ? { sendOn: opts.sendOn } : {}),
    ...(opts.cache ? { cache: opts.cache } : {}),
    ...(opts.responseData ? { responseData: opts.responseData } : {}),
    ...(opts.replaceData ? { replaceData: true } : {}),
    ...(opts.qsOptions ? { qsOptions: opts.qsOptions } : {}),
  };
}

/**
 * 创建带 Bearer Token 的 API 配置
 *
 * @description
 * 动态从 AMIS 数据域中获取 token 并注入 Authorization header。
 * 通常 token 由 useAmisEnv 的 fetcher 自动携带，但某些场景需要显式注入。
 */
export function apiWithToken(opts: ApiConfigOptions & {
  tokenField?: string;
}): Record<string, unknown> {
  const tokenField = opts.tokenField ?? "accessToken";
  return apiConfig({
    ...opts,
    headers: {
      ...opts.headers,
      Authorization: `Bearer \${${tokenField}}`,
    },
  });
}

/**
 * 创建 CRUD 数据接口（约定分页参数）
 */
export function crudApi(url: string, opts: {
  method?: string;
  pageField?: string;
  perPageField?: string;
  orderByField?: string;
  orderDirField?: string;
  extraData?: Record<string, unknown>;
} = {}): Record<string, unknown> {
  return apiConfig({
    method: (opts.method ?? "GET") as ApiConfigOptions["method"],
    url,
    data: {
      [opts.pageField ?? "page"]: "${page}",
      [opts.perPageField ?? "perPage"]: "${perPage}",
      ...(opts.orderByField ? { [opts.orderByField]: "${orderBy}" } : {}),
      ...(opts.orderDirField ? { [opts.orderDirField]: "${orderDir}" } : {}),
      ...opts.extraData,
    },
  });
}

/**
 * 创建远程选项数据源
 */
export function optionsSource(url: string, opts: {
  labelField?: string;
  valueField?: string;
  searchParam?: string;
} = {}): Record<string, unknown> {
  const searchParam = opts.searchParam ?? "keyword";
  return {
    source: `${url}?${searchParam}=\${term}`,
    ...(opts.labelField ? { labelField: opts.labelField } : {}),
    ...(opts.valueField ? { valueField: opts.valueField } : {}),
  };
}
