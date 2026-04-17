import { describe, it, expect } from 'vitest';

describe('lowcode-preview-web 端口约束', () => {
  it('端口为 5184（PLAN.md §3.2）', async () => {
    const mod = await import('../index');
    expect(mod.ATLAS_LOWCODE_PREVIEW_WEB_PORT).toBe(5184);
  });
});
