/**
 * P1-1 守门测试：47 件 ComponentMeta 必须有对应的 React 组件实现 + propertyPanels。
 *
 * 注：单测在 node 环境跑，不能直接 import 组件 .tsx（会触发 @douyinfe/semi-icons CJS 转换问题）。
 * 改为 import `implementation-keys.ts` 的纯静态清单做契约校验：
 *  - 实现 type 清单与 ComponentMeta type 一一对应（防漏防多）；
 *  - implementation-keys 导出的 47 个 type 与 ALL_METAS 严格相等；
 *  - propertyPanels 完整性同步校验（每字段有 renderer）。
 *
 * 真正的 React 实现可由 lowcode-studio-web / lowcode-preview-web 在 jsdom 环境通过 Playwright 端到端验证。
 */
import { describe, it, expect } from 'vitest';
import { ALL_METAS } from '../meta/categories';
import { ALL_IMPLEMENTATION_TYPES, IMPLEMENTATION_TYPE_SET, hasImplementation } from '../components/implementation-keys';

describe('lowcode-components-web 实现完整性（P1-1）', () => {
  it('所有 ComponentMeta 必须在实现清单中', () => {
    const missing: string[] = [];
    for (const meta of ALL_METAS) {
      if (!hasImplementation(meta.type)) {
        missing.push(meta.type);
      }
    }
    expect(missing, `以下组件 type 缺少 React 实现：${missing.join(', ')}`).toEqual([]);
  });

  it('实现 type 清单数量必须 ≥ 47', () => {
    expect(ALL_IMPLEMENTATION_TYPES.length).toBeGreaterThanOrEqual(47);
  });

  it('实现 type 与 ALL_METAS 完全一致（防漏防多）', () => {
    const metaTypes = new Set(ALL_METAS.map((m) => m.type));
    const onlyInMeta = [...metaTypes].filter((t) => !IMPLEMENTATION_TYPE_SET.has(t));
    const onlyInImpl = [...IMPLEMENTATION_TYPE_SET].filter((t) => !metaTypes.has(t));
    expect(onlyInMeta, '在 meta 但实现未声明').toEqual([]);
    expect(onlyInImpl, '在实现但 meta 未声明').toEqual([]);
  });

  it('每个 ComponentMeta 必须含 propertyPanels（PLAN P1-2 元数据驱动表单前置）', () => {
    const missing = ALL_METAS.filter((m) => !m.propertyPanels || m.propertyPanels.length === 0)
      .map((m) => m.type);
    expect(missing, `以下组件缺 propertyPanels：${missing.join(', ')}`).toEqual([]);
  });

  it('每个 propertyPanels 字段必须有 renderer', () => {
    const issues: string[] = [];
    for (const m of ALL_METAS) {
      for (const p of m.propertyPanels ?? []) {
        for (const f of p.fields) {
          if (!f.renderer) issues.push(`${m.type}.${p.group}.${f.key}`);
        }
      }
    }
    expect(issues, `字段缺 renderer：${issues.join(', ')}`).toEqual([]);
  });
});
