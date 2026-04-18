/**
 * 元数据驱动属性面板渲染器（M05 C05-3）。
 *
 * 输入：ComponentMeta.propertyPanels + 当前 ComponentSchema.props 值
 * 输出：Field 视图模型数组（label / renderer / value / dependsOn 是否可见 / 校验状态）
 *
 * React 表单组件（基于 Semi `Form`）由 M07 lowcode-studio-web 用本视图模型渲染。
 */

import type { BindingSchema, ComponentMeta, ComponentSchema, JsonValue, PropertyPanelField, PropertyPanelSchema } from '@atlas/lowcode-schema';

export interface FieldViewModel {
  group: string;
  groupLabel: string;
  /** 是否折叠（与 PropertyPanel.collapsed 对齐）。*/
  collapsedByDefault: boolean;
  field: PropertyPanelField;
  /** 当前值（可能是字面 JSON 也可能是 BindingSchema）。*/
  currentValue: BindingSchema | JsonValue | undefined;
  /** dependsOn 校验后是否可见。*/
  visible: boolean;
}

/** 计算所有字段的可见性（按 dependsOn）。*/
export function computeFieldVMs(meta: ComponentMeta, component: ComponentSchema): FieldViewModel[] {
  const props = component.props ?? {};
  const out: FieldViewModel[] = [];
  const panels: ReadonlyArray<PropertyPanelSchema> = meta.propertyPanels ?? [];
  for (const panel of panels) {
    for (const field of panel.fields) {
      const vm: FieldViewModel = {
        group: panel.group,
        groupLabel: panel.label,
        collapsedByDefault: !!panel.collapsed,
        field,
        currentValue: props[field.key] as BindingSchema | JsonValue | undefined,
        visible: true
      };
      if (field.dependsOn) {
        const peer = props[field.dependsOn.field];
        vm.visible = isLooselyEqual(peer as JsonValue, field.dependsOn.equals);
      }
      out.push(vm);
    }
  }
  return out;
}

/** 简单的设计期校验：必填 / valueType 字面量匹配。*/
export interface FieldValidationIssue {
  fieldKey: string;
  message: string;
}

export function validateFields(vms: ReadonlyArray<FieldViewModel>): FieldValidationIssue[] {
  const issues: FieldValidationIssue[] = [];
  for (const vm of vms) {
    if (!vm.visible) continue;
    if (vm.field.required && (vm.currentValue === undefined || vm.currentValue === null || vm.currentValue === '')) {
      issues.push({ fieldKey: vm.field.key, message: `字段 ${vm.field.label} 必填` });
    }
  }
  return issues;
}

function isLooselyEqual(a: JsonValue | undefined, b: JsonValue): boolean {
  if (a === undefined) return false;
  if (a === b) return true;
  return JSON.stringify(a) === JSON.stringify(b);
}
