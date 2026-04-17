/**
 * Jinja-like 模板字符串求值（M02 C02-2）。
 *
 * 支持：
 *  - {{ var }} 插值（var 通过 jsonata 求值，可写完整 jsonata 表达式）
 *  - {% if expr %}...{% else %}...{% endif %}
 *  - {% for item in items %}...{% endfor %}
 *
 * 局限（M02 阶段已知，按需求精简）：
 *  - 不支持嵌套循环外层 break/continue（与 docx §A04-A05 提示词模板要求对齐即可）
 *  - 不支持自定义 filter（需要时在 M18 提示词模板库扩展）
 *
 * 与 jsonata 同入口：模板内表达式按 jsonata 语法解析。
 */

import type { JsonValue } from '@atlas/lowcode-schema';
import { evaluate as jsonataEvaluate } from '../jsonata';

interface RenderContext {
  scope: JsonValue;
}

/** 解析单层 token。*/
type Token =
  | { kind: 'text'; value: string }
  | { kind: 'expr'; value: string }
  | { kind: 'if'; cond: string }
  | { kind: 'else' }
  | { kind: 'endif' }
  | { kind: 'for'; itemVar: string; iterableExpr: string }
  | { kind: 'endfor' };

const TOKEN_RE = /\{\{\s*([^}]+?)\s*\}\}|\{%\s*([\s\S]+?)\s*%\}/g;

function tokenize(template: string): Token[] {
  const tokens: Token[] = [];
  let last = 0;
  let m: RegExpExecArray | null;
  while ((m = TOKEN_RE.exec(template)) !== null) {
    if (m.index > last) tokens.push({ kind: 'text', value: template.slice(last, m.index) });
    if (m[1] !== undefined) {
      tokens.push({ kind: 'expr', value: m[1] });
    } else if (m[2] !== undefined) {
      const directive = m[2].trim();
      if (directive.startsWith('if ')) {
        tokens.push({ kind: 'if', cond: directive.slice(3).trim() });
      } else if (directive === 'else') {
        tokens.push({ kind: 'else' });
      } else if (directive === 'endif') {
        tokens.push({ kind: 'endif' });
      } else if (directive.startsWith('for ')) {
        const body = directive.slice(4).trim();
        const inIdx = body.indexOf(' in ');
        if (inIdx === -1) throw new TemplateSyntaxError(`for 指令缺少 in：${directive}`);
        const itemVar = body.slice(0, inIdx).trim();
        const iterableExpr = body.slice(inIdx + 4).trim();
        tokens.push({ kind: 'for', itemVar, iterableExpr });
      } else if (directive === 'endfor') {
        tokens.push({ kind: 'endfor' });
      } else {
        throw new TemplateSyntaxError(`未知模板指令：${directive}`);
      }
    }
    last = TOKEN_RE.lastIndex;
  }
  if (last < template.length) tokens.push({ kind: 'text', value: template.slice(last) });
  return tokens;
}

export class TemplateSyntaxError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'TemplateSyntaxError';
  }
}

async function renderTokens(tokens: Token[], i: number, ctx: RenderContext, stopAt?: Set<Token['kind']>): Promise<{ rendered: string; nextIndex: number; stopReason?: Token['kind'] }> {
  let out = '';
  while (i < tokens.length) {
    const t = tokens[i];
    if (stopAt?.has(t.kind)) {
      return { rendered: out, nextIndex: i, stopReason: t.kind };
    }
    switch (t.kind) {
      case 'text': {
        out += t.value;
        i += 1;
        break;
      }
      case 'expr': {
        const v = await jsonataEvaluate(t.value, ctx.scope);
        out += v === null || v === undefined ? '' : String(v);
        i += 1;
        break;
      }
      case 'if': {
        const cond = await jsonataEvaluate(t.cond, ctx.scope);
        i += 1;
        const branchTrue = await renderTokens(tokens, i, ctx, new Set(['else', 'endif']));
        i = branchTrue.nextIndex;
        let elseRendered = '';
        if (branchTrue.stopReason === 'else') {
          i += 1;
          const branchFalse = await renderTokens(tokens, i, ctx, new Set(['endif']));
          elseRendered = branchFalse.rendered;
          i = branchFalse.nextIndex;
        }
        if (tokens[i]?.kind !== 'endif') throw new TemplateSyntaxError('if 缺少 endif');
        i += 1;
        out += cond ? branchTrue.rendered : elseRendered;
        break;
      }
      case 'for': {
        const iter = await jsonataEvaluate(t.iterableExpr, ctx.scope);
        i += 1;
        // 找到 endfor 边界，先把 body tokens 抽出来。
        const bodyStart = i;
        let depth = 1;
        while (i < tokens.length && depth > 0) {
          if (tokens[i].kind === 'for') depth += 1;
          else if (tokens[i].kind === 'endfor') depth -= 1;
          if (depth > 0) i += 1;
        }
        const bodyEnd = i;
        if (tokens[i]?.kind !== 'endfor') throw new TemplateSyntaxError('for 缺少 endfor');
        i += 1; // 跳过 endfor
        const items = Array.isArray(iter) ? iter : iter == null ? [] : [iter];
        for (const item of items) {
          const childScope: JsonValue = mergeScope(ctx.scope, t.itemVar, item as JsonValue);
          const inner = await renderTokens(tokens.slice(bodyStart, bodyEnd), 0, { scope: childScope });
          out += inner.rendered;
        }
        break;
      }
      case 'else':
      case 'endif':
      case 'endfor':
        return { rendered: out, nextIndex: i, stopReason: t.kind };
    }
  }
  return { rendered: out, nextIndex: i };
}

function mergeScope(scope: JsonValue, key: string, value: JsonValue): JsonValue {
  if (scope === null || typeof scope !== 'object' || Array.isArray(scope)) {
    return { [key]: value } as JsonValue;
  }
  return { ...(scope as Record<string, JsonValue>), [key]: value };
}

/** 渲染 Jinja-like 模板。*/
export async function renderTemplate(template: string, scope: JsonValue): Promise<string> {
  const tokens = tokenize(template);
  const r = await renderTokens(tokens, 0, { scope });
  return r.rendered;
}
