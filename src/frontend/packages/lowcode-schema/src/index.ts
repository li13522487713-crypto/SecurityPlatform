/**
 * @atlas/lowcode-schema — 入口（M01）。
 *
 * 17 类完整类型 + zod 校验 + guards + migrate + shared-types 子模块。
 * 详见 PLAN.md §M01 与 docs/lowcode-runtime-spec.md。
 */

export * as Shared from './shared';
export * from './shared/enums';
export type {
  JsonValue,
  JsonObject,
  JsonArray,
  JsonPrimitive
} from './shared/json';

export * from './types';
export * from './zod';
export * from './guards';
export * from './migrate';

/** 标记常量（与 M00 骨架兼容，防止 isolatedModules 空模块错误。已迁移到正式 src/index.ts）。*/
export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-schema' as const;
