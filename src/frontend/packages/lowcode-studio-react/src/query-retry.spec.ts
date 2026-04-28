import { describe, expect, it } from 'vitest';
import { shouldRetryLowcodeQuery } from './query-retry';
import { LowcodeApiError } from './services/api-core';

describe('shouldRetryLowcodeQuery', () => {
  it('对 4xx 错误不重试', () => {
    expect(shouldRetryLowcodeQuery(1, new LowcodeApiError('not found', 404))).toBe(false);
    expect(shouldRetryLowcodeQuery(1, new LowcodeApiError('bad request', 400))).toBe(false);
  });

  it('对非 4xx 错误最多重试一次', () => {
    expect(shouldRetryLowcodeQuery(1, new Error('server error'))).toBe(true);
    expect(shouldRetryLowcodeQuery(2, new Error('server error'))).toBe(false);
  });
});
