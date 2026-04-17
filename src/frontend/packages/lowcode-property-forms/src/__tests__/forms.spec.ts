import { describe, it, expect } from 'vitest';
import type { ComponentMeta, ComponentSchema, ContentParamKind } from '@atlas/lowcode-schema';
import { switchSource, isCompatible } from '../value-source';
import { defaultContentParam, switchKind, listKinds, KIND_LABEL, ensureExternalLinkAllowed, setLinkPolicyChecker } from '../content-params';
import { computeFieldVMs, validateFields } from '../renderer';

describe('value-source', () => {
  it('5 种 sourceType 切换', () => {
    expect(switchSource('static', { valueType: 'string' }).sourceType).toBe('static');
    expect(switchSource('variable', { valueType: 'string' }).sourceType).toBe('variable');
    expect(switchSource('expression', { valueType: 'number' }).sourceType).toBe('expression');
    expect(switchSource('workflow_output', { valueType: 'array' }).sourceType).toBe('workflow_output');
    expect(switchSource('chatflow_output', { valueType: 'string' }).sourceType).toBe('chatflow_output');
  });
  it('isCompatible 当前全允许', () => {
    expect(isCompatible('static', 'string')).toBe(true);
  });
  it('static 默认值按 valueType 分发', () => {
    expect((switchSource('static', { valueType: 'string' }) as { value: string }).value).toBe('');
    expect((switchSource('static', { valueType: 'number' }) as { value: number }).value).toBe(0);
    expect((switchSource('static', { valueType: 'boolean' }) as { value: boolean }).value).toBe(false);
    expect((switchSource('static', { valueType: 'array' }) as { value: unknown[] }).value).toEqual([]);
    expect((switchSource('static', { valueType: 'object' }) as { value: object }).value).toEqual({});
  });
});

describe('content-params', () => {
  it('listKinds 6 类', () => {
    const kinds = listKinds();
    expect(kinds.length).toBe(6);
  });

  it('defaultContentParam 6 类默认配置', () => {
    for (const k of listKinds() as ReadonlyArray<ContentParamKind>) {
      const cp = defaultContentParam(k, `c-${k}`);
      expect(cp.kind).toBe(k);
      expect(cp.code).toBe(`c-${k}`);
      expect(KIND_LABEL[k].length).toBeGreaterThan(0);
    }
  });

  it('switchKind 保留 code/description', () => {
    const prev = defaultContentParam('text', 'c1');
    prev.description = '描述';
    const next = switchKind(prev, 'image');
    expect(next.kind).toBe('image');
    expect(next.code).toBe('c1');
    expect(next.description).toBe('描述');
  });

  it('外链白名单 checker 可配置', () => {
    expect(ensureExternalLinkAllowed('https://example.com').allowed).toBe(true);
    setLinkPolicyChecker({ isAllowed: () => false });
    expect(ensureExternalLinkAllowed('https://evil.com').allowed).toBe(false);
    setLinkPolicyChecker({ isAllowed: () => true });
  });
});

describe('renderer 元数据驱动', () => {
  const meta: ComponentMeta = {
    type: 'Demo',
    displayName: 'Demo',
    category: 'misc',
    version: '1.0.0',
    runtimeRenderer: ['web'],
    bindableProps: ['placeholder', 'disabled'],
    supportedEvents: ['onChange'],
    childPolicy: { arity: 'none' },
    propertyPanels: [
      {
        group: 'basic',
        label: '基础',
        fields: [
          { key: 'placeholder', label: '占位符', renderer: 'input', valueType: 'string', required: true },
          { key: 'disabled', label: '禁用', renderer: 'switch', valueType: 'boolean' },
          {
            key: 'helperText',
            label: '帮助文本',
            renderer: 'input',
            valueType: 'string',
            dependsOn: { field: 'disabled', equals: false }
          }
        ]
      }
    ]
  };

  const c: ComponentSchema = { id: 'demo-1', type: 'Demo', props: { disabled: true } };

  it('computeFieldVMs 正确生成', () => {
    const vms = computeFieldVMs(meta, c);
    expect(vms.length).toBe(3);
    const helper = vms.find((v) => v.field.key === 'helperText')!;
    expect(helper.visible).toBe(false); // dependsOn disabled=false 不满足
  });

  it('validateFields 必填提示', () => {
    const vms = computeFieldVMs(meta, c);
    const issues = validateFields(vms);
    expect(issues.find((i) => i.fieldKey === 'placeholder')).toBeDefined();
  });
});
