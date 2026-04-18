/**
 * @atlas/lowcode-component-registry — 组件注册表（M06 C06-1）。
 *
 * 强约束（PLAN.md §1.3 #4）：
 * - 组件实现内 **禁止** fetch / import workflow client / 硬写业务逻辑；
 * - 由 ./principles 校验器在注册期检查，违反者抛错（CI 守门）。
 */

import type { ComponentMeta } from '@atlas/lowcode-schema';
import { ComponentMetaZod } from '@atlas/lowcode-schema';
import { ensureMetadataDriven, type ComponentImplementationDescriptor } from './principles';

const REGISTRY = new Map<string, ComponentMeta>();
/** 实现描述符（用于元数据驱动校验）。*/
const IMPL_DESCRIPTORS = new Map<string, ComponentImplementationDescriptor>();

export interface RegisterOptions {
  /** 实现描述符：声明该组件实现是否引用了禁止的全局符号。*/
  implementationDescriptor?: ComponentImplementationDescriptor;
}

export function registerComponent(meta: ComponentMeta, options?: RegisterOptions): void {
  const parsed = ComponentMetaZod.safeParse(meta);
  if (!parsed.success) {
    throw new Error(`registerComponent: ComponentMeta 校验失败 (${meta.type}): ${JSON.stringify(parsed.error.format())}`);
  }
  if (REGISTRY.has(meta.type)) {
    throw new Error(`registerComponent: 组件 type=${meta.type} 已注册，禁止覆盖`);
  }
  if (options?.implementationDescriptor) {
    ensureMetadataDriven(meta.type, options.implementationDescriptor);
    IMPL_DESCRIPTORS.set(meta.type, options.implementationDescriptor);
  }
  REGISTRY.set(meta.type, meta);
}

export function getRegistry(): ReadonlyMap<string, ComponentMeta> {
  return REGISTRY;
}

export function getMeta(type: string): ComponentMeta | undefined {
  return REGISTRY.get(type);
}

export function listMetas(): ReadonlyArray<ComponentMeta> {
  return Array.from(REGISTRY.values());
}

export function getImplementationDescriptor(type: string): ComponentImplementationDescriptor | undefined {
  return IMPL_DESCRIPTORS.get(type);
}

/** 仅供测试：清空注册表。*/
export function __resetRegistryForTesting(): void {
  REGISTRY.clear();
  IMPL_DESCRIPTORS.clear();
}

export * from './principles';
export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-component-registry' as const;
