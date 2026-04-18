/**
 * 类型推断与错误位置（M02 C02-5）。
 *
 * 设计要点：
 * - 基于 jsonata 编译错误，提取 row / col 信息。
 * - 基于变量 schema（外部传入），简单推断字面量与变量引用的 valueType（不做完整类型系统）。
 * - 自动补全候选索引：把所有可访问变量按作用域 + 路径排平，输出给 Monaco。
 */

import type { ValueType, ScopeRoot } from '@atlas/lowcode-schema/shared';
import { compile } from '../jsonata';

export interface ExpressionError {
  message: string;
  /** 1-based 行号。*/
  line?: number;
  /** 1-based 列号。*/
  column?: number;
  /** 字符位置（jsonata 原生）。*/
  position?: number;
  /** 关联的子表达式片段。*/
  token?: string;
}

/** 静态语法校验：只 parse 不 evaluate。*/
export function lintExpression(expression: string): ExpressionError | null {
  try {
    compile(expression);
    return null;
  } catch (err) {
    const e = err as { message?: string; position?: number; token?: string };
    const { line, column } = locatePosition(expression, e.position ?? 0);
    return {
      message: e.message ?? String(err),
      line,
      column,
      position: e.position,
      token: e.token
    };
  }
}

/** 基于字符位置反推行列号。*/
export function locatePosition(source: string, position: number): { line: number; column: number } {
  let line = 1;
  let column = 1;
  for (let i = 0; i < Math.min(position, source.length); i++) {
    if (source.charCodeAt(i) === 10 /* \n */) {
      line += 1;
      column = 1;
    } else {
      column += 1;
    }
  }
  return { line, column };
}

/** 自动补全候选项（用于 Monaco）。*/
export interface CompletionCandidate {
  /** 表达式中插入的文本（如 'app.currentUser.name'）。*/
  insertText: string;
  /** 用户可见的标签。*/
  label: string;
  /** 所属作用域。*/
  scope: ScopeRoot;
  /** 推断 valueType（未知则 'any'）。*/
  valueType: ValueType;
  /** 描述。*/
  description?: string;
}

/** 表达式作用域索引（外部维护：变量 / 工作流输出 / 组件等）。*/
export interface ExpressionIndex {
  variables: ReadonlyArray<{ scope: Extract<ScopeRoot, 'page' | 'app' | 'system'>; code: string; valueType: ValueType; description?: string }>;
  workflows: ReadonlyArray<{ id: string; outputPaths: ReadonlyArray<{ path: string; valueType: ValueType }> }>;
  chatflows: ReadonlyArray<{ id: string; outputPaths: ReadonlyArray<{ path: string; valueType: ValueType }> }>;
  components: ReadonlyArray<{ id: string; valueType: ValueType }>;
}

/** 全量补全候选列表。Monaco LSP 适配器按 prefix 过滤。*/
export function buildCompletionList(index: ExpressionIndex): CompletionCandidate[] {
  const out: CompletionCandidate[] = [];
  for (const v of index.variables) {
    out.push({
      insertText: `${v.scope}.${v.code}`,
      label: `${v.scope}.${v.code}`,
      scope: v.scope,
      valueType: v.valueType,
      description: v.description
    });
  }
  for (const wf of index.workflows) {
    for (const o of wf.outputPaths) {
      out.push({
        insertText: `workflow.outputs.${wf.id}.${o.path}`,
        label: `workflow.outputs.${wf.id}.${o.path}`,
        scope: 'workflow.outputs',
        valueType: o.valueType
      });
    }
  }
  for (const cf of index.chatflows) {
    for (const o of cf.outputPaths) {
      out.push({
        insertText: `chatflow.outputs.${cf.id}.${o.path}`,
        label: `chatflow.outputs.${cf.id}.${o.path}`,
        scope: 'chatflow.outputs',
        valueType: o.valueType
      });
    }
  }
  for (const c of index.components) {
    out.push({
      insertText: `component.${c.id}.value`,
      label: `component.${c.id}.value`,
      scope: 'component',
      valueType: c.valueType
    });
  }
  return out;
}

/** 简单字面量推断（与服务端 IServerSideExpressionEvaluator 共享语义）。*/
export function inferLiteralType(literal: unknown): ValueType {
  if (typeof literal === 'string') return 'string';
  if (typeof literal === 'number') return 'number';
  if (typeof literal === 'boolean') return 'boolean';
  if (literal === null) return 'any';
  if (Array.isArray(literal)) return 'array';
  if (literal instanceof Date) return 'date';
  if (typeof literal === 'object') return 'object';
  return 'any';
}
