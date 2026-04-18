import { describe, it, expect } from 'vitest';
import { isUrlAllowed } from '..';

describe('isUrlAllowed', () => {
  it('精确匹配', () => {
    expect(isUrlAllowed('https://example.com/p', ['example.com'])).toBe(true);
    expect(isUrlAllowed('https://other.com', ['example.com'])).toBe(false);
  });
  it('通配子域（.example.com）', () => {
    expect(isUrlAllowed('https://api.example.com', ['.example.com'])).toBe(true);
    expect(isUrlAllowed('https://example.com', ['.example.com'])).toBe(true);
    expect(isUrlAllowed('https://other.com', ['.example.com'])).toBe(false);
  });
  it('非法 URL 拒绝', () => {
    expect(isUrlAllowed('not-a-url', ['example.com'])).toBe(false);
  });
});
