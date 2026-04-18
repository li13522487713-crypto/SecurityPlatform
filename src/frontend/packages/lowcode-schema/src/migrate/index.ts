/**
 * @atlas/lowcode-schema/migrate — Schema 升级器与降级器（M01）。
 *
 * 设计要点：
 * - 当前 Schema 版本由 SCHEMA_VERSION 常量定义（v1）。
 * - 未来增加 v2 时，注册 v1→v2 的升级器与 v2→v1 的降级器；保持向后兼容窗口 ≥ 6 个月（与 AGENTS.md 控制器版本策略一致）。
 * - 升级器必须是纯函数：同一输入 → 同一输出；不依赖外部状态。
 */

import { SCHEMA_VERSION, type SchemaVersion } from '../shared/enums';
import type { JsonObject } from '../shared/json';

export type SchemaUpgrader = (input: JsonObject) => JsonObject;
export type SchemaDowngrader = (input: JsonObject) => JsonObject;

interface MigrationStep {
  fromVersion: string;
  toVersion: string;
  upgrade: SchemaUpgrader;
  downgrade?: SchemaDowngrader;
}

const STEPS: MigrationStep[] = [
  // 当前阶段没有跨版本步骤；保留注册表结构以便未来 v1→v2 加入。
];

export const CURRENT_SCHEMA_VERSION: SchemaVersion = SCHEMA_VERSION;

/** 注册一个迁移步骤（M01-M20 期间允许调用，但禁止覆盖既有版本对）。*/
export const registerMigrationStep = (step: MigrationStep): void => {
  if (STEPS.some((s) => s.fromVersion === step.fromVersion && s.toVersion === step.toVersion)) {
    throw new Error(`迁移步骤 ${step.fromVersion} → ${step.toVersion} 已存在，禁止覆盖`);
  }
  STEPS.push(step);
};

/** 把任意旧版 schema 升级到当前版本。*/
export const upgradeSchema = (input: JsonObject): JsonObject => {
  let current = input;
  let version = (current.schemaVersion as string | undefined) ?? CURRENT_SCHEMA_VERSION;
  // 循环找到下一段 fromVersion=version 的步骤直到达成 CURRENT_SCHEMA_VERSION。
  // 防止无限循环：步骤总数即上限。
  for (let i = 0; i < STEPS.length + 1; i++) {
    if (version === CURRENT_SCHEMA_VERSION) return { ...current, schemaVersion: CURRENT_SCHEMA_VERSION };
    const step = STEPS.find((s) => s.fromVersion === version);
    if (!step) {
      throw new Error(`找不到自版本 ${version} 升级到 ${CURRENT_SCHEMA_VERSION} 的迁移步骤`);
    }
    current = step.upgrade(current);
    version = step.toVersion;
  }
  throw new Error(`升级超过最大迭代次数（疑似环），当前版本：${version}`);
};

/** 把当前版本 schema 降级到目标旧版（如降级链不可用则抛错）。*/
export const downgradeSchema = (input: JsonObject, toVersion: string): JsonObject => {
  let current = input;
  let version = CURRENT_SCHEMA_VERSION as string;
  for (let i = 0; i < STEPS.length + 1; i++) {
    if (version === toVersion) return { ...current, schemaVersion: toVersion };
    const step = [...STEPS].reverse().find((s) => s.toVersion === version && s.downgrade);
    if (!step || !step.downgrade) {
      throw new Error(`找不到自版本 ${version} 降级到 ${toVersion} 的迁移步骤`);
    }
    current = step.downgrade(current);
    version = step.fromVersion;
  }
  throw new Error(`降级超过最大迭代次数（疑似环），当前版本：${version}`);
};

/** 仅供测试用：清空注册表（不在生产代码中使用）。*/
export const __resetMigrationStepsForTesting = (): void => {
  STEPS.length = 0;
};
