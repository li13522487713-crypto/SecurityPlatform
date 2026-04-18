/**
 * 47 组件实现集中导出（M06 P1-1）。
 *
 * 由 lowcode-runtime-web / lowcode-preview-web / lowcode-studio-web 通过
 * `getComponentImplementation(type)` 拿到 React.FC，再以 RuntimeContext 注入事件 / 内容参数 / 适配器。
 *
 * 强约束（PLAN §1.3 #4）：
 *  - 任意组件实现禁止直调 /api/runtime/* 或 fetch；外部能力一律通过 props.fireEvent / props.getContentParam 注入；
 *  - AI 类组件（AiChat 等）通过 props.chatflowAdapter（由 RuntimeContext 注入）使用，不在本包硬编码 URL；
 *  - lint / test 守门：components-web 包内 grep `/api/runtime/` `fetch(` 必须 0 命中（除注释）。
 */
import { LAYOUT_COMPONENTS } from './layout';
import { DISPLAY_COMPONENTS } from './display';
import { INPUT_COMPONENTS } from './input';
import { AI_COMPONENTS } from './ai';
import { DATA_COMPONENTS } from './data';
import type { ComponentImplementationRegistry, ComponentRenderer } from './runtime-types';

export type { ComponentImplementationRegistry, ComponentRenderer, ComponentRenderContext, FireEvent, GetContentParam } from './runtime-types';
export {
  ALL_IMPLEMENTATION_TYPES,
  AI_IMPLEMENTATION_TYPES,
  DATA_IMPLEMENTATION_TYPES,
  DISPLAY_IMPLEMENTATION_TYPES,
  INPUT_IMPLEMENTATION_TYPES,
  IMPLEMENTATION_TYPE_SET,
  LAYOUT_IMPLEMENTATION_TYPES,
  hasImplementation
} from './implementation-keys';

const ALL_COMPONENTS: ComponentImplementationRegistry = {
  ...LAYOUT_COMPONENTS,
  ...DISPLAY_COMPONENTS,
  ...INPUT_COMPONENTS,
  ...AI_COMPONENTS,
  ...DATA_COMPONENTS
};

export function getComponentImplementation(type: string): ComponentRenderer | undefined {
  return ALL_COMPONENTS[type];
}

export function listComponentImplementations(): Readonly<ComponentImplementationRegistry> {
  return ALL_COMPONENTS;
}

export const COMPONENT_IMPLEMENTATION_COUNT = Object.keys(ALL_COMPONENTS).length;
