import { describe, expect, it } from 'vitest';

import { stableStringifyWorkflowSchema } from './stable-json';

describe('stableStringifyWorkflowSchema', () => {
  it('sorts object keys recursively', () => {
    const left = stableStringifyWorkflowSchema({
      b: 1,
      a: { d: 4, c: 3 },
    });
    const right = stableStringifyWorkflowSchema({
      a: { c: 3, d: 4 },
      b: 1,
    });

    expect(left).toBe(right);
  });

  it('strips undefined values and rejects circular references', () => {
    expect(stableStringifyWorkflowSchema({ a: 1, b: undefined })).toBe(
      '{"a":1}',
    );

    const circular: { self?: unknown } = {};
    circular.self = circular;

    expect(() => stableStringifyWorkflowSchema(circular)).toThrow(
      /circular workflow schema/,
    );
  });

  it('keeps large id-like values as strings', () => {
    expect(
      stableStringifyWorkflowSchema({
        workflow_id: '9223372036854775807',
      }),
    ).toBe('{"workflow_id":"9223372036854775807"}');
  });
});
