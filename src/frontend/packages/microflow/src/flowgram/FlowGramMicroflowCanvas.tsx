import { useMemo, useState, type DragEvent } from "react";

import { Button, Space } from "@douyinfe/semi-ui";
import { IconMinus, IconPlus, IconRefresh, IconUndo, IconRedo } from "@douyinfe/semi-icons";
import {
  PlaygroundReactRenderer,
  WorkflowResetLayoutService,
  usePlayground,
  useService,
} from "@flowgram-adapter/free-layout-editor";
import { WorkflowRenderProvider } from "@coze-workflow/render";

import { microflowNodeRegistryByKey, type MicroflowNodeDragPayload, type MicroflowNodeRegistryItem } from "../node-registry";
import type { MicroflowPoint, MicroflowSchema, MicroflowTraceFrame, MicroflowValidationIssue } from "../schema";
import { FlowGramMicroflowContainerModule } from "./FlowGramMicroflowPlugins";
import { FlowGramMicroflowCaseEditor, booleanCaseValue } from "./FlowGramMicroflowCaseEditor";
import type { FlowGramMicroflowPendingLine, FlowGramMicroflowSelection } from "./FlowGramMicroflowTypes";
import { useFlowGramMicroflowBridge } from "./hooks/useFlowGramMicroflowBridge";
import "@flowgram-adapter/free-layout-editor/css-load";
import "./styles/flowgram-microflow-canvas.css";
import "./styles/flowgram-microflow-port.css";
import "./styles/flowgram-microflow-line.css";

export interface FlowGramMicroflowCanvasProps {
  schema: MicroflowSchema;
  validationIssues: MicroflowValidationIssue[];
  runtimeTrace: MicroflowTraceFrame[];
  readonly?: boolean;
  onSchemaChange: (nextSchema: MicroflowSchema, reason: string) => void;
  onSelectionChange: (selection: FlowGramMicroflowSelection) => void;
  onDropRegistryItem?: (item: MicroflowNodeRegistryItem, position: MicroflowPoint) => void;
}

function FlowGramMicroflowToolbar() {
  const playground = usePlayground();
  const resetLayout = useService<WorkflowResetLayoutService>(WorkflowResetLayoutService);
  return (
    <div className="microflow-flowgram-toolbar">
      <Space>
        <Button icon={<IconPlus />} size="small" onClick={() => playground.config.zoomin()} />
        <Button icon={<IconMinus />} size="small" onClick={() => playground.config.zoomout()} />
        <Button icon={<IconRefresh />} size="small" onClick={() => resetLayout.fitView()} />
        <Button icon={<IconUndo />} size="small" disabled />
        <Button icon={<IconRedo />} size="small" disabled />
      </Space>
    </div>
  );
}

function existingBooleanCases(schema: MicroflowSchema, sourceObjectId?: string) {
  const cases = new Set<boolean>();
  for (const flow of schema.flows) {
    if (flow.kind !== "sequence" || flow.originObjectId !== sourceObjectId) {
      continue;
    }
    for (const caseValue of flow.caseValues) {
      if (caseValue.kind === "boolean") {
        cases.add(Boolean(caseValue.value));
      }
    }
  }
  return cases;
}

function readNodeDragPayload(dataTransfer: DataTransfer): MicroflowNodeDragPayload | undefined {
  const raw = dataTransfer.getData("application/x-atlas-microflow-node") || dataTransfer.getData("application/json");
  if (!raw) {
    return undefined;
  }
  try {
    const parsed = JSON.parse(raw) as MicroflowNodeDragPayload;
    return parsed.dragType === "microflow-node" ? parsed : undefined;
  } catch {
    return undefined;
  }
}

function snapPoint(point: MicroflowPoint, gridSize = 16): MicroflowPoint {
  return {
    x: Math.round(point.x / gridSize) * gridSize,
    y: Math.round(point.y / gridSize) * gridSize,
  };
}

function FlowGramMicroflowCanvasInner(props: FlowGramMicroflowCanvasProps) {
  const playground = usePlayground();
  const [pendingBooleanLine, setPendingBooleanLine] = useState<FlowGramMicroflowPendingLine>();
  const bridge = useFlowGramMicroflowBridge({
    schema: props.schema,
    issues: props.validationIssues,
    traceFrames: props.runtimeTrace,
    readonly: props.readonly,
    onSchemaChange: props.onSchemaChange,
    onSelectionChange: props.onSelectionChange,
    onPendingBooleanLine: setPendingBooleanLine,
  });
  const usedCases = useMemo(
    () => existingBooleanCases(props.schema, pendingBooleanLine?.sourceObjectId),
    [pendingBooleanLine?.sourceObjectId, props.schema],
  );
  const options = [
    { value: true, disabled: usedCases.has(true), reason: usedCases.has(true) ? "该分支已存在" : undefined },
    { value: false, disabled: usedCases.has(false), reason: usedCases.has(false) ? "该分支已存在" : undefined },
  ];

  const handleDragOver = (event: DragEvent<HTMLDivElement>) => {
    const payload = readNodeDragPayload(event.dataTransfer);
    if (!payload || props.readonly) {
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = "copy";
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    const payload = readNodeDragPayload(event.dataTransfer);
    if (!payload || props.readonly) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const item = microflowNodeRegistryByKey.get(payload.registryKey);
    if (!item) {
      return;
    }
    const rawPosition = playground.config.getPosFromMouseEvent(event.nativeEvent);
    props.onDropRegistryItem?.(item, snapPoint({ x: rawPosition.x, y: rawPosition.y }));
  };

  return (
    <div className="microflow-flowgram-canvas" onDragOver={handleDragOver} onDrop={handleDrop}>
      <PlaygroundReactRenderer />
      <FlowGramMicroflowToolbar />
      <FlowGramMicroflowCaseEditor
        visible={Boolean(pendingBooleanLine)}
        options={options}
        onCancel={() => setPendingBooleanLine(undefined)}
        onConfirm={value => {
          if (!pendingBooleanLine || usedCases.has(value)) {
            return;
          }
          bridge.createBooleanCaseFlow([booleanCaseValue(value)], value ? "是" : "否", pendingBooleanLine);
          setPendingBooleanLine(undefined);
        }}
      />
    </div>
  );
}

export function FlowGramMicroflowCanvas(props: FlowGramMicroflowCanvasProps) {
  return (
    <WorkflowRenderProvider containerModules={[FlowGramMicroflowContainerModule]}>
      <FlowGramMicroflowCanvasInner {...props} />
    </WorkflowRenderProvider>
  );
}
