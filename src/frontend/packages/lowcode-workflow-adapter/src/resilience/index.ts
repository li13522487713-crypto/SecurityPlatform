/**
 * Workflow 弹性策略包装（M09 C09-9）。
 *
 * 默认策略（与 docs/lowcode-resilience-spec.md 对齐）：
 *  - timeoutMs: 30_000
 *  - retry: maxAttempts=3 / backoff=exponential / initialDelayMs=500
 *  - circuitBreaker: failuresThreshold=5 / windowMs=60_000 / openMs=30_000
 *  - fallback: 由调用方提供（例如降级到指定 fallbackWorkflowId 或静态值）
 */

import type { ResiliencePolicy } from '@atlas/lowcode-schema';

export const DEFAULT_WORKFLOW_RESILIENCE: ResiliencePolicy = {
  timeoutMs: 30_000,
  retry: { maxAttempts: 3, backoff: 'exponential', initialDelayMs: 500 },
  circuitBreaker: { failuresThreshold: 5, windowMs: 60_000, openMs: 30_000 }
};

export function mergeResiliencePolicy(base: ResiliencePolicy, override?: Partial<ResiliencePolicy>): ResiliencePolicy {
  if (!override) return { ...base };
  return {
    timeoutMs: override.timeoutMs ?? base.timeoutMs,
    retry: override.retry ?? base.retry,
    circuitBreaker: override.circuitBreaker ?? base.circuitBreaker,
    fallback: override.fallback ?? base.fallback
  };
}
