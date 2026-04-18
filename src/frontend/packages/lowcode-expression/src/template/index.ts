/**
 * Jinja-like 模板字符串求值（M02 C02-2 + M18 收尾）。
 *
 * 支持：
 *  - {{ var }} 插值（var 通过 jsonata 求值，可写完整 jsonata 表达式）
 *  - {{ var | filterName(arg1, arg2) | another }} 链式 filter
 *  - {% if expr %}...{% else %}...{% endif %}
 *  - {% for item in items %}...{% endfor %}
 *  - {% break %} / {% continue %} 在最近一层 for 内生效
 *
 * 内置 filter（registerFilter 可扩展）：
 *  - upper / lower / capitalize / trim
 *  - default(value)：值为 null/undefined/'' 时返回 value
 *  - join(sep)：数组按 sep 拼接
 *  - length：字符串/数组长度
 *  - json：JSON.stringify（用于把对象嵌入提示词）
 *  - truncate(n, suffix='…')
 *
 * 与 jsonata 同入口：模板内表达式按 jsonata 语法解析；filter 链由本模块解析后顺序执行。
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
  | { kind: 'endfor' }
  | { kind: 'break' }
  | { kind: 'continue' };

/** 模板 filter：(value, ...args) → value。*/
export type TemplateFilter = (value: unknown, ...args: unknown[]) => unknown;

const FILTERS: Map<string, TemplateFilter> = new Map<string, TemplateFilter>([
  ['upper', (v) => String(v ?? '').toUpperCase()],
  ['lower', (v) => String(v ?? '').toLowerCase()],
  ['capitalize', (v) => {
    const s = String(v ?? '');
    return s.length === 0 ? s : s[0]!.toUpperCase() + s.slice(1);
  }],
  ['trim', (v) => String(v ?? '').trim()],
  ['default', (v, fallback) => (v === null || v === undefined || v === '' ? fallback : v)],
  ['join', (v, sep) => Array.isArray(v) ? v.join(typeof sep === 'string' ? sep : ',') : String(v ?? '')],
  ['length', (v) => Array.isArray(v) ? v.length : (v == null ? 0 : String(v).length)],
  ['json', (v) => JSON.stringify(v)],
  ['truncate', (v, n, suffix) => {
    const s = String(v ?? '');
    const limit = typeof n === 'number' ? n : Number(n) || 0;
    if (limit <= 0 || s.length <= limit) return s;
    return s.slice(0, limit) + (typeof suffix === 'string' ? suffix : '…');
  }]
]);

/** 注册自定义 filter（M18 提示词模板库扩展入口）。*/
export function registerFilter(name: string, fn: TemplateFilter): void {
  FILTERS.set(name, fn);
}

const TOKEN_RE = /\{\{\s*([^}]+?)\s*\}\}|\{%\s*([\s\S]+?)\s*%\}/g;

function tokenize(template: string): Token[] {
  const tokens: Token[] = [];
  let last = 0;
  let m: RegExpExecArray | null;
  // 复位全局 regex 的 lastIndex，避免跨调用残留导致错误偏移
  TOKEN_RE.lastIndex = 0;
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
      } else if (directive === 'break') {
        tokens.push({ kind: 'break' });
      } else if (directive === 'continue') {
        tokens.push({ kind: 'continue' });
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
        const v = await evaluateExpressionWithFilters(t.value, ctx.scope);
        out += v === null || v === undefined ? '' : String(v);
        i += 1;
        break;
      }
      case 'if': {
        const cond = await jsonataEvaluate(t.cond, ctx.scope);
        i += 1;
        // 把 break/continue 也加到 stopAt：让 if 体内的 break/continue 能透传给外层 for
        const innerStop = new Set<Token['kind']>(['else', 'endif', 'break', 'continue']);
        const branchTrue = await renderTokens(tokens, i, ctx, innerStop);
        i = branchTrue.nextIndex;
        let elseRendered = '';
        let elseStopReason: Token['kind'] | undefined;
        // true 分支提前退出（break/continue）：跳过 else/endif 直到 endif；
        // 若 cond 为 true 才把 stopReason 上抛给外层 for，否则忽略（false 分支不该执行此 break）
        if (branchTrue.stopReason === 'break' || branchTrue.stopReason === 'continue') {
          while (i < tokens.length && tokens[i].kind !== 'endif') i += 1;
          if (tokens[i]?.kind !== 'endif') throw new TemplateSyntaxError('if 缺少 endif');
          i += 1;
          out += cond ? branchTrue.rendered : '';
          if (cond) return { rendered: out, nextIndex: i, stopReason: branchTrue.stopReason };
          break;
        }
        if (branchTrue.stopReason === 'else') {
          i += 1;
          const branchFalse = await renderTokens(tokens, i, ctx, innerStop);
          elseRendered = branchFalse.rendered;
          i = branchFalse.nextIndex;
          if (branchFalse.stopReason === 'break' || branchFalse.stopReason === 'continue') {
            elseStopReason = branchFalse.stopReason;
            while (i < tokens.length && tokens[i].kind !== 'endif') i += 1;
          }
        }
        if (tokens[i]?.kind !== 'endif') throw new TemplateSyntaxError('if 缺少 endif');
        i += 1;
        out += cond ? branchTrue.rendered : elseRendered;
        // 仅当 cond 为 false 且 else 分支触发了 break/continue 才上抛
        if (!cond && elseStopReason !== undefined) {
          return { rendered: out, nextIndex: i, stopReason: elseStopReason };
        }
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
        outer: for (const item of items) {
          const childScope: JsonValue = mergeScope(ctx.scope, t.itemVar, item as JsonValue);
          const inner = await renderTokens(tokens.slice(bodyStart, bodyEnd), 0, { scope: childScope }, new Set(['break', 'continue']));
          out += inner.rendered;
          if (inner.stopReason === 'break') break outer;
          if (inner.stopReason === 'continue') continue;
        }
        break;
      }
      case 'else':
      case 'endif':
      case 'endfor':
      case 'break':
      case 'continue':
        return { rendered: out, nextIndex: i, stopReason: t.kind };
    }
  }
  return { rendered: out, nextIndex: i };
}

