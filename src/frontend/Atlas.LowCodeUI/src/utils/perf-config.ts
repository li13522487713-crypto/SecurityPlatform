/**
 * 【高级 VII-7.5 性能优化】
 * 性能优化配置工具：API 缓存、虚拟滚动、延迟加载
 */
import type { AmisSchema } from "@/types/amis";

/** API 缓存配置 */
export interface ApiCacheConfig {
  /** 缓存时间（毫秒，默认 300000 = 5分钟） */
  cache?: number;
}

/**
 * 为 API 配置添加缓存
 *
 * @example
 * ```ts
 * withApiCache({ method: 'GET', url: '/api/v1/options' }, 60000)
 * ```
 */
export function withApiCache(
  api: string | Record<string, unknown>,
  cacheDuration = 300000,
): Record<string, unknown> {
  if (typeof api === "string") {
    return { url: api, cache: cacheDuration };
  }
  return { ...api, cache: cacheDuration };
}

/**
 * 创建带虚拟滚动的 CRUD Schema 配置片段
 *
 * @description
 * 大数据量场景下启用虚拟滚动，仅渲染可视区域内的行。
 * 需要设置固定行高（itemHeight）和固定容器高度。
 */
export function virtualScrollConfig(opts: {
  /** 行高（默认 40px） */
  itemHeight?: number;
  /** 容器高度 */
  height?: number | string;
  /** 预加载行数（上下各多渲染的行数） */
  bufferSize?: number;
} = {}): Record<string, unknown> {
  return {
    autoFillHeight: true,
    ...(opts.height ? { style: { height: typeof opts.height === "number" ? `${opts.height}px` : opts.height } } : {}),
  };
}

/**
 * 延迟加载配置
 *
 * @description
 * 为组件设置延迟加载（deferLoad），仅在组件进入可视区域时才初始化。
 */
export function withDeferLoad(schema: AmisSchema): AmisSchema {
  return {
    ...schema,
    deferLoad: true,
  };
}

/**
 * 图片懒加载配置
 */
export function lazyImage(opts: {
  name: string;
  src?: string;
  thumbMode?: "w-full" | "h-full" | "contain" | "cover";
  thumbRatio?: "1:1" | "4:3" | "16:9";
  originalSrc?: string;
  enlargeAble?: boolean;
  className?: string;
}): AmisSchema {
  return {
    type: "image",
    name: opts.name,
    ...(opts.src ? { src: opts.src } : {}),
    thumbMode: opts.thumbMode ?? "cover",
    ...(opts.thumbRatio ? { thumbRatio: opts.thumbRatio } : {}),
    ...(opts.originalSrc ? { originalSrc: opts.originalSrc } : {}),
    ...(opts.enlargeAble ? { enlargeAble: true } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/**
 * 接口防抖配置
 */
export function withDebounce(
  api: string | Record<string, unknown>,
  delay = 300,
): Record<string, unknown> {
  if (typeof api === "string") {
    return { url: api, sendOn: `\${NOW() - __lastFetch > ${delay}}` };
  }
  return { ...api, sendOn: `\${NOW() - __lastFetch > ${delay}}` };
}

/**
 * 停止轮询条件
 */
export function stopPollingWhen(condition: string): Record<string, string> {
  return { stopAutoRefreshWhen: condition };
}

/**
 * 性能优化最佳实践文档片段
 */
export const PERF_BEST_PRACTICES = `
## AMIS 性能优化最佳实践

### 1. 接口缓存
- 对于不经常变化的数据（如选项列表），设置 \`cache\` 参数
- 推荐缓存时间：选项列表 5~10 分钟，配置数据 30 分钟

### 2. 虚拟滚动
- 超过 200 行数据的表格建议启用虚拟滚动
- 需要设置固定行高和容器高度

### 3. 延迟加载
- Tab 面板中的内容使用 mountOnEnter + unmountOnExit
- 折叠面板中的复杂组件使用 deferLoad

### 4. 接口优化
- 使用 sendOn 控制接口发送条件，避免无效请求
- 使用 trackExpression 仅在依赖数据变化时刷新图表
- 分页接口默认每页 20 条，避免过大

### 5. Schema 优化
- 避免深层嵌套（超过 5 层）
- 大型 Schema 拆分为子 Schema，通过 schemaApi 动态加载
- 使用 static 模式展示只读数据，比 form 更轻量
`;
