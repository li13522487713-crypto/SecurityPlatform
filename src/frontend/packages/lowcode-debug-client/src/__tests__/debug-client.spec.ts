import { describe, it, expect } from 'vitest';
import { buildQueryString, buildSpanTree, summarizePhases, type TraceSpanDto } from '..';

describe('buildQueryString', () => {
  it('忽略空值 + URL 编码', () => {
    const s = buildQueryString({ traceId: 't1', errorType: 'workflow_error', empty: '', n: undefined, page: 'home', special: 'a&b' });
    expect(s).toContain('traceId=t1');
    expect(s).toContain('errorType=workflow_error');
    expect(s).not.toContain('empty=');
    expect(s).not.toContain('n=');
    expect(s).toContain('special=a%26b');
  });
  it('全空返回空串', () => {
    expect(buildQueryString({ a: '', b: undefined })).toBe('');
  });
});

describe('buildSpanTree', () => {
  const spans: TraceSpanDto[] = [
    { spanId: 'a', name: 'dispatcher.start', status: 'ok', startedAt: '2026-04-17T00:00:00Z' },
    { spanId: 'b', parentSpanId: 'a', name: 'action.set_variable', status: 'ok', startedAt: '2026-04-17T00:00:01Z' },
    { spanId: 'c', parentSpanId: 'a', name: 'action.call_workflow', status: 'ok', startedAt: '2026-04-17T00:00:02Z' },
    { spanId: 'd', parentSpanId: 'c', name: 'workflow.invoke', status: 'ok', startedAt: '2026-04-17T00:00:03Z' }
  ];

  it('父子关系正确 + 按时间排序', () => {
    const tree = buildSpanTree(spans);
    expect(tree.length).toBe(1);
    expect(tree[0].span.spanId).toBe('a');
    expect(tree[0].children.map((c) => c.span.spanId)).toEqual(['b', 'c']);
    expect(tree[0].children[1].children[0].span.spanId).toBe('d');
  });

  it('孤立 span（找不到父）作为根节点', () => {
    const isolated = [...spans, { spanId: 'x', parentSpanId: 'missing', name: 'orphan', status: 'ok' as const, startedAt: '2026-04-17T01:00:00Z' }];
    const tree = buildSpanTree(isolated);
    expect(tree.find((t) => t.span.spanId === 'x')).toBeDefined();
  });
});

describe('summarizePhases', () => {
  it('按 name 前缀聚合 + 计算 avgMs/maxMs', () => {
    const spans: TraceSpanDto[] = [
      { spanId: '1', name: 'workflow.invoke', status: 'ok', startedAt: '2026-04-17T00:00:00Z', endedAt: '2026-04-17T00:00:01Z' },
      { spanId: '2', name: 'workflow.invoke', status: 'ok', startedAt: '2026-04-17T00:00:01Z', endedAt: '2026-04-17T00:00:04Z' },
      { spanId: '3', name: 'action.set_variable', status: 'ok', startedAt: '2026-04-17T00:00:00Z', endedAt: '2026-04-17T00:00:00.500Z' }
    ];
    const stats = summarizePhases(spans);
    const wf = stats.find((s) => s.phase === 'workflow')!;
    expect(wf.count).toBe(2);
    expect(wf.maxMs).toBe(3000);
    expect(wf.totalMs).toBe(4000);
    expect(stats.find((s) => s.phase === 'event')?.count).toBe(1);
  });
});
