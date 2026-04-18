import { describe, it, expect } from 'vitest';
import { isValidCron } from '..';

describe('cron 表达式校验', () => {
  it('合法表达式（5 字段 / 6 字段）', () => {
    expect(isValidCron('0 0 * * *')).toBe(true);
    expect(isValidCron('*/5 * * * *')).toBe(true);
    expect(isValidCron('0 0 * * * *')).toBe(true); // 6 字段 quartz
    expect(isValidCron('0 9-17 * * MON-FRI')).toBe(true);
  });
  it('非法表达式', () => {
    expect(isValidCron('')).toBe(false);
    expect(isValidCron('not-cron')).toBe(false);
    expect(isValidCron('1 2 3')).toBe(false);
  });
});
