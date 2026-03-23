/**
 * 【表单 II-2.3 布局模式】
 * 3 种表单布局模式：default / horizontal / inline
 */
import type { AmisSchema } from "@/types/amis";

/** 表单布局模式 */
export type FormLayoutMode = "normal" | "horizontal" | "inline";

/** 水平布局配置 */
export interface HorizontalConfig {
  left?: number;
  right?: number;
  offset?: number;
  leftFixed?: string | number;
}

/** 表单 Schema 选项 */
export interface FormSchemaOptions {
  /** 表单 title */
  title?: string;
  /** 表单控件数组 */
  body: AmisSchema[];
  /** 布局模式 */
  mode?: FormLayoutMode;
  /** 水平布局配置（mode=horizontal 时有效） */
  horizontal?: HorizontalConfig;
  /** 提交 API */
  api?: string | Record<string, unknown>;
  /** 初始化数据 API */
  initApi?: string | Record<string, unknown>;
  /** 是否自动聚焦第一个表单项 */
  autoFocus?: boolean;
  /** 是否显示提交按钮 */
  submitText?: string;
  /** 静态展示模式 */
  static?: boolean;
  /** 提交成功后动作 */
  actions?: AmisSchema[];
  /** 调试模式 */
  debug?: boolean;
  /** 附加 CSS class */
  className?: string;
  /** 提交成功后消息 */
  messages?: { saveSuccess?: string; saveFailed?: string };
  /** 表单重置后需要触发的动作 */
  resetAfterSubmit?: boolean;
  /** 提交执行前校验 */
  preventEnterSubmit?: boolean;
}

/**
 * 创建表单 Schema
 *
 * @example
 * ```ts
 * // 默认布局
 * formSchema({
 *   body: [inputText({ name: 'username', label: '用户名' })],
 *   api: '/api/v1/users',
 * })
 *
 * // 水平布局
 * formSchema({
 *   body: [...],
 *   mode: 'horizontal',
 *   horizontal: { left: 3, right: 9 },
 * })
 *
 * // 行内布局
 * formSchema({
 *   body: [...],
 *   mode: 'inline',
 * })
 * ```
 */
export function formSchema(opts: FormSchemaOptions): AmisSchema {
  return {
    type: "form",
    ...(opts.title ? { title: opts.title } : {}),
    body: opts.body,
    ...(opts.mode ? { mode: opts.mode } : {}),
    ...(opts.horizontal ? { horizontal: opts.horizontal } : {}),
    ...(opts.api ? { api: opts.api } : {}),
    ...(opts.initApi ? { initApi: opts.initApi } : {}),
    ...(opts.autoFocus ? { autoFocus: true } : {}),
    ...(opts.submitText !== undefined ? { submitText: opts.submitText } : {}),
    ...(opts.static ? { static: true } : {}),
    ...(opts.actions ? { actions: opts.actions } : {}),
    ...(opts.debug ? { debug: true } : {}),
    ...(opts.className ? { className: opts.className } : {}),
    ...(opts.messages ? { messages: opts.messages } : {}),
    ...(opts.resetAfterSubmit ? { resetAfterSubmit: true } : {}),
    ...(opts.preventEnterSubmit ? { preventEnterSubmit: true } : {}),
  };
}

/** 默认布局示例 Schema */
export function defaultLayoutExample(body: AmisSchema[]): AmisSchema {
  return formSchema({ body, mode: "normal" });
}

/** 水平布局示例 Schema */
export function horizontalLayoutExample(body: AmisSchema[], horizontal?: HorizontalConfig): AmisSchema {
  return formSchema({
    body,
    mode: "horizontal",
    horizontal: horizontal ?? { left: 3, right: 9 },
  });
}

/** 行内布局示例 Schema */
export function inlineLayoutExample(body: AmisSchema[]): AmisSchema {
  return formSchema({ body, mode: "inline" });
}
