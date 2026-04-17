/**
 * 5 种值源切换（M05 C05-4）。
 *
 * 在属性面板中，单个字段允许在 5 种 sourceType 之间切换：
 *   static / variable / expression / workflow_output / chatflow_output
 *
 * 切换时按目标 sourceType 构造默认 BindingSchema；保留 valueType / fallback 等公共字段。
 */

import type { BindingSchema, ValueSourceType, ValueType } from '@atlas/lowcode-schema';

export interface SwitchOptions {
  /** 期望 valueType（可不变）。*/
  valueType: ValueType;
  /** 切换前的 fallback。*/
  fallback?: BindingSchema['fallback'];
  /** 默认变量路径（变量源场景下作为初始值）。*/
  defaultVariablePath?: string;
  /** 默认表达式（表达式源场景下作为初始值）。*/
  defaultExpression?: string;
}

export function switchSource(target: ValueSourceType, opts: SwitchOptions): BindingSchema {
  const base = { valueType: opts.valueType, fallback: opts.fallback } as const;
  switch (target) {
    case 'static':
      return { ...base, sourceType: 'static', value: defaultStatic(opts.valueType) };
    case 'variable':
      return { ...base, sourceType: 'variable', path: opts.defaultVariablePath ?? 'page.', scopeRoot: 'page' };
    case 'expression':
      return { ...base, sourceType: 'expression', expression: opts.defaultExpression ?? '' };
    case 'workflow_output':
      return { ...base, sourceType: 'workflow_output', workflowId: '' };
    case 'chatflow_output':
      return { ...base, sourceType: 'chatflow_output', chatflowId: '' };
  }
}

function defaultStatic(valueType: ValueType): BindingSchema['fallback'] {
  switch (valueType) {
    case 'string': return '';
    case 'number': return 0;
    case 'boolean': return false;
    case 'array': return [];
    case 'object': return {};
    case 'date': return null;
    case 'file': return null;
    case 'image': return null;
    case 'any': return null;
  }
}

/** 校验 sourceType 与 valueType 是否兼容（5 值源 × 9 valueType 矩阵）。*/
export function isCompatible(sourceType: ValueSourceType, valueType: ValueType): boolean {
  // 当前阶段全允许；未来可基于 valueType 限制（如 file 不允许 expression 直接产出 File）。
  void sourceType;
  void valueType;
  return true;
}
