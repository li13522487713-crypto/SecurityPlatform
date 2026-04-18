import type { BindingSchema } from './binding';
import type { ContentParamSchema } from './content-param';
import type { EventSchema } from './event';
import type { JsonObject, JsonValue } from '../shared/json';
import type { RendererType, ValueType } from '../shared/enums';

/**
 * ComponentSchema —— 组件实例 schema（docx §10.2.4）。
 *
 * 组件实例 = 类型 + 实例 ID + props（含 BindingSchema）+ children + events + slots。
 * 组件能力的元数据（哪些 prop 可绑定 / 支持哪些事件 / 子策略）在 ComponentMeta 中声明（M06）。
 */
export interface ComponentSchema {
  /** 组件实例唯一 ID（PageSchema 内唯一，跨页面允许重复）。*/
  id: string;
  /** 组件类型（与 ComponentMeta.type 对应）。*/
  type: string;
  /** 实例 props（key=prop 名, value=BindingSchema 或字面 JSON）。*/
  props?: Record<string, BindingSchema | JsonValue>;
  /** 内容参数实例（按 ComponentMeta.contentParams 声明配置）。*/
  contentParams?: ContentParamSchema[];
  /** 事件绑定。*/
  events?: EventSchema[];
  /** 子组件（递归结构，按 ComponentMeta.childPolicy 约束）。*/
  children?: ComponentSchema[];
  /** 命名插槽：key=slot 名, value=组件子树。*/
  slots?: Record<string, ComponentSchema[]>;
  /** 设计期可见性（隐藏组件依旧渲染逻辑可用）。*/
  visible?: boolean;
  /** 锁定（编辑时禁止修改）。*/
  locked?: boolean;
  /** 元信息：注释、备注、设计期标签。*/
  metadata?: JsonObject;
}

/** SlotSchema —— 命名插槽元数据（用于 ComponentMeta 声明）。*/
export interface SlotSchema {
  name: string;
  description?: string;
  /** 是否允许多个子组件，false 时仅允许 1 个。*/
  multiple?: boolean;
  /** 限定可放入的组件 type 白名单（空表示不限）。*/
  allowComponentTypes?: string[];
}

/** PropertyPanelSchema —— 属性面板分组与字段元数据（用于 propertyPanels 元数据驱动渲染，M05）。*/
export interface PropertyPanelSchema {
  group: string;
  label: string;
  fields: PropertyPanelField[];
  /** 折叠默认状态。*/
  collapsed?: boolean;
}

export interface PropertyPanelField {
  /** 字段 key，与 ComponentSchema.props 的 key 对齐。*/
  key: string;
  label: string;
  /** 渲染器类型（input / number / select / switch / monaco-expr / value-source / ...）。*/
  renderer: string;
  /** 渲染器参数（如 select 的 options）。*/
  rendererProps?: JsonObject;
  /** 该字段允许的取值类型（对齐 ValueType）。*/
  valueType?: ValueType;
  /** 字段依赖的另一字段是否为某值才显示。*/
  dependsOn?: { field: string; equals: JsonValue };
  /** 是否必填。*/
  required?: boolean;
  /** 默认值（设计期）。*/
  defaultValue?: JsonValue;
}

/**
 * ComponentMeta —— 组件能力元数据（docx §10.3，M06 完整落地）。
 *
 * 强约束（PLAN.md §1.3 #4）：组件实现内禁止硬编码业务逻辑（fetch / import workflow client）。
 * 所有可绑定 prop / 支持事件 / 子策略 / 内容参数必须在 ComponentMeta 中声明。
 */
export interface ComponentMeta {
  type: string;
  displayName: string;
  category: string;
  group?: string;
  icon?: string;
  version: string;
  /** 该组件支持的渲染器矩阵（web / mini-wx / mini-douyin / h5）。*/
  runtimeRenderer: readonly RendererType[];
  /** 可被表达式引用的内置 prop 列表（不在该列表中的 prop 不允许 binding）。*/
  bindableProps: readonly string[];
  /** 该组件支持的内容参数类型。*/
  contentParams?: readonly ContentParamSchema['kind'][];
  /** 该组件支持的事件名。*/
  supportedEvents: readonly EventSchema['name'][];
  /** 子组件接受策略。*/
  childPolicy: ChildPolicy;
  /** 命名插槽声明。*/
  slots?: SlotSchema[];
  /** 属性面板声明（M05 渲染）。*/
  propertyPanels?: PropertyPanelSchema[];
  /** 该组件 prop 默认 valueType（用于自动推断）。*/
  supportedValueType?: Record<string, ValueType>;
}

export interface ChildPolicy {
  /** none：不接受子组件；one：仅 1 个；many：多个。*/
  arity: 'none' | 'one' | 'many';
  /** 限定可放入的组件 type 白名单（空表示不限）。*/
  allowTypes?: string[];
}
