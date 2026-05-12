import { type ReactNode } from "react";
import { Button, Space, Tabs, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconClose, IconCopy, IconDelete } from "@douyinfe/semi-icons";
import type { MicroflowAction, MicroflowActionActivity, MicroflowDataType, MicroflowExpression, MicroflowFlow, MicroflowObject } from "../schema";
import type { MicroflowPropertyTabKey } from "../schema/types";
import { defaultMicroflowActionRegistry, defaultMicroflowEdgeRegistry, defaultMicroflowObjectNodeRegistry } from "../node-registry";
import { findObjectWithCollection } from "../schema/utils/object-utils";
import { FieldError } from "./common";
import type { MicroflowEdgePatch, MicroflowPropertyPanelProps } from "./types";
import { getIssuesForField } from "./utils";

const { Text, Title } = Typography;

export function expression(raw = "", inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: []
  };
}

export function objectTitle(object: MicroflowObject): string {
  if (object.kind === "actionActivity") {
    return `${object.caption} (${object.action.kind})`;
  }
  return object.caption ?? object.kind;
}

export function actionPatch(action: MicroflowAction, patch: Partial<MicroflowAction>): MicroflowAction {
  return { ...action, ...patch } as MicroflowAction;
}

export function updateAction(activity: MicroflowActionActivity, patch: Partial<MicroflowAction>): MicroflowActionActivity {
  return { ...activity, action: actionPatch(activity.action, patch) };
}

const defaultTabLabels: Record<MicroflowPropertyTabKey, string> = {
  properties: "配置",
  documentation: "文档",
  errorHandling: "错误处理",
  output: "输入 / 输出",
  advanced: "高级",
};

type TabLabelMap = Partial<Record<MicroflowPropertyTabKey, string>>;

export function issuesFor(props: MicroflowPropertyPanelProps, objectId?: string, flowId?: string, actionId?: string) {
  if (flowId) {
    return getIssuesForFlow(props.validationIssues, flowId);
  }
  if (objectId) {
    return getIssuesForObject(props.validationIssues, objectId, actionId);
  }
  return [];
}

