import { describe, it, expect, beforeEach } from 'vitest';
import { withResilience, CircuitOpenError, TimeoutError, __resetCircuitsForTesting } from '../resilience';

beforeEach(() => __resetCircuitsForTesting());

describe('resilience', () => {
  it('无策略直接执行', async () => {
    const r = await withResilience(async () => 1);
    expect(r).toBe(1);
  });

  it('超时抛 TimeoutError', async () => {
    await expect(
      withResilience(() => new Promise((resolve) => setTimeout(() => resolve(1), 200)) as Promise<number>, {
        policy: { timeoutMs: 50 }
      } as never)
    ).rejects.toBeInstanceOf(TimeoutError);
  });

  it('重试至成功', async () => {
    let attempts = 0;
    const r = await withResilience(
      async () => {
        attempts++;
        if (attempts < 3) throw new Error('boom');
        return 'ok';
      },
      { policy: { retry: { maxAttempts: 5, backoff: 'fixed', initialDelayMs: 1 } } } as never
    );
    expect(r).toBe('ok');
    expect(attempts).toBe(3);
  });

  it('熔断打开后拒绝', async () => {
    const policy = {
      retry: { maxAttempts: 1, backoff: 'fixed', initialDelayMs: 1 },
      circuitBreaker: { failuresThreshold: 2, windowMs: 10_000, openMs: 10_000 }
    } as const;
    // 触发 2 次失败 → 熔断打开
    for (let i = 0; i < 2; i++) {
      await expect(
        withResilience(async () => { throw new Error('boom'); }, { policy: policy as never, circuitKey: 'k1' })
      ).rejects.toThrow();
    }
    // 此时熔断打开
    await expect(
      withResilience(async () => 1, { policy: policy as never, circuitKey: 'k1' })
    ).rejects.toBeInstanceOf(CircuitOpenError);
  });

  it('降级 staticValue 在熔断时返回', async () => {
    const policy = {
      retry: { maxAttempts: 1, backoff: 'fixed', initialDelayMs: 1 },
      circuitBreaker: { failuresThreshold: 1, windowMs: 10_000, openMs: 10_000 },
      fallback: { kind: 'static' as const, staticValue: 'fallback' }
    } as const;
    const r = await withResilience(async () => { throw new Error('boom'); }, { policy: policy as never, circuitKey: 'k2' });
    // 第一次 attempt 失败 → maxAttempts 用尽 → fallback.staticValue 'fallback' 返回
    expect(r).toBe('fallback');
  });

  it('exponential backoff 增长延迟', async () => {
    let attempts = 0;
    const start = Date.now();
    await expect(
      withResilience(async () => {
        attempts++;
        throw new Error('x');
      }, { policy: { retry: { maxAttempts: 3, backoff: 'exponential', initialDelayMs: 10 } } } as never)
    ).rejects.toThrow();
    const elapsed = Date.now() - start;
    expect(attempts).toBe(3);
    // 至少: 10 + 20 = 30ms
    expect(elapsed).toBeGreaterThanOrEqual(25);
  });
});
