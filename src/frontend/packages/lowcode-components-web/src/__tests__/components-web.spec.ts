import { describe, it, expect, beforeEach } from 'vitest';
import { ALL_METAS, AI_METAS, DATA_METAS, DISPLAY_METAS, INPUT_METAS, LAYOUT_METAS } from '../meta/categories';
import { registerAllWebComponents } from '../meta';
import { __resetRegistryForTesting, listMetas } from '@atlas/lowcode-component-registry';

beforeEach(() => __resetRegistryForTesting());

describe('lowcode-components-web', () => {
  it('5 大类齐全', () => {
    expect(LAYOUT_METAS.length).toBeGreaterThanOrEqual(8);
    expect(DISPLAY_METAS.length).toBeGreaterThanOrEqual(13);
    expect(INPUT_METAS.length).toBeGreaterThanOrEqual(18);
    expect(AI_METAS.length).toBe(4);
    expect(DATA_METAS.length).toBeGreaterThanOrEqual(4);
  });

  it('总数 ≥ 30 + AI 原生 4 件', () => {
    expect(ALL_METAS.length).toBeGreaterThanOrEqual(30);
    const ai = ALL_METAS.filter((m) => m.category === 'ai');
    expect(ai.length).toBe(4);
  });

  it('AI 原生组件均含 contentParams=ai 或 data', () => {
    for (const m of AI_METAS) {
      expect(m.contentParams && m.contentParams.length).toBeGreaterThan(0);
    }
  });

  it('registerAllWebComponents 一次性注册成功', () => {
    registerAllWebComponents();
    expect(listMetas().length).toBe(ALL_METAS.length);
  });

  it('6 维矩阵零空缺：每个组件都至少满足 1 维', () => {
    for (const m of ALL_METAS) {
      const dim1Form = m.supportedValueType && Object.keys(m.supportedValueType).length > 0;
      const dim2Event = m.supportedEvents.length > 0;
      const dim3Workflow = m.bindableProps.length > 0; // workflow output 通过 binding 回填
      const dim4Ai = m.category === 'ai';
      const dim5Upload = m.type === 'FileUpload' || m.type === 'ImageUpload';
      const dim6ContentParam = (m.contentParams ?? []).length > 0;
      const total = [dim1Form, dim2Event, dim3Workflow, dim4Ai, dim5Upload, dim6ContentParam].filter(Boolean).length;
      expect(total, `组件 ${m.type} 6 维矩阵应至少满足 1 维`).toBeGreaterThanOrEqual(1);
    }
  });
});
