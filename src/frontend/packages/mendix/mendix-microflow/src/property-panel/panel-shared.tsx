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
    const actionItem = defaultMicroflowActionRegistry.find(item => item.kind === object.action.kind);
    return actionItem?.titleZh ?? object.caption ?? object.action.kind;
  }
  const objectItem = defaultMicroflowObjectNodeRegistry.find(item => item.objectKind === object.kind);
  return objectItem?.titleZh ?? object.caption ?? object.kind;
}

export function objectSubtitle(object: MicroflowObject): string {
  if (object.kind === "actionActivity") {
    const actionItem = defaultMicroflowActionRegistry.find(item => item.kind === object.action.kind);
    return actionItem?.title ?? object.action.kind;
  }
  const objectItem = defaultMicroflowObjectNodeRegistry.find(item => item.objectKind === object.kind);
  return objectItem?.title ?? object.kind;
}

export function objectIconGlyph(object: MicroflowObject): string {
  if (object.kind === "actionActivity") {
    switch (object.action.kind) {
      case "createObject":
      case "retrieve":
      case "changeMembers":
      case "delete":
        return "●";
      case "createList":
      case "aggregateList":
      case "listOperation":
      case "changeList":
        return "▦";
      case "createVariable":
      case "changeVariable":
        return "𝑥";
      case "callMicroflow":
        return "⚡";
      case "restCall":
        return "⇄";
      default:
        return "•";
    }
  }
  switch (object.kind) {
    case "startEvent":
      return "▶";
    case "endEvent":
      return "■";
    case "exclusiveSplit":
      return "◇";
    case "loopedActivity":
      return "↻";
    case "exclusiveMerge":
      return "⊕";
    case "annotation":
      return "✎";
    case "tryCatch":
      return "⚠";
    default:
      return "•";
  }
}

export function actionPatch(action: MicroflowAction, patch: Partial<MicroflowAction>): MicroflowAction {
  return { ...action, ...patch } as MicroflowAction;
}

export function updateAction(activity: MicroflowActionActivity, patch: Partial<MicroflowAction>): MicroflowActionActivity {
  return { ...activity, action: actionPatch(activity.action, patch) };
}

const defaultTabLabels: Record<MicroflowPropertyTabKey, string> = {
  properties: "Configuration",
  documentation: "Documentation",
  errorHandling: "Error Handling",
  output: "Input / Output",
  advanced: "Advanced",
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

export function Header({ props, title, subtitle, icon, onDelete, onDuplicate }: {
  props: MicroflowPropertyPanelProps;
  title: string;
  subtitle: string;
  icon?: ReactNode;
  onDelete?: () => void;
  onDuplicate?: () => void;
}) {
  const readonlyDisabledReason = props.readonly ? "Readonly mode cannot edit this object." : "";
  return (
    <div style={{ padding: 14, borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
      <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
        <div style={{ minWidth: 0 }}>
          <Space align="center" spacing={8}>
            {icon ? (
              <span
                aria-hidden="true"
                style={{
                  width: 18,
                  height: 18,
                  borderRadius: 9,
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  background: "rgba(22, 93, 255, 0.12)",
                  color: "#165dff",
                  fontSize: 11,
                  fontWeight: 700,
                  flex: "0 0 auto",
                }}
              >
                {icon}
              </span>
            ) : null}
            <Title heading={6} style={{ margin: 0, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{title}</Title>
          </Space>
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
    return { properties: "Parameters" };
  }
  if (object.kind === "endEvent") {
    return { properties: "Return Value", documentation: "Documentation" };
  }
  if (object.kind === "exclusiveSplit") {
    return { properties: "Cases", documentation: "Documentation" };
  }
  if (object.kind === "loopedActivity") {
    return { properties: "Configuration", documentation: "Documentation" };
  }
  if (object.kind === "exclusiveMerge") {
    return { documentation: "Documentation" };
  }
  if (object.kind === "annotation") {
    return { properties: "Documentation" };
  }
  if (object.kind === "tryCatch") {
    return { properties: "Configuration", documentation: "Documentation" };
  }
  if (object.kind === "actionActivity") {
    if (object.action.kind === "createObject") {
      return { properties: "Configuration", output: "Input / Output", documentation: "Documentation" };
    }
    if (object.action.kind === "callMicroflow") {
      return { properties: "Configuration", output: "Input / Output", documentation: "Documentation" };
    }
    if (object.action.kind === "restCall") {
      return { properties: "General", advanced: "Request", output: "Response", errorHandling: "Authentication", documentation: "Documentation" };
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
