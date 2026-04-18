import { describe, it, expect } from 'vitest';
import { buildAgenticInvokeMetadata, shouldHideIntermediateNodes, switchMode } from '../orchestration';
import { DEFAULT_WORKFLOW_RESILIENCE, mergeResiliencePolicy } from '../resilience';

describe('orchestration', () => {
  it('switchMode 双向切换', () => {
    const a = switchMode({ workflowId: 'w', mode: 'explicit' }, 'agentic');
    expect(a.mode).toBe('agentic');
    expect(a.agenticTools).toEqual([]);
    const b = switchMode({ ...a, agenticTools: [{ name: 't', description: 'd' }] }, 'explicit');
    expect(b.mode).toBe('explicit');
    expect((b as { agenticTools?: unknown }).agenticTools).toBeUndefined();
  });

  it('shouldHideIntermediateNodes', () => {
    expect(shouldHideIntermediateNodes('agentic')).toBe(true);
    expect(shouldHideIntermediateNodes('explicit')).toBe(false);
  });

  it('buildAgenticInvokeMetadata 输出 tool 列表', () => {
    const md = buildAgenticInvokeMetadata({ workflowId: 'w', mode: 'agentic', agenticTools: [{ name: 'search', description: 'web search' }] });
    expect(md.orchestration).toBe('agentic');
    expect(Array.isArray(md.tools)).toBe(true);
  });

  it('explicit 模式不附带 metadata', () => {
    expect(buildAgenticInvokeMetadata({ workflowId: 'w', mode: 'explicit' })).toEqual({});
  });
});

describe('resilience policy', () => {
  it('默认 30s/3 重试/熔断 5/60s/30s', () => {
    expect(DEFAULT_WORKFLOW_RESILIENCE.timeoutMs).toBe(30_000);
    expect(DEFAULT_WORKFLOW_RESILIENCE.retry?.maxAttempts).toBe(3);
    expect(DEFAULT_WORKFLOW_RESILIENCE.circuitBreaker?.failuresThreshold).toBe(5);
  });

  it('mergeResiliencePolicy 仅覆盖指定字段', () => {
    const p = mergeResiliencePolicy(DEFAULT_WORKFLOW_RESILIENCE, { timeoutMs: 5_000 });
    expect(p.timeoutMs).toBe(5_000);
    expect(p.retry?.maxAttempts).toBe(3);
  });
});
