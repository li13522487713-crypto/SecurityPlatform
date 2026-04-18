/**
 * 弹性策略包装器（M03 C03-6）。
 *
 * 抽象层：超时 / 重试 / 退避 / 熔断 / 降级。
 * 真正的策略实施由各 Adapter 在 M09 / M11 / M19 完整落地（含服务端 Polly），本模块仅提供编排底座。
 *
 * 详见 docs/lowcode-resilience-spec.md。
 */

import type { JsonValue, ResiliencePolicy } from '@atlas/lowcode-schema';

export interface ResilienceContext {
  /** 用于 trace 标识，便于在 M13 调试台关联。*/
  actionId?: string;
  /** 失败时回报上层（不阻断）。*/
  onAttemptFailure?: (info: { attempt: number; error: Error }) => void;
}

/** 简易熔断器单例存储（按 key 隔离，key 由调用方传入，如 workflowId）。*/
const CIRCUITS = new Map<string, { failures: number; openedAt?: number }>();

function getOrCreateCircuit(key: string) {
  let c = CIRCUITS.get(key);
  if (!c) {
    c = { failures: 0 };
    CIRCUITS.set(key, c);
  }
  return c;
}

export class CircuitOpenError extends Error {
  constructor(key: string) {
    super(`circuit "${key}" is open, request rejected`);
    this.name = 'CircuitOpenError';
  }
}

export class TimeoutError extends Error {
  constructor(timeoutMs: number) {
    super(`operation timeout after ${timeoutMs}ms`);
    this.name = 'TimeoutError';
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** 包装一个异步操作，按 ResiliencePolicy 应用超时 / 重试 / 熔断 / 降级（fallback 由调用方提供）。*/
export async function withResilience<T extends JsonValue | undefined>(
  operation: () => Promise<T>,
  options: {
    policy?: ResiliencePolicy;
    /** 熔断 key（如 workflow:wf1）。无 policy.circuitBreaker 时忽略。*/
    circuitKey?: string;
    /** 降级回调（policy.fallback.kind=workflow 时由调用方实现）。*/
    fallback?: () => Promise<T>;
    context?: ResilienceContext;
  } = {}
): Promise<T> {
  const { policy, circuitKey, fallback, context } = options;
  if (!policy) {
    return operation();
  }

  // 熔断状态判断
  if (policy.circuitBreaker && circuitKey) {
    const c = getOrCreateCircuit(circuitKey);
    if (c.openedAt !== undefined) {
      const elapsed = Date.now() - c.openedAt;
      if (elapsed < policy.circuitBreaker.openMs) {
        // 半开期内拒绝；如有 fallback 则降级
        if (fallback) return fallback();
        if (policy.fallback?.kind === 'static' && policy.fallback.staticValue !== undefined) {
          return policy.fallback.staticValue as T;
        }
        throw new CircuitOpenError(circuitKey);
      }
      // 进入半开：清空失败计数，允许一次试探
      c.failures = 0;
      c.openedAt = undefined;
    }
  }

  const maxAttempts = policy.retry?.maxAttempts ?? 1;
  const backoff = policy.retry?.backoff ?? 'fixed';
  const initialDelay = policy.retry?.initialDelayMs ?? 200;
  const timeoutMs = policy.timeoutMs;

  let lastErr: Error | undefined;
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const exec = timeoutMs ? withTimeout(operation(), timeoutMs) : operation();
      const result = await exec;

      // 成功 → 清零熔断
      if (policy.circuitBreaker && circuitKey) {
        const c = getOrCreateCircuit(circuitKey);
        c.failures = 0;
        c.openedAt = undefined;
      }
      return result;
    } catch (err) {
      lastErr = err instanceof Error ? err : new Error(String(err));
      context?.onAttemptFailure?.({ attempt, error: lastErr });
      // 记录熔断失败计数
      if (policy.circuitBreaker && circuitKey) {
        const c = getOrCreateCircuit(circuitKey);
        c.failures += 1;
        if (c.failures >= policy.circuitBreaker.failuresThreshold) {
          c.openedAt = Date.now();
        }
      }
      if (attempt < maxAttempts) {
        const wait = backoff === 'exponential' ? initialDelay * 2 ** (attempt - 1) : initialDelay;
        await delay(wait);
      }
    }
  }

  // 所有重试均失败 → 降级
  if (fallback) return fallback();
  if (policy.fallback?.kind === 'static' && policy.fallback.staticValue !== undefined) {
    return policy.fallback.staticValue as T;
  }
  throw lastErr ?? new Error('withResilience: unknown failure');
}

async function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  try {
    return await Promise.race<T>([
      promise,
      new Promise<T>((_, reject) => {
        timer = setTimeout(() => reject(new TimeoutError(timeoutMs)), timeoutMs);
      })
    ]);
  } finally {
    if (timer) clearTimeout(timer);
  }
}

/** 仅供测试：清空熔断器。*/
export function __resetCircuitsForTesting(): void {
  CIRCUITS.clear();
}