export function Header({ props, title, subtitle, onDelete, onDuplicate }: {
  props: MicroflowPropertyPanelProps;
  title: string;
  subtitle: string;
  onDelete?: () => void;
  onDuplicate?: () => void;
}) {
  const readonlyDisabledReason = props.readonly ? "Readonly mode cannot edit this object." : "";
  return (
    <div style={{ padding: 14, borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
      <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
        <div style={{ minWidth: 0 }}>
          <Title heading={6} style={{ margin: 0, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{title}</Title>
          <Text size="small" type="tertiary">{subtitle}</Text>
        </div>
        <Space>
          {onDuplicate ? (
            <Tooltip content={readonlyDisabledReason || "Duplicate"}>
              <span style={{ display: "inline-flex" }}>
                <Button icon={<IconCopy />} theme="borderless" onClick={onDuplicate} disabled={props.readonly} />
              </span>
            </Tooltip>
          ) : null}
          {onDelete ? (
            <Tooltip content={readonlyDisabledReason || "Delete"}>
              <span style={{ display: "inline-flex" }}>
                <Button icon={<IconDelete />} theme="borderless" type="danger" onClick={onDelete} disabled={props.readonly} />
              </span>
            </Tooltip>
          ) : null}
          <Button icon={<IconClose />} theme="borderless" onClick={props.onClose} />
        </Space>
      </Space>
    </div>
  );
}

export function Field({
  label,
  children,
  issues,
  fieldPath,
}: {
  label: string;
  children: ReactNode;
  issues?: ReturnType<typeof getIssuesForField>;
  fieldPath?: string;
}) {
  const resolvedFieldPath = fieldPath ?? issues?.[0]?.fieldPath;
  return (
    <label style={{ display: "grid", gap: 6 }} data-mf-field-path={resolvedFieldPath || undefined}>
      <Text size="small" strong>{label}</Text>
      {children}
      <FieldError issues={issues} />
    </label>
  );
}

export function getObjectTabs(object: MicroflowObject): MicroflowPropertyTabKey[] {
  if (object.kind === "actionActivity") {
    return defaultMicroflowActionRegistry.find(item => item.kind === object.action.kind)?.propertyTabs ?? ["properties", "documentation", "errorHandling", "output", "advanced"];
  }
  return defaultMicroflowObjectNodeRegistry.find(item => item.objectKind === object.kind)?.propertyTabs ?? ["properties", "documentation"];
}

export function getObjectTabLabels(object: MicroflowObject): TabLabelMap {
  if (object.kind === "startEvent") {
    return { properties: "参数" };
  }
  if (object.kind === "endEvent") {
    return { properties: "返回值", documentation: "文档" };
  }
  if (object.kind === "exclusiveSplit") {
    return { properties: "分支", documentation: "文档" };
  }
  if (object.kind === "loopedActivity") {
    return { properties: "循环配置", documentation: "文档" };
  }
  if (object.kind === "exclusiveMerge") {
    return { properties: "合并配置" };
  }
  if (object.kind === "annotation") {
    return { properties: "文档" };
  }
  if (object.kind === "tryCatch") {
    return { properties: "分支配置", documentation: "文档" };
  }
  if (object.kind === "actionActivity") {
    if (object.action.kind === "createObject") {
      return { properties: "配置", output: "输入 / 输出", documentation: "文档" };
    }
    if (object.action.kind === "callMicroflow") {
      return { properties: "配置", output: "输入 / 输出", documentation: "文档" };
    }
    if (object.action.kind === "restCall") {
      return { properties: "基本配置", advanced: "请求", output: "响应", errorHandling: "错误处理", documentation: "文档" };
    }
  }
  return {};
}

export function getFlowEdgeKind(flow: MicroflowFlow): "sequence" | "decisionCondition" | "objectTypeCondition" | "loopBody" | "errorHandler" | "annotation" {
  if (flow.kind === "annotation") {
    return "annotation";
  }
  if (flow.isErrorHandler) {
    return "errorHandler";
  }
  return flow.editor.edgeKind;
}

export function getFlowTabs(flow: MicroflowFlow): MicroflowPropertyTabKey[] {
  const edgeKind = getFlowEdgeKind(flow);
  return (defaultMicroflowEdgeRegistry.find(item => item.edgeKind === edgeKind)?.propertyTabs ?? ["properties", "documentation"]) as MicroflowPropertyTabKey[];
}

export function PropertyTabs({
  tabs,
  activeKey,
  onChange,
  labels,
}: {
  tabs: MicroflowPropertyTabKey[];
  activeKey: MicroflowPropertyTabKey;
  onChange: (key: MicroflowPropertyTabKey) => void;
  labels?: TabLabelMap;
}) {
  return (
    <Tabs
      activeKey={activeKey}
      size="small"
      style={{ padding: "0 14px", borderBottom: "1px solid var(--semi-color-border, #e5e6eb)" }}
      onChange={key => onChange(key as MicroflowPropertyTabKey)}
    >
      {tabs.map(tab => <Tabs.TabPane key={tab} itemKey={tab} tab={labels?.[tab] ?? defaultTabLabels[tab]} />)}
    </Tabs>
  );
}

export function updateObjectDocumentation(object: MicroflowObject, documentation: string): MicroflowObject {
  return { ...object, documentation } as MicroflowObject;
}

export function updateObjectAdvanced(object: MicroflowObject, patch: Record<string, unknown>): MicroflowObject {
  return ({
    ...object,
    editor: {
      ...object.editor,
      advanced: {
        ...((object.editor as unknown as { advanced?: Record<string, unknown> }).advanced ?? {}),
        ...patch,
      },
    },
  } as unknown) as MicroflowObject;
}

export function dataTypeLabel(dataType?: MicroflowDataType): string {
  if (!dataType) {
    return "unknown";
  }
  if (dataType.kind === "enumeration") {
    return `enumeration:${dataType.enumerationQualifiedName}`;
  }
  if (dataType.kind === "object") {
    return `object:${dataType.entityQualifiedName}`;
  }
  if (dataType.kind === "list") {
    return `list:${dataTypeLabel(dataType.itemType)}`;
  }
  return dataType.kind;
}

export function dataTypeFromKey(kind: string): MicroflowDataType {
  if (kind === "boolean" || kind === "integer" || kind === "long" || kind === "decimal" || kind === "dateTime" || kind === "string") {
    return { kind };
  }
  return { kind: "string" };
}

export function objectName(schema: MicroflowPropertyPanelProps["schema"], objectId: string): string {
  const object = findObjectWithCollection(schema, objectId)?.object;
  return object?.caption ?? object?.kind ?? objectId;
}

export function flowPatch(flow: MicroflowFlow, patch: MicroflowEdgePatch): MicroflowFlow {
  return { ...flow, ...patch } as MicroflowFlow;
}
