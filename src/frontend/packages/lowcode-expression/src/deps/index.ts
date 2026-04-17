/**
 * 依赖追踪与反向索引（M02 C02-6）。
 *
 * 用例：
 * - 当 page.foo 变化时，需要重算哪些 binding？答：调用 reverseLookup('page.foo')。
 * - 实现：给每个 binding 抽取依赖路径，建立 path → bindingId 反向索引。
 *
 * 抽取依赖的策略：
 *  - 简单正则匹配 7 作用域根开头的 dot-path。
 *  - 不做完整 jsonata AST 解析（jsonata 暴露的 ast 结构有限，本场景只需识别变量引用即可）。
 */

import type { ScopeRoot } from '@atlas/lowcode-schema/shared';

const SCOPE_ROOT_PATTERNS: ReadonlyArray<{ scope: ScopeRoot; prefix: string }> = [
  { scope: 'workflow.outputs', prefix: 'workflow.outputs' },
  { scope: 'chatflow.outputs', prefix: 'chatflow.outputs' },
  { scope: 'component', prefix: 'component' },
  { scope: 'event', prefix: 'event' },
  { scope: 'page', prefix: 'page' },
  { scope: 'app', prefix: 'app' },
  { scope: 'system', prefix: 'system' }
];

const PATH_RE = /\b(workflow\.outputs|chatflow\.outputs|component|event|page|app|system)(?:\.[a-zA-Z_][a-zA-Z0-9_-]*)+/g;

export interface DependencyPath {
  scope: ScopeRoot;
  /** 完整 dot-path（含作用域前缀）。*/
  path: string;
}

/** 从单个 jsonata 表达式抽取所有依赖路径。*/
export function extractDeps(expression: string): DependencyPath[] {
  const out: DependencyPath[] = [];
  PATH_RE.lastIndex = 0;
  let m: RegExpExecArray | null;
  const seen = new Set<string>();
  while ((m = PATH_RE.exec(expression)) !== null) {
    const path = m[0];
    if (seen.has(path)) continue;
    seen.add(path);
    const scope = SCOPE_ROOT_PATTERNS.find((s) => path.startsWith(s.prefix))?.scope;
    if (scope) out.push({ scope, path });
  }
  return out;
}

/**
 * 反向索引：维护 (path → bindingIds) 的映射，支持增量增删与查询。
 * 不可变操作：每次返回新实例，便于配合 zustand / immer。
 */
export class ReverseDependencyIndex {
  private readonly map = new Map<string, Set<string>>();
  private readonly bindingDeps = new Map<string, Set<string>>();

  upsertBinding(bindingId: string, paths: ReadonlyArray<DependencyPath>): void {
    // 先移除旧依赖
    this.removeBinding(bindingId);
    const newPathSet = new Set<string>();
    for (const dep of paths) {
      newPathSet.add(dep.path);
      let bucket = this.map.get(dep.path);
      if (!bucket) {
        bucket = new Set();
        this.map.set(dep.path, bucket);
      }
      bucket.add(bindingId);
    }
    this.bindingDeps.set(bindingId, newPathSet);
  }

  removeBinding(bindingId: string): void {
    const old = this.bindingDeps.get(bindingId);
    if (!old) return;
    for (const path of old) {
      const bucket = this.map.get(path);
      if (!bucket) continue;
      bucket.delete(bindingId);
      if (bucket.size === 0) this.map.delete(path);
    }
    this.bindingDeps.delete(bindingId);
  }

  /** 返回依赖某 path 的所有 bindingId。注意：path 必须为完整 dot-path（与 extractDeps 输出一致）。*/
  reverseLookup(path: string): string[] {
    const bucket = this.map.get(path);
    if (!bucket) return [];
    return Array.from(bucket);
  }

  /**
   * 路径前缀匹配查找：当 page.foo 变化时，依赖 page.foo / page.foo.bar 的所有 binding 都受影响。
   */
  reverseLookupByPrefix(pathPrefix: string): string[] {
    const out = new Set<string>();
    for (const [path, ids] of this.map.entries()) {
      if (path === pathPrefix || path.startsWith(`${pathPrefix}.`)) {
        for (const id of ids) out.add(id);
      }
    }
    return Array.from(out);
  }

  size(): number {
    return this.bindingDeps.size;
  }
}
