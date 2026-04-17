import type { BindingSchema } from './binding';
import type { ContentParamKind } from '../shared/enums';
import type { JsonObject } from '../shared/json';

/**
 * ContentParamSchema —— 内容参数 6 类独立机制（docx §U11，PLAN.md §M05/M06）。
 *
 * 与 BindingSchema 的差异：
 * - BindingSchema 描述「单个 prop 的取值来源」。
 * - ContentParamSchema 描述「组件接收哪类内容（文案 / 图片 / 数据 / 链接 / 媒体 / AI 内容）+ 多种取值方式的统一封装」。
 *
 * 详见 docs/lowcode-content-params-spec.md。
 */
export type ContentParamSchema =
  | TextContentParam
  | ImageContentParam
  | DataContentParam
  | LinkContentParam
  | MediaContentParam
  | AiContentParam;

export interface ContentParamBase {
  kind: ContentParamKind;
  /** 参数编码（应用内唯一），用于在表达式 / props 引用：`contentParam.<code>`。*/
  code: string;
  /** 描述。*/
  description?: string;
}

export interface TextContentParam extends ContentParamBase {
  kind: 'text';
  /** 模式：static（静态文本）/ template（模板字符串）/ i18n（i18n key）。*/
  mode: 'static' | 'template' | 'i18n';
  /** 静态文本 / 模板字符串原文 / i18n key。*/
  source: string;
  /** 模板模式下，可注入的变量上下文（M02 模板引擎使用）。*/
  context?: JsonObject;
}

export interface ImageContentParam extends ContentParamBase {
  kind: 'image';
  /** 模式：url / fileHandle / imageId / placeholder。*/
  mode: 'url' | 'fileHandle' | 'imageId' | 'placeholder';
  source: string;
  /** 占位图（fallback）。*/
  placeholder?: string;
}

export interface DataContentParam extends ContentParamBase {
  kind: 'data';
  /** 数据来源 binding（接 workflow output 或 variable）。*/
  source: BindingSchema;
  /** 是否预期为数组（用于设计期校验）。*/
  expectArray?: boolean;
}

export interface LinkContentParam extends ContentParamBase {
  kind: 'link';
  /** 链接类型：internal（内部路由）/ external（外部 URL，受 webview 白名单约束）。*/
  linkType: 'internal' | 'external';
  href: string;
  target?: '_self' | '_blank';
}

export interface MediaContentParam extends ContentParamBase {
  kind: 'media';
  /** video / audio。*/
  mediaType: 'video' | 'audio';
  url: string;
  cover?: string;
}

export interface AiContentParam extends ContentParamBase {
  kind: 'ai';
  /** 子模式：chatflow_stream（chatflow 流式输出渲染）/ ai_card（AI 卡片配置）。*/
  mode: 'chatflow_stream' | 'ai_card';
  /** 当 mode=chatflow_stream 时使用。*/
  chatflowId?: string;
  /** 当 mode=ai_card 时使用，AI 卡片完整配置 JSON。*/
  cardConfig?: JsonObject;
}
