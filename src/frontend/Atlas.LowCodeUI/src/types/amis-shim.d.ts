/**
 * amis / amis-editor 模块类型补丁
 * 为缺少类型声明的模块提供基础类型
 */

declare module "amis" {
  import type { ReactElement } from "react";

  export interface AmisRenderOptions {
    data?: unknown;
    env?: Record<string, unknown>;
    locale?: string;
    theme?: string;
  }

  export function render(
    schema: Record<string, unknown>,
    props?: Record<string, unknown>,
    env?: Record<string, unknown>,
  ): ReactElement;
}

declare module "amis-core" {
  export interface RendererConfig {
    type: string;
    name?: string;
    component: unknown;
    [key: string]: unknown;
  }

  export function Renderer(config: RendererConfig): (target: unknown) => void;
}

declare module "amis-editor" {
  import type { ComponentType } from "react";

  export const Editor: ComponentType<Record<string, unknown>>;

  export class BasePlugin {
    rendererName?: string;
    name?: string;
    description?: string;
    tags?: string[];
    icon?: string;
    scaffold?: Record<string, unknown>;
    previewSchema?: Record<string, unknown>;
    [key: string]: unknown;
  }

  export function registerEditorPlugin(plugin: unknown): void;
}