/**
 * 解析 `expr | filterName(arg1, arg2) | other` 形式的表达式。
 * jsonata 本身不识别 `|` 作为 pipe，因此先按非引号 `|` 切分，再依序应用 filter。
 */
async function evaluateExpressionWithFilters(expression: string, scope: JsonValue): Promise<unknown> {
  const parts = splitOnPipes(expression);
  let value: unknown = await jsonataEvaluate(parts[0], scope);
  for (let i = 1; i < parts.length; i++) {
    const { name, args } = parseFilterCall(parts[i]);
    const fn = FILTERS.get(name);
    if (!fn) throw new TemplateSyntaxError(`未知模板 filter：${name}`);
    const resolvedArgs: unknown[] = [];
    for (const a of args) resolvedArgs.push(await resolveFilterArg(a, scope));
    value = fn(value, ...resolvedArgs);
  }
  return value;
}

function splitOnPipes(expr: string): string[] {
  const out: string[] = [];
  let buf = '';
  let inS = false;
  let inD = false;
  let parenDepth = 0;
  let bracketDepth = 0;
  for (const ch of expr) {
    if (ch === "'" && !inD) inS = !inS;
    else if (ch === '"' && !inS) inD = !inD;
    else if (!inS && !inD) {
      if (ch === '(') parenDepth++;
      else if (ch === ')') parenDepth--;
      else if (ch === '[') bracketDepth++;
      else if (ch === ']') bracketDepth--;
      else if (ch === '|' && parenDepth === 0 && bracketDepth === 0) {
        out.push(buf.trim());
        buf = '';
        continue;
      }
    }
    buf += ch;
  }
  if (buf.trim().length > 0) out.push(buf.trim());
  return out;
}

function parseFilterCall(part: string): { name: string; args: string[] } {
  const m = /^([A-Za-z_][A-Za-z0-9_]*)\s*(?:\((.*)\))?\s*$/s.exec(part);
  if (!m) throw new TemplateSyntaxError(`非法 filter 写法：${part}`);
  const name = m[1]!;
  const argsRaw = m[2];
  if (!argsRaw || argsRaw.trim().length === 0) return { name, args: [] };
  return { name, args: splitArgs(argsRaw) };
}

function splitArgs(input: string): string[] {
  const out: string[] = [];
  let buf = '';
  let inS = false;
  let inD = false;
  let depth = 0;
  for (const ch of input) {
    if (ch === "'" && !inD) inS = !inS;
    else if (ch === '"' && !inS) inD = !inD;
    else if (!inS && !inD) {
      if (ch === '(' || ch === '[') depth++;
      else if (ch === ')' || ch === ']') depth--;
      else if (ch === ',' && depth === 0) {
        out.push(buf.trim());
        buf = '';
        continue;
      }
    }
    buf += ch;
  }
  if (buf.trim().length > 0) out.push(buf.trim());
  return out;
}

async function resolveFilterArg(raw: string, scope: JsonValue): Promise<unknown> {
  // 字面量：'..' / ".." / 数字 / true/false/null；其它走 jsonata
  if ((raw.startsWith("'") && raw.endsWith("'")) || (raw.startsWith('"') && raw.endsWith('"'))) {
    return raw.slice(1, -1);
  }
  if (/^-?\d+(\.\d+)?$/.test(raw)) return Number(raw);
  if (raw === 'true') return true;
  if (raw === 'false') return false;
  if (raw === 'null') return null;
  return await jsonataEvaluate(raw, scope);
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
