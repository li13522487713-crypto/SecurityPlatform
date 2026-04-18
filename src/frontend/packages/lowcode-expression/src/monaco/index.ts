/**
 * Monaco LSP 适配器（M02 C02-7）。
 *
 * 设计要点：
 * - 不强制依赖 monaco-editor（M05 内嵌时再 attach）；本模块仅暴露纯函数与协议结构，便于不同宿主复用。
 * - 提供四个能力：
 *    1. computeDiagnostics：把 lint 结果映射为 Monaco IMarkerData 兼容结构。
 *    2. computeCompletionItems：把候选项映射为 Monaco 兼容 CompletionItem。
 *    3. computeHover：基于变量 schema 提供悬浮提示。
 *    4. computeWriteWarnings：识别表达式中疑似写入只读作用域的字面量（如 set_variable.targetPath 内嵌于表达式）。
 *
 * 实际 Monaco 实例 attach 由 M05 lowcode-property-forms/src/monaco 完成。
 */

import { lintExpression, type CompletionCandidate, type ExpressionError, type ExpressionIndex, buildCompletionList } from '../inference';
import { extractDeps } from '../deps';
import { isReadonlyScope } from '../scope';

/** 与 Monaco IMarkerData 兼容的子集。*/
export interface MonacoMarker {
  startLineNumber: number;
  startColumn: number;
  endLineNumber: number;
  endColumn: number;
  message: string;
  severity: 'error' | 'warning' | 'info' | 'hint';
}

export function computeDiagnostics(expression: string): MonacoMarker[] {
  const err: ExpressionError | null = lintExpression(expression);
  if (!err) return [];
  const line = err.line ?? 1;
  const col = err.column ?? 1;
  return [
    {
      startLineNumber: line,
      startColumn: col,
      endLineNumber: line,
      endColumn: col + Math.max(1, err.token?.length ?? 1),
      message: err.message,
      severity: 'error'
    }
  ];
}

export interface MonacoCompletionItem {
  label: string;
  insertText: string;
  detail?: string;
  documentation?: string;
}

export function computeCompletionItems(index: ExpressionIndex): MonacoCompletionItem[] {
  const list = buildCompletionList(index);
  return list.map((c: CompletionCandidate) => ({
    label: c.label,
    insertText: c.insertText,
    detail: `${c.scope} : ${c.valueType}`,
    documentation: c.description
  }));
}

export interface MonacoHover {
  contents: string[];
}

export function computeHover(path: string, index: ExpressionIndex): MonacoHover | null {
  // 在 4 类作用域索引中查找 path。
  for (const v of index.variables) {
    const full = `${v.scope}.${v.code}`;
    if (path === full || path.startsWith(`${full}.`)) {
      return { contents: [`**${full}**`, `valueType: \`${v.valueType}\``, v.description ?? ''] };
    }
  }
  for (const wf of index.workflows) {
    if (path.startsWith(`workflow.outputs.${wf.id}`)) {
      return { contents: [`**workflow ${wf.id} 输出**`] };
    }
  }
  for (const cf of index.chatflows) {
    if (path.startsWith(`chatflow.outputs.${cf.id}`)) {
      return { contents: [`**chatflow ${cf.id} 输出**`] };
    }
  }
  for (const c of index.components) {
    if (path === `component.${c.id}.value` || path.startsWith(`component.${c.id}.value.`)) {
      return { contents: [`**component ${c.id}**`, `valueType: \`${c.valueType}\``] };
    }
  }
  return null;
}

/**
 * 表达式中若引用只读作用域且嵌入"set"操作符（jsonata 中无 set，但用户可能在表达式编辑器误写赋值伪语法），
 * 这里返回警告告知用户跨作用域写入受限。
 *
 * 实际写入校验由 M03 action-runtime scope-guard 做强约束抛错；此处仅做编辑期红线提示。
 */
export function computeWriteWarnings(expression: string): MonacoMarker[] {
  const deps = extractDeps(expression);
  const warnings: MonacoMarker[] = [];
  // 当前 jsonata 不支持赋值，因此"读取只读作用域"本身合法；
  // 真正需要在 set_variable 动作的 targetPath 字段做编辑期校验，那由 property-forms 调用 ensureWritablePath。
  // 本函数仅为 future-proof：当出现疑似写法符号（如 :=）且依赖只读作用域时给出 hint。
  if (/:=/.test(expression)) {
    for (const d of deps) {
      if (isReadonlyScope(d.scope)) {
        warnings.push({
          startLineNumber: 1,
          startColumn: 1,
          endLineNumber: 1,
          endColumn: expression.length + 1,
          message: `检测到疑似写入语法 ":="，但表达式引用了只读作用域 ${d.scope}（路径：${d.path}）。请改用 set_variable 动作并确保 targetPath 为 page.* 或 app.*。`,
          severity: 'warning'
        });
      }
    }
  }
  return warnings;
}
