import { describe, it, expect } from 'vitest';
import { STUDIO_MESSAGES } from '../i18n';

describe('lowcode-studio-web i18n', () => {
  it('zh-CN 与 en-US 词条 key 完全对齐', () => {
    const zhKeys = Object.keys(STUDIO_MESSAGES['zh-CN']).sort();
    const enKeys = Object.keys(STUDIO_MESSAGES['en-US']).sort();
    expect(zhKeys).toEqual(enKeys);
    expect(zhKeys.length).toBeGreaterThanOrEqual(50);
  });
});
