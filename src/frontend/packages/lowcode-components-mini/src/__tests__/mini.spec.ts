import { describe, it, expect, beforeEach } from 'vitest';
import { ALL_MINI_METAS, MINI_CAPABILITY_MATRIX, getMiniRenderers, registerAllMiniComponents } from '..';
import { __resetRegistryForTesting, listMetas } from '@atlas/lowcode-component-registry';

beforeEach(() => __resetRegistryForTesting());

describe('lowcode-components-mini', () => {
  it('能力矩阵覆盖关键组件', () => {
    expect(MINI_CAPABILITY_MATRIX['CodeEditor']).toEqual([]);
    expect(MINI_CAPABILITY_MATRIX['Chart']).toEqual(['h5']);
  });

  it('未列出组件默认 3 端全支持', () => {
    expect(getMiniRenderers('Button').length).toBe(3);
  });

  it('CodeEditor 因 0 端支持被过滤', () => {
    expect(ALL_MINI_METAS.find((m) => m.type === 'CodeEditor')).toBeUndefined();
  });

  it('Chart 仅保留 h5', () => {
    const chart = ALL_MINI_METAS.find((m) => m.type === 'Chart');
    expect(chart?.runtimeRenderer).toEqual(['h5']);
  });

  it('注册全部 mini 组件成功', () => {
    registerAllMiniComponents();
    expect(listMetas().length).toBe(ALL_MINI_METAS.length);
  });
});
