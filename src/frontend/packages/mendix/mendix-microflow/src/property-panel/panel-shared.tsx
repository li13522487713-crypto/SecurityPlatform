import { type ReactNode } from "react";
import { Button, Space, Tabs, Tag, Typography } from "@douyinfe/semi-ui";
import { IconClose, IconCopy, IconDelete } from "@douyinfe/semi-icons";
import type { MicroflowAction, MicroflowActionActivity, MicroflowDataType, MicroflowExpression, MicroflowFlow, MicroflowObject } from "../schema";
import type { MicroflowPropertyTabKey } from "../schema/types";
import { defaultMicroflowActionRegistry, defaultMicroflowEdgeRegistry, defaultMicroflowObjectNodeRegistry } from "../node-registry";
import { findObjectWithCollection } from "../schema/utils/object-utils";
import { FieldError } from "./common";
import type { MicroflowEdgePatch, MicroflowPropertyPanelProps } from "./types";
import { countIssuesBySeverity, getIssuesForField, getIssuesForFlow, getIssuesForObject } from "./utils";

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

const tabLabels: Record<MicroflowPropertyTabKey, string> = {
  properties: "Properties",
  documentation: "Documentation",
  errorHandling: "Error Handling",
  output: "Output",
  advanced: "Advanced",
};

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
  const counts = countIssuesBySeverity(props.selectedFlow
    ? getIssuesForFlow(props.validationIssues, props.selectedFlow.id)
    : props.selectedObject
      ? getIssuesForObject(props.validationIssues, props.selectedObject.id, props.selectedObject.kind === "actionActivity" ? props.selectedObject.action.id : undefined)
      : []);
  const runtimeStatus = props.selectedFlow
    ? runtimeStatusForFlow(props.traceFrames ?? [], props.selectedFlow.id)
    : props.selectedObject
      ? runtimeStatusForObject(props.traceFrames ?? [], props.selectedObject.id)
      : undefined;
  return (
    <div style={{ padding: 14, borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
      <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
        <div style={{ minWidth: 0 }}>
          <Title heading={6} style={{ margin: 0 }}>{title}</Title>
          <Text size="small" type="tertiary">{subtitle}</Text>
          {runtimeStatus ? (
            <>
              <br />
              <Text size="small" type={runtimeStatus.status === "failed" ? "danger" : "tertiary"}>
                最近执行：{runtimeStatus.status} · {runtimeStatus.durationMs ?? 0}ms{runtimeStatus.errorMessage ? ` · ${runtimeStatus.errorMessage}` : ""}
              </Text>
            </>
          ) : null}
        </div>
        <Space>
          {runtimeStatus ? <Tag color={runtimeStatus.status === "failed" ? "red" : runtimeStatus.status === "success" || runtimeStatus.status === "visited" ? "green" : "grey"}>{runtimeStatus.status}</Tag> : <Tag color="grey">notRun</Tag>}
          {counts.errors > 0 ? <Tag color="red">{counts.errors} error</Tag> : null}
          {counts.warnings > 0 ? <Tag color="orange">{counts.warnings} warning</Tag> : null}
          {onDuplicate ? <Button icon={<IconCopy />} theme="borderless" onClick={onDuplicate} disabled={props.readonly} /> : null}
          {onDelete ? <Button icon={<IconDelete />} theme="borderless" type="danger" onClick={onDelete} disabled={props.readonly} /> : null}
          <Button icon={<IconClose />} theme="borderless" onClick={props.onClose} />
        </Space>
      </Space>
    </div>
  );
}

function runtimeStatusForObject(frames: MicroflowPropertyPanelProps["traceFrames"], objectId: string) {
  const frame = [...(frames ?? [])].reverse().find(item => item.objectId === objectId);
  if (!frame) {
    return undefined;
  }
  return { status: frame.status, durationMs: frame.durationMs, errorMessage: frame.error?.message };
}

function runtimeStatusForFlow(frames: MicroflowPropertyPanelProps["traceFrames"], flowId: string) {
  const frame = [...(frames ?? [])].reverse().find(item => item.incomingFlowId === flowId || item.outgoingFlowId === flowId);
  if (!frame) {
    return undefined;
  }
  return {
    status: frame.outgoingFlowId === flowId || frame.incomingFlowId === flowId ? "visited" : "notRun",
    durationMs: frame.durationMs,
    errorMessage: frame.error?.flowId === flowId ? frame.error.message : undefined,
  };
}

export function Field({ label, children, issues }: { label: string; children: ReactNode; issues?: ReturnType<typeof getIssuesForField> }) {
  return (
    <label style={{ display: "grid", gap: 6 }}>
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

export function getFlowEdgeKind(flow: MicroflowFlow): "sequence" | "decisionCondition" | "objectTypeCondition" | "errorHandler" | "annotation" {
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
}: {
  tabs: MicroflowPropertyTabKey[];
  activeKey: MicroflowPropertyTabKey;
  onChange: (key: MicroflowPropertyTabKey) => void;
}) {
  return (
    <Tabs
      activeKey={activeKey}
      size="small"
      style={{ padding: "0 14px", borderBottom: "1px solid var(--semi-color-border, #e5e6eb)" }}
      onChange={key => onChange(key as MicroflowPropertyTabKey)}
    >
      {tabs.map(tab => <Tabs.TabPane key={tab} itemKey={tab} tab={tabLabels[tab]} />)}
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
