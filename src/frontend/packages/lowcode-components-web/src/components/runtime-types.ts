/**
 * 运行时渲染契约（M06 P1-1）：lowcode-components-web 暴露 ComponentRenderer 接口。
 *
 * lowcode-runtime-web / lowcode-preview-web / lowcode-studio-web 通过 type → ComponentRenderer 找到 React 实现；
 * 不强制本包依赖 RuntimeContext，所有外部能力（事件分发 / 工作流调用）通过 Props 注入。
 */
import type { ComponentSchema } from '@atlas/lowcode-schema';
import type * as React from 'react';

/**
 * 运行时事件触发函数。Renderer 仅按 ComponentMeta.supportedEvents 中声明的 EventName 调用。
 * 实际 dispatch 委托给 lowcode-action-runtime → POST /api/runtime/events/dispatch（PLAN §1.3 #2）。
 */
export type FireEvent = (eventName: string, payload?: unknown) => void;

/**
 * 内容参数访问：组件按 contentParams 声明从外部读取已绑定/已渲染的内容（如 Markdown 已渲染 HTML，
 * 图片已上传 URL，data 数据数组等）。
 */
export type GetContentParam = (kind: string, key?: string) => unknown;

export interface ComponentRenderContext {
  schema: ComponentSchema;
  /** 组件 props（已经过表达式求值的最终字面量值）。*/
  props: Record<string, unknown>;
  /** 子组件已渲染节点（递归由调用方完成）。*/
  children?: React.ReactNode;
  /** 事件触发函数。*/
  fireEvent: FireEvent;
  /** 内容参数读取函数（可选）。*/
  getContentParam?: GetContentParam;
}

export type ComponentRenderer = React.FC<ComponentRenderContext>;

/** 组件实现注册表：type → React 组件。*/
export type ComponentImplementationRegistry = Record<string, ComponentRenderer>;
