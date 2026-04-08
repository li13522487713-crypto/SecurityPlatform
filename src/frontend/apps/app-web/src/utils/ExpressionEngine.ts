/**
 * @deprecated 此模块已废弃。新代码请使用 runtime/expressions/cel-preview-client.ts
 * 所有表达式统一走后端 CelExpressionEngine（POST /api/v1/expressions/evaluate）。
 * 参见 docs/platform-unified-schema-and-expression.md 封板规则：
 * "禁止绕过 CEL 的独立表达式执行器"。
 *
 * 保留仅为历史兼容，将在 Phase 2 完全移除。
 *
 * 原说明：安全的 JavaScript 表达式沙盒执行引擎，
 * 拦截对全局对象（如 window、document）的访问，防止恶意代码执行。
 */

const ALLOWED_GLOBALS = new Set([
  "Math", "Date", "String", "Number", "Boolean", "Array", "Object", "JSON",
  "parseInt", "parseFloat", "isNaN", "isFinite", "decodeURI", "encodeURI",
  "undefined", "null", "NaN", "Infinity"
]);

export function evaluateExpression(expr: string, context: Record<string, unknown> = {}): unknown {
  if (!expr || typeof expr !== "string") return expr;

  const sandboxContext = new Proxy(context, {
    has(target, key) {
      if (typeof key === "symbol") return Reflect.has(target, key);
      return true;
    },
    get(target, key, receiver) {
      if (key === Symbol.unscopables) return undefined;

      if (Reflect.has(target, key)) {
        return Reflect.get(target, key, receiver);
      }

      if (typeof key === "string" && ALLOWED_GLOBALS.has(key)) {
        return (window as unknown as Record<string, unknown>)[key];
      }

      if (key === "window" || key === "document" || key === "globalThis") {
        throw new Error(`[Sandbox] Access denied to global object: ${String(key)}`);
      }

      return undefined;
    }
  });

  try {
    const fn = new Function("sandboxContext", `
      with (sandboxContext) {
        return (${expr});
      }
    `);

    return fn(sandboxContext);
  } catch (err: unknown) {
    console.warn(`[ExpressionEngine] Failed to evaluate expression: ${expr}`, err);
    return undefined;
  }
}

export function parseMustache(text: string, context: Record<string, unknown> = {}): unknown {
  if (typeof text !== "string") return text;

  const regex = /\{\{\s*(.+?)\s*\}\}/g;

  const exactMatch = text.match(/^\{\{\s*(.+?)\s*\}\}$/);
  if (exactMatch) {
    return evaluateExpression(exactMatch[1], context);
  }

  return text.replace(regex, (_match, expr: string) => {
    const val = evaluateExpression(expr, context);
    return val !== undefined && val !== null ? String(val) : "";
  });
}
