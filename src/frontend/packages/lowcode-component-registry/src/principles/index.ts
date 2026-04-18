/**
 * 元数据驱动校验器（M06 C06-2）。
 *
 * 强约束（PLAN.md §1.3 #4 + §M06 C06-2）：
 * - 组件实现禁止 fetch / direct workflow / direct chatflow / direct files API；
 * - 任何业务逻辑必须经事件 → 动作链 → dispatch 路由（M13 落地）；
 * - 实现包通过提交 ComponentImplementationDescriptor 声明：是否引用了 forbidden 符号。
 *
 * 实际 CI 守门由两层：
 * 1. 运行时 ensureMetadataDriven：注册期校验。
 * 2. 静态扫描 grepForbiddenInImplementation：扫描 lowcode-components-{web,mini} 源码（M06 / M15 提供脚本）。
 */

export const FORBIDDEN_GLOBALS = [
  'fetch',
  'XMLHttpRequest',
  'window.fetch',
  '@coze-arch/bot-api/workflow_api',
  '@atlas/workflow-api',
  '@atlas/chatflow-api',
  '@atlas/files-api'
] as const;

export interface ComponentImplementationDescriptor {
  /** 组件实现内引用了哪些全局符号（由实现包静态分析或开发者手动声明）。*/
  importedGlobals: ReadonlyArray<string>;
  /** 组件实现内引用了哪些 npm/workspace 包。*/
  importedPackages: ReadonlyArray<string>;
}

export class MetadataDrivenViolationError extends Error {
  constructor(public readonly componentType: string, public readonly violations: string[]) {
    super(`组件 ${componentType} 违反元数据驱动原则：${violations.join(', ')}`);
    this.name = 'MetadataDrivenViolationError';
  }
}

export function ensureMetadataDriven(type: string, descriptor: ComponentImplementationDescriptor): void {
  const violations: string[] = [];
  for (const sym of FORBIDDEN_GLOBALS) {
    if (descriptor.importedGlobals.includes(sym) || descriptor.importedPackages.includes(sym)) {
      violations.push(sym);
    }
  }
  if (violations.length > 0) {
    throw new MetadataDrivenViolationError(type, violations);
  }
}
