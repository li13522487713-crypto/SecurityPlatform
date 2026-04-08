/**
 * 前端表达式相关类型，与后端 ExpressionsController 契约对齐。
 */

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
  record?: Record<string, unknown>;
  user?: Record<string, unknown>;
  page?: Record<string, unknown>;
  app?: Record<string, unknown>;
  tenant?: Record<string, unknown>;
  global?: Record<string, unknown>;
}

export interface ExpressionEvaluateResponse {
  success: boolean;
  resultValue?: string;
  resultBool?: boolean;
  error?: string;
}
