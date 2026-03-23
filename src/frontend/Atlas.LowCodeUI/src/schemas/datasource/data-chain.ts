/**
 * 【数据源 VI-6.1 数据域/数据链】
 * 数据域与数据链继承机制说明 + initData 注入
 */
import type { AmisSchema } from "@/types/amis";

/**
 * AMIS 数据链说明
 *
 * AMIS 中的数据域（Data Scope）和数据链（Data Chain）是核心概念：
 *
 * 1. **数据域**：每个组件拥有自己的数据域（如 Page、Form、CRUD、Service）
 * 2. **数据链**：子组件可以访问父组件的数据域，形成数据链：
 *    Page → Service → CRUD → Form
 *    外层的数据变量可以被内层组件通过 `${variable}` 引用
 * 3. **同名覆盖**：子组件数据域中的变量会覆盖父组件同名变量
 * 4. **canAccessSuperData**：部分组件默认开启向上查找（如 Form），
 *    CRUD 默认关闭（需手动设置 `canAccessSuperData: true`）
 */

/**
 * 创建带初始数据的 Service 组件
 *
 * @description
 * Service 组件是 AMIS 中用于注入数据的关键容器。
 * 它可以通过 data 属性注入静态数据，或通过 api/schemaApi 动态加载。
 *
 * @example
 * ```ts
 * dataService({
 *   data: { projectName: 'Atlas', version: '1.0' },
 *   body: [
 *     { type: 'tpl', tpl: '项目: ${projectName} v${version}' },
 *   ],
 * })
 * ```
 */
export function dataService(opts: {
  data?: Record<string, unknown>;
  api?: string | Record<string, unknown>;
  schemaApi?: string | Record<string, unknown>;
  body: AmisSchema | AmisSchema[];
  initFetch?: boolean;
  interval?: number;
  silentPolling?: boolean;
  name?: string;
  className?: string;
}): AmisSchema {
  return {
    type: "service",
    ...(opts.data ? { data: opts.data } : {}),
    ...(opts.api ? { api: opts.api } : {}),
    ...(opts.schemaApi ? { schemaApi: opts.schemaApi } : {}),
    body: opts.body,
    ...(opts.initFetch !== undefined ? { initFetch: opts.initFetch } : {}),
    ...(opts.interval ? { interval: opts.interval } : {}),
    ...(opts.silentPolling ? { silentPolling: true } : {}),
    ...(opts.name ? { name: opts.name } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/**
 * 设置组件的数据域访问权限
 */
export function withSuperData(schema: AmisSchema, canAccess = true): AmisSchema {
  return { ...schema, canAccessSuperData: canAccess };
}

/**
 * 为组件注入静态数据
 */
export function withData(schema: AmisSchema, data: Record<string, unknown>): AmisSchema {
  return { ...schema, data };
}

/**
 * 创建 Page → Service → CRUD 数据链示例
 */
export function dataChainExample(): AmisSchema {
  return {
    type: "page",
    data: {
      globalTitle: "全局标题（Page 数据域）",
      apiBase: "/api/v1",
    },
    body: {
      type: "service",
      data: {
        serviceInfo: "Service 层数据",
      },
      body: [
        {
          type: "tpl",
          tpl: "Page 数据: ${globalTitle} | Service 数据: ${serviceInfo}",
        },
        {
          type: "crud",
          api: "${apiBase}/users",
          canAccessSuperData: true,
          columns: [
            { name: "id", label: "ID" },
            { name: "name", label: "名称" },
          ],
        },
      ],
    },
  };
}
