/**
 * 前端表达式相关类型，与后端 ExpressionsController 契约对齐。
 */

import type { ValueMap } from "../types/base-types";

export interface RuntimeExpressionContext {
  record?: ValueMap;
  user?: ValueMap;
  page?: ValueMap;
  app?: ValueMap;
  tenant?: ValueMap;
  global?: ValueMap;
  form?: ValueMap;
}

export interface ExpressionValidateRequest {
  expression: string;
}

export interface ExpressionValidateResponse {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  variables: string[];
}

export interface ExpressionEvaluateRequest {
  expression: string;
  record?: ValueMap;
  user?: ValueMap;
  page?: ValueMap;
  app?: ValueMap;
  tenant?: ValueMap;
  global?: ValueMap;
  form?: ValueMap;
}

export interface ExpressionEvaluateResponse {
  success: boolean;
  resultValue?: string | null;
  resultBool?: boolean | null;
  error?: string | null;
}
