import { requestApi } from './api-core'

export interface ExpressionValidateRequest {
  expression: string
}

export interface ExpressionValidateResponse {
  isValid: boolean
  errors: string[]
  warnings: string[]
  variables: string[]
}

export interface ExpressionEvaluateRequest {
  expression: string
  record?: Record<string, unknown>
  user?: Record<string, unknown>
  page?: Record<string, unknown>
}

export interface ExpressionEvaluateResponse {
  success: boolean
  resultValue: string | null
  resultBool: boolean | null
  error: string | null
}

export const validateExpression = (body: ExpressionValidateRequest) =>
  requestApi<ExpressionValidateResponse>('/api/v1/expressions/validate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const evaluateExpression = (body: ExpressionEvaluateRequest) =>
  requestApi<ExpressionEvaluateResponse>('/api/v1/expressions/evaluate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
