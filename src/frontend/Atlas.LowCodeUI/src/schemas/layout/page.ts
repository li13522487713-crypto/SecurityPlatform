/**
 * 【布局 IV-4.3 Page】
 * PageSchemaBuilder：支持 body/aside/toolbar/initApi/interval/pullRefresh/asideResizor/cssVars
 */
import type { AmisSchema } from "@/types/amis";

export interface PageSchemaOptions {
  /** 页面标题 */
  title?: string;
  /** 副标题 */
  subTitle?: string;
  /** 页面备注 */
  remark?: string;
  /** 页面主体内容 */
  body: AmisSchema | AmisSchema[];
  /** 侧边栏内容 */
  aside?: AmisSchema | AmisSchema[];
  /** 侧边栏位置 */
  asidePosition?: "left" | "right";
  /** 侧边栏宽度 */
  asideWidth?: number | string;
  /** 是否可调整侧边栏宽度 */
  asideResizor?: boolean;
  /** 侧边栏最小/最大宽度 */
  asideMinWidth?: number;
  asideMaxWidth?: number;
  /** 顶部工具栏 */
  toolbar?: AmisSchema | AmisSchema[];
  /** 初始化数据 API */
  initApi?: string | Record<string, unknown>;
  /** 轮询间隔（毫秒） */
  interval?: number;
  /** 是否静默轮询（不显示 loading） */
  silentPolling?: boolean;
  /** 停止轮询条件 */
  stopAutoRefreshWhen?: string;
  /** CSS 变量覆盖 */
  cssVars?: Record<string, string>;
  /** 附加 className */
  className?: string;
  /** 附加 body className */
  bodyClassName?: string;
  /** 头部区域 className */
  headerClassName?: string;
  /** 是否显示错误信息 */
  showErrorMsg?: boolean;
}

/**
 * 创建 Page Schema
 *
 * @example
 * ```ts
 * pageSchema({
 *   title: '用户管理',
 *   toolbar: [{ type: 'button', label: '新建', actionType: 'dialog' }],
 *   body: crudSchema({ ... }),
 *   aside: menuSchema({ ... }),
 *   asideWidth: 240,
 *   initApi: '/api/v1/dashboard/summary',
 *   interval: 60000,
 * })
 * ```
 */
export function pageSchema(opts: PageSchemaOptions): AmisSchema {
  return {
    type: "page",
    ...(opts.title ? { title: opts.title } : {}),
    ...(opts.subTitle ? { subTitle: opts.subTitle } : {}),
    ...(opts.remark ? { remark: opts.remark } : {}),
    body: opts.body,
    ...(opts.aside ? { aside: opts.aside } : {}),
    ...(opts.asidePosition ? { asidePosition: opts.asidePosition } : {}),
    ...(opts.asideWidth ? { asideWidth: opts.asideWidth } : {}),
    ...(opts.asideResizor ? { asideResizor: true } : {}),
    ...(opts.asideMinWidth ? { asideMinWidth: opts.asideMinWidth } : {}),
    ...(opts.asideMaxWidth ? { asideMaxWidth: opts.asideMaxWidth } : {}),
    ...(opts.toolbar ? { toolbar: opts.toolbar } : {}),
    ...(opts.initApi ? { initApi: opts.initApi } : {}),
    ...(opts.interval ? { interval: opts.interval } : {}),
    ...(opts.silentPolling ? { silentPolling: true } : {}),
    ...(opts.stopAutoRefreshWhen ? { stopAutoRefreshWhen: opts.stopAutoRefreshWhen } : {}),
    ...(opts.cssVars ? { cssVars: opts.cssVars } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    ...(opts.bodyClassName ? { bodyClassName: opts.bodyClassName } : {}),
    ...(opts.headerClassName ? { headerClassName: opts.headerClassName } : {}),
    ...(opts.showErrorMsg !== undefined ? { showErrorMsg: opts.showErrorMsg } : {}),
  };
}
