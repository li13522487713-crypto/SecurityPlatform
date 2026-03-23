import type { AmisEnv, AmisEnvOptions, AmisFetcherConfig, AmisFetcherResult, AmisNotifyType } from "@/types/amis";

/**
 * 默认 fetcher：基于浏览器 fetch API 的通用请求器
 * 宿主项目可通过 options.fetcher 覆盖
 */
async function defaultFetcher(config: AmisFetcherConfig): Promise<AmisFetcherResult> {
  const method = (config.method ?? "GET").toUpperCase();
  const headers = new Headers(config.headers ?? {});

  let body: BodyInit | undefined;
  if (!["GET", "HEAD"].includes(method) && config.data != null) {
    if (config.data instanceof FormData) {
      body = config.data as FormData;
    } else if (typeof config.data === "string") {
      if (!headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
      }
      body = config.data;
    } else {
      if (!headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
      }
      body = JSON.stringify(config.data);
    }
  }

  try {
    const response = await fetch(config.url, { method, headers, body });
    const json = (await response.json()) as Record<string, unknown>;

    return {
      data: json,
      ok: response.ok,
      status: response.status,
      msg: typeof json.message === "string" ? json.message : undefined,
    };
  } catch (error) {
    const msg = error instanceof Error ? error.message : "请求失败";
    return {
      data: null,
      ok: false,
      status: 0,
      msg,
    };
  }
}

/** 默认通知：使用 console */
function defaultNotify(type: AmisNotifyType, msg: string): void {
  const logMap: Record<AmisNotifyType, (...args: unknown[]) => void> = {
    info: console.info,
    success: console.log,
    warning: console.warn,
    error: console.error,
  };
  logMap[type](`[AMIS ${type}]`, msg);
}

/** 默认弹窗 */
function defaultAlert(msg: string): void {
  if (typeof window !== "undefined") {
    window.alert(msg);
  }
}

/** 默认确认 */
async function defaultConfirm(msg: string): Promise<boolean> {
  if (typeof window !== "undefined") {
    return window.confirm(msg);
  }
  return false;
}

/** 默认复制 */
function defaultCopy(content: string): void {
  if (typeof navigator !== "undefined" && navigator.clipboard) {
    void navigator.clipboard.writeText(content);
  }
}

/**
 * 构造 AMIS env 对象
 *
 * @description
 * 通用版本，不依赖 Atlas.WebApp 内部模块。
 * 宿主项目可通过 options 参数注入自定义的 fetcher / notify / alert / confirm 等函数。
 *
 * @example
 * ```ts
 * // 基础用法（使用默认 fetch）
 * const env = useAmisEnv();
 *
 * // 注入自定义 fetcher（例如使用 axios 或宿主项目的请求封装）
 * const env = useAmisEnv({
 *   fetcher: myCustomFetcher,
 *   notify: (type, msg) => antdMessage[type](msg),
 *   locale: 'zh-CN',
 * });
 * ```
 */
export function useAmisEnv(options: AmisEnvOptions = {}): AmisEnv {
  return {
    fetcher: options.fetcher ?? defaultFetcher,
    notify: options.notify ?? defaultNotify,
    alert: options.alert ?? defaultAlert,
    confirm: options.confirm ?? defaultConfirm,
    updateLocation: options.updateLocation ?? ((location: string, replace?: boolean) => {
      console.debug("[AMIS] updateLocation:", location, replace);
    }),
    copy: options.copy ?? defaultCopy,
    locale: options.locale ?? "zh-CN",
    theme: options.theme ?? "cxd",
    data: options.data ?? {},
  };
}
