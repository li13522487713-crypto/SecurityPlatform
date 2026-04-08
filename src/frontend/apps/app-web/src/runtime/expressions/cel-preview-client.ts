/**
 * CEL 表达式前端客户端。
 *
 * 所有表达式校验与求值统一走后端 CelExpressionEngine，
 * 符合 docs/platform-unified-schema-and-expression.md 封板规则：
 * "禁止绕过 CEL 的独立表达式执行器"。
 */

import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";
import type {
  ExpressionValidateRequest,
  ExpressionValidateResponse,
  ExpressionEvaluateRequest,
  ExpressionEvaluateResponse,
  RuntimeExpressionContext,
} from "./expression-types";

const EXPRESSIONS_BASE = "/expressions";

export async function validateExpression(
  expression: string,
): Promise<ExpressionValidateResponse> {
  const body: ExpressionValidateRequest = { expression };
  const resp = await requestApi<ApiResponse<ExpressionValidateResponse>>(
    `${EXPRESSIONS_BASE}/validate`,
    {
      method: "POST",
      body: JSON.stringify(body),
    },
  );

  return (
    resp.data ?? {
      isValid: false,
      errors: [resp.message ?? "Unknown error"],
      warnings: [],
      variables: [],
    }
  );
}

export async function evaluateExpression(
  expression: string,
  variables: RuntimeExpressionContext,
): Promise<ExpressionEvaluateResponse> {
  const body: ExpressionEvaluateRequest = {
    expression,
    ...variables,
  };

  const resp = await requestApi<ApiResponse<ExpressionEvaluateResponse>>(
    `${EXPRESSIONS_BASE}/evaluate`,
    {
      method: "POST",
      body: JSON.stringify(body),
    },
  );

  return (
    resp.data ?? {
      success: false,
      error: resp.message ?? "Unknown error",
    }
  );
}

/**
 * Mustache 变量占位符替换（纯本地，不执行表达式）。
 *
 * 仅做 {{ varName }} -> 变量值 的简单替换，
 * 不调用 new Function / eval，符合安全封板要求。
 */
export function resolveVariablePlaceholders(
  text: string,
  variables: Record<string, unknown>,
): string {
  if (typeof text !== "string") return text;
  return text.replace(/\{\{\s*(.+?)\s*\}\}/g, (_match, path: string) => {
    const value = resolveDotPath(variables, path.trim());
    return value !== undefined && value !== null ? String(value) : "";
  });
}

function resolveDotPath(obj: Record<string, unknown>, path: string): unknown {
  const segments = path.split(".");
  let current: unknown = obj;
  for (const seg of segments) {
    if (current === null || current === undefined) return undefined;
    if (typeof current !== "object") return undefined;
    current = (current as Record<string, unknown>)[seg];
  }
  return current;
}
