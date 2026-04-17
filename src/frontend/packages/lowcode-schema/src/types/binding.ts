import type { ScopeRoot, ValueSourceType, ValueType } from '../shared/enums';
import type { JsonValue } from '../shared/json';

/**
 * BindingSchema —— 组件 prop 的统一绑定结构（docx §10.2.4）。
 *
 * 5 种 sourceType：static / variable / expression / workflow_output / chatflow_output。
 * 每种 sourceType 各自所需字段在 union 中显式表达，禁止"all-in-one"扁平结构。
 */
export type BindingSchema =
  | StaticBinding
  | VariableBinding
  | ExpressionBinding
  | WorkflowOutputBinding
  | ChatflowOutputBinding;

export interface BindingBase {
  /** 期望值类型（来自 9 种 valueType），用于设计时与运行时双向校验。*/
  valueType: ValueType;
  /** fallback：当解析失败 / loading 时显示的占位 JSON 值。*/
  fallback?: JsonValue;
  /** 仅供调试 / trace 标识，不影响运行。*/
  trace?: boolean;
}

export interface StaticBinding extends BindingBase {
  sourceType: 'static';
  value: JsonValue;
}

export interface VariableBinding extends BindingBase {
  sourceType: 'variable';
  /**
   * 变量路径，含作用域根。
   * 示例：'page.formValues.name' / 'app.currentUser' / 'system.tenantId'。
   */
  path: string;
  scopeRoot: ScopeRoot;
}

export interface ExpressionBinding extends BindingBase {
  sourceType: 'expression';
  /** JSONata 表达式（M02 lowcode-expression 解析）。*/
  expression: string;
}

export interface WorkflowOutputBinding extends BindingBase {
  sourceType: 'workflow_output';
  workflowId: string;
  /** outputs 字段的 JSONata 路径，留空表示整个 outputs 对象。*/
  outputPath?: string;
}

export interface ChatflowOutputBinding extends BindingBase {
  sourceType: 'chatflow_output';
  chatflowId: string;
  outputPath?: string;
}

/** 与 sourceType 字面量一对一映射，便于类型守卫。*/
export const BINDING_SOURCE_TYPES = [
  'static',
  'variable',
  'expression',
  'workflow_output',
  'chatflow_output'
] satisfies readonly ValueSourceType[];
