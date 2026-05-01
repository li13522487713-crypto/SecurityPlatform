import { useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState, type CSSProperties, type KeyboardEvent as ReactKeyboardEvent, type ReactNode, type Ref } from "react";

import { Badge, Button, Empty, Space, Tabs, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconClose, IconDelete, IconPlay, IconRefresh, IconSave, IconSetting, IconTickCircle, IconUndo, IconRedo } from "@douyinfe/semi-icons";

import { MicroflowNodePanel, type MicroflowNodePanelLabels } from "../node-panel";
import { MicroflowPropertyPanel } from "../property-panel";
import type { MicroflowApiClient, SaveMicroflowResponse, TestRunMicroflowResponse, ValidateMicroflowResponse } from "../runtime-adapter";
import { MicroflowTestRunModal } from "../debug/MicroflowTestRunModal";
import type { MicroflowRunSession, MicroflowTestRunInput, MicroflowTestRunSample, MicroflowTraceFrame } from "../debug/trace-types";
import { readStoredTestRunSamples, writeStoredTestRunSamples } from "../debug/test-run-samples-store";
import type { MicroflowValidationAdapterLike, MicroflowValidationMode } from "../performance";
import type { MicroflowMetadataAdapter, MicroflowMetadataCatalog } from "../metadata";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData, FlowGramMicroflowSelection } from "../flowgram/FlowGramMicroflowTypes";
import {
  FlowGramMicroflowNativeCanvas,
} from "../flowgram/FlowGramMicroflowNativeCanvas";
import {
  createMicroflowWorkflowEdge,
  createMicroflowWorkflowNode,
  createWorkflowNodeFromPanelItem,
  workflowEdgeById,
  workflowEdgeCount,
  workflowNodeById,
  workflowNodeCount,
} from "../flowgram/flowgram-native-schema";
import type {
  MicroflowDesignSchema,
  MicroflowValidationIssue,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import { createDefaultEditorState } from "../schema/utils/schema-utils";
import type {
  MicroflowEditorHandle,
  MicroflowEditorLabels,
  MicroflowEditorStatusSnapshot,
  MicroflowWorkbenchBottomTab,
  MicroflowWorkbenchLayoutState,
  MicroflowWorkbenchStatus,
} from "./index";

const { Text, Title } = Typography;

const RAIL_WIDTH_PX = 44;
const LEFT_PANEL_EXPANDED_PX = 300;
const RIGHT_PANEL_EXPANDED_PX = 360;
const BOTTOM_STRIP_HEIGHT_PX = 32;
const BOTTOM_DOCK_PEEK_HEIGHT_PX = 260;

type BottomDockMode = "collapsed" | "peek" | "full";
type NativeHistoryReason = "init" | "workflow" | "property" | "selection" | "layout" | "delete" | "runtime";
type NativeContextMenuState = {
  x: number;
  y: number;
  selection: FlowGramMicroflowSelection;
};

export interface NativeMicroflowEditorProps {
  schema: MicroflowDesignSchema;
  apiClient?: MicroflowApiClient;
  labels?: Partial<MicroflowEditorLabels>;
  toolbarPrefix?: ReactNode;
  toolbarSuffix?: ReactNode;
  nodePanelLabels?: Partial<MicroflowNodePanelLabels>;
  readonly?: boolean;
  onPublish?: (schema: MicroflowDesignSchema) => Promise<void> | void;
  onSaveComplete?: (response: SaveMicroflowResponse) => void;
  onValidateComplete?: (response: ValidateMicroflowResponse) => void;
  onValidationStateChange?: (state: { microflowId: string; issues: MicroflowValidationIssue[]; status: string; lastValidatedAt?: Date }) => void;
  onTestRunComplete?: (response: TestRunMicroflowResponse) => void;
  onSchemaChange?: (schema: MicroflowDesignSchema) => void;
  metadataAdapter?: MicroflowMetadataAdapter;
  metadataCatalog?: MicroflowMetadataCatalog;
  metadataWorkspaceId?: string;
  metadataModuleId?: string;
  validationAdapter?: MicroflowValidationAdapterLike;
  toolbarMode?: "internal" | "external";
  editorRef?: Ref<MicroflowEditorHandle>;
  shellMode?: "legacy-host-layout" | "editor-native-layout";
  onLayoutStateChange?: (state: MicroflowWorkbenchLayoutState) => void;
  onWorkbenchStatusChange?: (status: MicroflowWorkbenchStatus) => void;
}

const defaultLabels: Partial<MicroflowEditorLabels> = {
  save: "保存",
  validate: "校验",
  testRun: "运行",
  publish: "发布",
  undo: "撤销",
  redo: "重做",
  nodePanel: "Nodes",
  properties: "Properties",
  problems: "Problems",
  debug: "Debug",
  quickFix: "Quick fix",
  quickFixUnavailable: "Quick fix is not available for this issue.",
  missingDecisionBranchCreated: "Missing Decision branch created.",
  format: "Auto",
};

function cloneSchema(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const cloned = JSON.parse(JSON.stringify(schema)) as MicroflowDesignSchema;
  const defaultEditor = createDefaultEditorState();
  return {
    ...cloned,
    workflow: {
      ...(cloned.workflow ?? {}),
      nodes: Array.isArray(cloned.workflow?.nodes) ? cloned.workflow.nodes : [],
      edges: Array.isArray(cloned.workflow?.edges) ? cloned.workflow.edges : [],
    },
    editor: {
      ...defaultEditor,
      ...(cloned.editor ?? {}),
      viewport: {
        ...defaultEditor.viewport,
        ...(cloned.editor?.viewport ?? {}),
      },
      selection: {
        ...(cloned.editor?.selection ?? {}),
      },
    },
    parameters: Array.isArray(cloned.parameters) ? cloned.parameters : [],
    variables: Array.isArray(cloned.variables) ? cloned.variables : [],
    validation: cloned.validation ?? { issues: [] },
  };
}

function isEditableElement(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && Boolean(target.closest("input, textarea, select, [contenteditable='true']"));
}

function schemaWorkflowSignature(schema: MicroflowDesignSchema): string {
  return JSON.stringify(schema.workflow);
}

function issue(id: string, message: string, code: string, severity: MicroflowValidationIssue["severity"] = "error", extra: Partial<MicroflowValidationIssue> = {}): MicroflowValidationIssue {
  return {
    id,
    severity,
    message,
    code,
    source: "schema",
    blockSave: severity === "error",
    blockPublish: severity === "error",
    createdAt: new Date().toISOString(),
    ...extra,
  };
}

function validateNativeSchema(schema: MicroflowDesignSchema, mode: MicroflowValidationMode = "edit"): MicroflowValidationIssue[] {
  const nodes = schema.workflow.nodes as MicroflowWorkflowNodeJSON[];
  const edges = schema.workflow.edges as MicroflowWorkflowEdgeJSON[];
  const issues: MicroflowValidationIssue[] = [];
  const starts = nodes.filter(node => ((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type) === "startEvent");
  const ends = nodes.filter(node => ((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type) === "endEvent");
  if (starts.length !== 1) {
    issues.push(issue(`${schema.id}:start-count`, "微流必须且只能有一个 Start 节点。", "MF_START_COUNT"));
  }
  if (ends.length < 1) {
    issues.push(issue(`${schema.id}:end-missing`, "微流至少需要一个 End 节点。", "MF_END_MISSING"));
  }
  const nodeIds = new Set(nodes.map(node => node.id));
  const edgeKeys = new Set<string>();
  for (const edge of edges) {
    const edgeKey = [edge.sourceNodeID, edge.sourcePortID ?? "", edge.targetNodeID, edge.targetPortID ?? ""].join("::");
    if (edgeKeys.has(edgeKey)) {
      issues.push(issue(`${schema.id}:duplicate-edge:${edgeKey}`, "不允许重复连线。", "MF_EDGE_DUPLICATED", "error", { flowId: edge.id }));
    }
    edgeKeys.add(edgeKey);
    if (!nodeIds.has(edge.sourceNodeID) || !nodeIds.has(edge.targetNodeID)) {
      issues.push(issue(`${schema.id}:edge-missing-node:${edge.id}`, "连线引用了不存在的节点。", "MF_EDGE_NODE_MISSING", "error", { flowId: edge.id }));
    }
    if (edge.sourceNodeID === edge.targetNodeID) {
      issues.push(issue(`${schema.id}:edge-self:${edge.id}`, "不允许节点自连。", "MF_EDGE_SELF_LOOP", "error", { flowId: edge.id }));
    }
  }
  if (mode !== "edit" && starts[0] && edges.every(edge => edge.sourceNodeID !== starts[0].id)) {
    issues.push(issue(`${schema.id}:start-no-outgoing`, "Start 节点需要至少一条出线。", "MF_START_NO_OUTGOING", "error", { objectId: starts[0].id }));
  }
  return issues;
}

function buildAcceptance120Schema(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const intType = { kind: "integer" as const };
  const intListType = { kind: "list" as const, itemType: intType };
  const action = (id: string, kind: string, config: Record<string, unknown>) => ({
    id: `action-${id}`,
    kind,
    officialType: `Microflows$${kind}`,
    ...config,
  });
  const node = (
    id: string,
    objectKind: FlowGramMicroflowNodeData["objectKind"],
    title: string,
    x: number,
    y: number,
    data: Record<string, unknown> = {},
  ): MicroflowWorkflowNodeJSON => createMicroflowWorkflowNode({
    id,
    objectKind,
    position: { x, y },
    title,
    officialType: `Microflows$${objectKind}`,
    data: {
      title,
      ...data,
    },
  }) as MicroflowWorkflowNodeJSON;
  const actionNode = (id: string, title: string, x: number, y: number, actionKind: string, config: Record<string, unknown>) =>
    node(id, "actionActivity", title, x, y, {
      actionKind,
      action: action(id, actionKind, config),
    });
  const loopBody = "loop-numbers-body";
  const loopChild = (id: string, objectKind: FlowGramMicroflowNodeData["objectKind"], title: string, x: number, y: number, data: Record<string, unknown> = {}) =>
    node(id, objectKind, title, x, y, { collectionId: loopBody, parentObjectId: "loop-numbers", ...data });
  const loopAction = (id: string, title: string, x: number, y: number, actionKind: string, config: Record<string, unknown>) =>
    loopChild(id, "actionActivity", title, x, y, {
      actionKind,
      action: action(id, actionKind, config),
    });
  const edge = (id: string, source: string, target: string, data: Record<string, unknown> = {}) => {
    const created = createMicroflowWorkflowEdge({
      id,
      sourceNodeID: source,
      targetNodeID: target,
      data,
    }) as MicroflowWorkflowEdgeJSON & Record<string, unknown>;
    if (typeof data.edgeKind === "string") {
      created.edgeKind = data.edgeKind;
    }
    if (Array.isArray(data.caseValues)) {
      created.caseValues = data.caseValues;
    }
    if (typeof data.collectionId === "string") {
      created.collectionId = data.collectionId;
    }
    return created as MicroflowWorkflowEdgeJSON;
  };
  const loopEdge = (id: string, source: string, target: string, data: Record<string, unknown> = {}) =>
    edge(id, source, target, { collectionId: loopBody, ...data });
  const boolCase = (value: boolean) => [{ kind: "boolean", value, persistedValue: value ? "true" : "false" }];
  const exprCase = (expression: string) => [{ kind: "expression", condition: expression, expression }];
  const objectCase = (value: string) => [{ kind: "objectType", value, entityQualifiedName: value, persistedValue: value }];
  const nodes: MicroflowWorkflowNodeJSON[] = [
    node("start", "startEvent", "Start", 80, 240),
    actionNode("create-total", "创建变量", 280, 240, "createVariable", { variableName: "total", dataType: intType, initialValue: "0" }),
    actionNode("create-list-score", "创建变量", 480, 240, "createVariable", { variableName: "listScore", dataType: intType, initialValue: "0" }),
    actionNode("create-loop-score", "创建变量", 680, 240, "createVariable", { variableName: "loopScore", dataType: intType, initialValue: "0" }),
    actionNode("create-object-score", "创建变量", 880, 240, "createVariable", { variableName: "objectScore", dataType: intType, initialValue: "0" }),
    actionNode("create-gateway-score", "创建变量", 1080, 240, "createVariable", { variableName: "gatewayScore", dataType: intType, initialValue: "0" }),
    actionNode("create-list", "创建列表", 1280, 240, "createList", { outputListVariableName: "workList", elementType: intType, items: [] }),
    actionNode("change-list", "修改列表", 1480, 240, "changeList", { targetListVariableName: "workList", operation: "addRange", items: [6, 1, 3, 2, 5, 4] }),
    actionNode("sort-list", "排序列表", 1680, 240, "sortList", { sourceListVariableName: "workList", outputVariableName: "sortedNumbers", direction: "asc" }),
    actionNode("filter-list", "过滤列表", 1880, 240, "filterList", { sourceListVariableName: "sortedNumbers", outputVariableName: "positiveNumbers", itemVariableName: "item", conditionExpression: "$item > 2", itemType: intType }),
    actionNode("aggregate-list", "列表聚合", 2080, 240, "aggregateList", { sourceListVariableName: "positiveNumbers", aggregateFunction: "sum", outputVariableName: "filteredSum", resultType: intType }),
    actionNode("list-operation", "列表操作", 2280, 240, "listOperation", { leftListVariableName: "positiveNumbers", operation: "contains", itemExpression: "5", outputVariableName: "hasFive" }),
    node("decision", "exclusiveSplit", "决策", 2480, 240, { splitCondition: { expression: "$hasFive = true", resultType: "boolean" } }),
    actionNode("set-list-score", "决策 True", 2580, 160, "changeVariable", { targetVariableName: "listScore", newValueExpression: "$filteredSum" }),
    actionNode("fallback-list-score", "决策 False", 2580, 320, "changeVariable", { targetVariableName: "listScore", newValueExpression: "-99" }),
    node("merge", "exclusiveMerge", "合并", 2680, 240),
    node("loop-numbers", "loopedActivity", "循环", 2880, 240, { bodyCollectionId: loopBody, loopSource: { kind: "iterableList", listVariableName: "numbers", iteratorVariableName: "currentNumber", iteratorVariableDataType: intType } }),
    loopChild("break-check", "exclusiveSplit", "currentNumber = 4", 2880, 380, { splitCondition: { expression: "$currentNumber = 4", resultType: "boolean" } }),
    loopChild("break-event", "breakEvent", "中断事件", 3080, 520),
    loopChild("continue-check", "exclusiveSplit", "currentNumber = 2", 3080, 380, { splitCondition: { expression: "$currentNumber = 2", resultType: "boolean" } }),
    loopChild("continue-event", "continueEvent", "继续事件", 3280, 520),
    loopAction("loop-touch", "修改变量", 2880, 520, "changeVariable", { targetVariableName: "loopScore", newValueExpression: "$loopScore + $currentNumber" }),
    actionNode("create-object", "创建对象", 3080, 240, "createObject", {
      entityQualifiedName: "Sales.Student",
      entityType: "Sales.Student",
      objectId: "student-1",
      outputVariableName: "student",
      memberChanges: [{ memberQualifiedName: "Sales.Student.Grade", valueExpression: "'A'" }],
      value: { id: "student-1", entityType: "Sales.Student", grade: "A" },
    }),
    actionNode("change-object", "修改对象", 3280, 240, "changeMembers", {
      changeVariableName: "student",
      entityQualifiedName: "Sales.Student",
      entityType: "Sales.Student",
      objectId: "student-1",
      memberChanges: [{ memberQualifiedName: "Sales.Student.Grade", valueExpression: "'B'" }],
      value: { id: "student-1", entityType: "Sales.Student", grade: "B" },
    }),
    actionNode("cast-object", "转换对象", 3480, 240, "cast", { sourceVariable: "student", targetEntity: "Sales.Member", outputVariable: "member" }),
    node("object-type", "inheritanceSplit", "对象类型决策", 3680, 240, { inputObjectVariableName: "member", generalizedEntityQualifiedName: "Sales.Member" }),
    actionNode("object-type-student-touch", "对象类型 Student", 3780, 160, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore + 30" }),
    actionNode("object-type-fallback-touch", "对象类型 Fallback", 3780, 320, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore - 99" }),
    node("object-type-merge", "exclusiveMerge", "合并", 3880, 240),
    actionNode("commit-object", "提交对象", 4080, 240, "commit", { objectOrListVariableName: "student", entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "student-1" }),
    actionNode("retrieve-object", "检索对象", 4280, 240, "retrieve", { outputVariableName: "students", retrieveSource: { kind: "database", entityQualifiedName: "Sales.Student", range: { kind: "list" } }, entityType: "Sales.Student", limit: 10 }),
    actionNode("retrieve-score", "修改变量", 4480, 240, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore + 20" }),
    actionNode("create-temp", "创建对象", 4680, 240, "createObject", { entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "temp-rollback", outputVariableName: "tempStudent", value: { id: "temp-rollback", entityType: "Sales.Student" } }),
    actionNode("rollback-object", "回滚对象", 4880, 240, "rollback", { objectOrListVariableName: "tempStudent", entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "temp-rollback" }),
    actionNode("rollback-score", "修改变量", 5080, 240, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore + 10" }),
    actionNode("delete-object", "删除对象", 5280, 240, "delete", { objectOrListVariableName: "student", entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "student-1" }),
    actionNode("delete-score", "修改变量", 5480, 240, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore + 8" }),
    node("parallel-fork", "parallelGateway", "并行网关", 5680, 240),
    actionNode("parallel-a", "并行分支 A", 5880, 160, "createVariable", { variableName: "parallelA", dataType: intType, initialValue: "7" }),
    actionNode("parallel-b", "并行分支 B", 5880, 320, "createVariable", { variableName: "parallelB", dataType: intType, initialValue: "11" }),
    node("parallel-join", "parallelGateway", "并行合并", 6080, 240),
    actionNode("parallel-score", "修改变量", 6280, 240, "changeVariable", { targetVariableName: "gatewayScore", newValueExpression: "$gatewayScore + $parallelA + $parallelB" }),
    node("inclusive-fork", "inclusiveGateway", "包含网关", 6480, 240),
    actionNode("inclusive-a", "包含分支 A", 6680, 160, "createVariable", { variableName: "inclusiveA", dataType: intType, initialValue: "5" }),
    actionNode("inclusive-b", "包含分支 B", 6680, 320, "createVariable", { variableName: "inclusiveB", dataType: intType, initialValue: "7" }),
    node("inclusive-join", "inclusiveGateway", "包含合并", 6880, 240),
    actionNode("inclusive-score", "修改变量", 7080, 240, "changeVariable", { targetVariableName: "gatewayScore", newValueExpression: "$gatewayScore + $inclusiveA + $inclusiveB" }),
    actionNode("final-total", "修改变量", 7280, 240, "changeVariable", { targetVariableName: "total", newValueExpression: "$listScore + $loopScore + $objectScore + $gatewayScore" }),
    node("end", "endEvent", "End", 7480, 240, { returnValue: { raw: "$total" } }),
  ];
  const edges: MicroflowWorkflowEdgeJSON[] = [
    edge("f-start-total", "start", "create-total"),
    edge("f-total-list-score", "create-total", "create-list-score"),
    edge("f-list-loop-score", "create-list-score", "create-loop-score"),
    edge("f-loop-object-score", "create-loop-score", "create-object-score"),
    edge("f-object-gateway-score", "create-object-score", "create-gateway-score"),
    edge("f-gateway-create-list", "create-gateway-score", "create-list"),
    edge("f-create-change-list", "create-list", "change-list"),
    edge("f-change-sort", "change-list", "sort-list"),
    edge("f-sort-filter", "sort-list", "filter-list"),
    edge("f-filter-aggregate", "filter-list", "aggregate-list"),
    edge("f-aggregate-operation", "aggregate-list", "list-operation"),
    edge("f-operation-decision", "list-operation", "decision"),
    edge("f-decision-true", "decision", "set-list-score", { edgeKind: "decisionCondition", caseValues: boolCase(true) }),
    edge("f-decision-false", "decision", "fallback-list-score", { edgeKind: "decisionCondition", caseValues: boolCase(false) }),
    edge("f-decision-true-merge", "set-list-score", "merge"),
    edge("f-decision-false-merge", "fallback-list-score", "merge"),
    edge("f-merge-loop", "merge", "loop-numbers"),
    edge("f-loop-create-object", "loop-numbers", "create-object"),
    edge("f-loop-body-break-decision", "loop-numbers", "break-check", { edgeKind: "loopBody" }),
    loopEdge("f-break-true", "break-check", "break-event", { edgeKind: "decisionCondition", caseValues: boolCase(true) }),
    loopEdge("f-break-false", "break-check", "continue-check", { edgeKind: "decisionCondition", caseValues: boolCase(false) }),
    loopEdge("f-continue-true", "continue-check", "continue-event", { edgeKind: "decisionCondition", caseValues: boolCase(true) }),
    loopEdge("f-continue-false", "continue-check", "loop-touch", { edgeKind: "decisionCondition", caseValues: boolCase(false) }),
    edge("f-create-change-object", "create-object", "change-object"),
    edge("f-change-cast", "change-object", "cast-object"),
    edge("f-cast-object-type", "cast-object", "object-type"),
    edge("f-object-student", "object-type", "object-type-student-touch", { edgeKind: "objectTypeCondition", caseValues: objectCase("Sales.Student") }),
    edge("f-object-fallback", "object-type", "object-type-fallback-touch", { edgeKind: "objectTypeCondition", caseValues: objectCase("fallback") }),
    edge("f-object-student-merge", "object-type-student-touch", "object-type-merge"),
    edge("f-object-fallback-merge", "object-type-fallback-touch", "object-type-merge"),
    edge("f-object-merge-commit", "object-type-merge", "commit-object"),
    edge("f-commit-retrieve", "commit-object", "retrieve-object"),
    edge("f-retrieve-score", "retrieve-object", "retrieve-score"),
    edge("f-retrieve-create-temp", "retrieve-score", "create-temp"),
    edge("f-create-temp-rollback", "create-temp", "rollback-object"),
    edge("f-rollback-score", "rollback-object", "rollback-score"),
    edge("f-rollback-delete", "rollback-score", "delete-object"),
    edge("f-delete-score", "delete-object", "delete-score"),
    edge("f-delete-parallel", "delete-score", "parallel-fork"),
    edge("f-parallel-a", "parallel-fork", "parallel-a"),
    edge("f-parallel-b", "parallel-fork", "parallel-b"),
    edge("f-parallel-a-join", "parallel-a", "parallel-join"),
    edge("f-parallel-b-join", "parallel-b", "parallel-join"),
    edge("f-parallel-score", "parallel-join", "parallel-score"),
    edge("f-parallel-inclusive", "parallel-score", "inclusive-fork"),
    edge("f-inclusive-a", "inclusive-fork", "inclusive-a", { edgeKind: "sequence", caseValues: exprCase("$hasFive = true") }),
    edge("f-inclusive-b", "inclusive-fork", "inclusive-b", { edgeKind: "sequence", caseValues: exprCase("$listScore = 18") }),
    edge("f-inclusive-a-join", "inclusive-a", "inclusive-join"),
    edge("f-inclusive-b-join", "inclusive-b", "inclusive-join"),
    edge("f-inclusive-score", "inclusive-join", "inclusive-score"),
    edge("f-inclusive-final", "inclusive-score", "final-total"),
    edge("f-final-end", "final-total", "end"),
  ];
  return {
    ...schema,
    displayName: schema.displayName === "MF_AllNodeComplexComputation_Test" && schema.name !== "MF_AllNodeComplexComputation_Test"
      ? schema.name
      : schema.displayName,
    documentation: "All screenshot node families acceptance fixture. Expected output: 120.",
    workflow: { nodes, edges },
    parameters: [{ id: "numbers", stableId: "numbers", name: "numbers", dataType: intListType, type: { kind: "list", name: "List<Integer>", itemType: { kind: "primitive", name: "Integer" } }, required: true }],
    returnType: intType,
    returnVariableName: "total",
    editor: { ...schema.editor, viewport: { x: 0, y: 0, zoom: 0.35 }, selection: {} },
    audit: { ...schema.audit, updatedAt: new Date().toISOString() },
  };
}

function summarizeIssues(issues: MicroflowValidationIssue[]) {
  return {
    errorCount: issues.filter(item => item.severity === "error").length,
    warningCount: issues.filter(item => item.severity === "warning").length,
    infoCount: issues.filter(item => item.severity === "info").length,
  };
}

function selectionPatch(schema: MicroflowDesignSchema, selection: FlowGramMicroflowSelection): MicroflowDesignSchema {
  return {
    ...schema,
    editor: {
      ...schema.editor,
      selectedObjectId: selection.objectId,
      selectedFlowId: selection.flowId,
      selectedCollectionId: selection.collectionId,
      selection: {
        objectId: selection.objectId,
        flowId: selection.flowId,
        collectionId: selection.collectionId,
        objectIds: selection.objectIds,
        flowIds: selection.flowIds,
        mode: selection.mode,
      },
    },
  };
}

function isProtectedWorkflowNode(node: MicroflowWorkflowNodeJSON): boolean {
  const kind = (node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type;
  return node.id === "start" || node.id === "end" || kind === "startEvent" || kind === "endEvent";
}

function selectionHasOnlyProtectedObjects(schema: MicroflowDesignSchema, selection: FlowGramMicroflowSelection): boolean {
  const selectedObjects = [selection.objectId, ...(selection.objectIds ?? [])].filter((id): id is string => Boolean(id));
  if (!selectedObjects.length) {
    return false;
  }
  return selectedObjects.every(objectId => {
    const node = workflowNodeById(schema.workflow, objectId) as MicroflowWorkflowNodeJSON | undefined;
    return node ? isProtectedWorkflowNode(node) : false;
  });
}

function deleteSelection(schema: MicroflowDesignSchema, targetSelection?: FlowGramMicroflowSelection): MicroflowDesignSchema {
  const selection = targetSelection ?? schema.editor.selection;
  const selectedObjects = new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id)));
  const selectedFlows = new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id)));
  const protectedObjects = new Set(
    (schema.workflow.nodes as MicroflowWorkflowNodeJSON[])
      .filter(node => selectedObjects.has(node.id) && isProtectedWorkflowNode(node))
      .map(node => node.id),
  );
  const deletedParameterIds = new Set(
    (schema.workflow.nodes as MicroflowWorkflowNodeJSON[])
      .filter(node => selectedObjects.has(node.id) && !protectedObjects.has(node.id))
      .flatMap(node => {
        const data = node.data as (Partial<FlowGramMicroflowNodeData> & {
          parameterId?: string;
        }) | undefined;
        const parameterId = data?.parameterId;
        return data?.objectKind === "parameterObject"
          ? [parameterId].filter((id): id is string => Boolean(id))
          : [];
      }),
  );
  return {
    ...schema,
    parameters: deletedParameterIds.size > 0
      ? schema.parameters.filter(parameter => !deletedParameterIds.has(parameter.id))
      : schema.parameters,
    workflow: {
      ...schema.workflow,
      nodes: (schema.workflow.nodes as MicroflowWorkflowNodeJSON[]).filter(node => !selectedObjects.has(node.id) || protectedObjects.has(node.id)),
      edges: (schema.workflow.edges as MicroflowWorkflowEdgeJSON[]).filter(edge => {
        const id = (edge.data as Partial<FlowGramMicroflowEdgeData> | undefined)?.flowId ?? edge.id;
        return !selectedFlows.has(String(id))
          && (!selectedObjects.has(edge.sourceNodeID) || protectedObjects.has(edge.sourceNodeID))
          && (!selectedObjects.has(edge.targetNodeID) || protectedObjects.has(edge.targetNodeID));
      }),
    },
    editor: {
      ...schema.editor,
      selectedObjectId: undefined,
      selectedFlowId: undefined,
      selection: {},
    },
  };
}

function applyAutoLayout(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const nodes = schema.workflow.nodes as MicroflowWorkflowNodeJSON[];
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes: nodes.map((node, index) => ({
        ...node,
        meta: {
          ...node.meta,
          position: {
            x: 260 + index * 260,
            y: 220 + (index % 2) * 120,
          },
        },
      })),
    },
  };
}

function panelStyle(): CSSProperties {
  return {
    minWidth: 0,
    minHeight: 0,
    overflow: "auto",
    padding: 12,
    borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
    background: "var(--semi-color-bg-2, #fff)",
  };
}

function propertyPaneStyle(): CSSProperties {
  return {
    width: RIGHT_PANEL_EXPANDED_PX,
    minWidth: 0,
    minHeight: 0,
    overflow: "auto",
    padding: 12,
    borderLeft: "1px solid var(--semi-color-border, #e5e6eb)",
    background: "var(--semi-color-bg-1, #fff)",
  };
}

function contextMenuPosition(point: { x: number; y: number }): { left: number; top: number } {
  const menuWidth = 168;
  const menuHeight = 128;
  const margin = 8;
  if (typeof window === "undefined") {
    return { left: point.x, top: point.y };
  }
  return {
    left: Math.max(margin, Math.min(point.x, window.innerWidth - menuWidth - margin)),
    top: Math.max(margin, Math.min(point.y, window.innerHeight - menuHeight - margin)),
  };
}

export function NativeMicroflowEditor(props: NativeMicroflowEditorProps) {
  const labels = { ...defaultLabels, ...props.labels };
  const [schema, setSchema] = useState<MicroflowDesignSchema>(() => cloneSchema(props.schema));
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(() => validateNativeSchema(props.schema));
  const [validationStatus, setValidationStatus] = useState("idle");
  const [lastValidatedAt, setLastValidatedAt] = useState<Date>();
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [testRunModalOpen, setTestRunModalOpen] = useState(false);
  const [testRunValues, setTestRunValues] = useState<Record<string, unknown>>();
  const [testRunSamples, setTestRunSamples] = useState<MicroflowTestRunSample[]>(() => readStoredTestRunSamples()[props.schema.id] ?? []);
  const [lastRunSession, setLastRunSession] = useState<MicroflowRunSession>();
  const [runtimeServiceError, setRuntimeServiceError] = useState<string>();
  const [leftOpen, setLeftOpen] = useState(true);
  const [rightOpen, setRightOpen] = useState(true);
  const [bottomDockMode, setBottomDockMode] = useState<BottomDockMode>("collapsed");
  const [bottomTab, setBottomTab] = useState<MicroflowWorkbenchBottomTab>("problems");
  const [focusObjectId, setFocusObjectId] = useState<string>();
  const [focusRequestSeq, setFocusRequestSeq] = useState(0);
  const [contextMenu, setContextMenu] = useState<NativeContextMenuState>();
  const [historyPast, setHistoryPast] = useState<MicroflowDesignSchema[]>([]);
  const [historyFuture, setHistoryFuture] = useState<MicroflowDesignSchema[]>([]);
  const shellRef = useRef<HTMLDivElement>(null);
  const contextMenuRef = useRef<HTMLDivElement>(null);
  const latestSchemaRef = useRef(schema);
  const savedSchemaSignatureRef = useRef(schemaWorkflowSignature(schema));
  const toolbarMode = props.toolbarMode ?? "internal";
  const shellMode = props.shellMode ?? (toolbarMode === "external" ? "editor-native-layout" : "legacy-host-layout");
  const externalLayout = shellMode === "editor-native-layout";
  const bottomOpen = bottomDockMode !== "collapsed";

  useEffect(() => {
    const next = cloneSchema(props.schema);
    setSchema(next);
    latestSchemaRef.current = next;
    savedSchemaSignatureRef.current = schemaWorkflowSignature(next);
    setDirty(false);
    setHistoryPast([]);
    setHistoryFuture([]);
    setIssues(validateNativeSchema(next));
    setTestRunSamples(readStoredTestRunSamples()[next.id] ?? []);
  }, [props.schema]);

  useEffect(() => {
    const stored = readStoredTestRunSamples();
    writeStoredTestRunSamples({ ...stored, [schema.id]: testRunSamples });
  }, [schema.id, testRunSamples]);

  const commitSchema = useCallback((next: MicroflowDesignSchema, reason: NativeHistoryReason, options: { pushHistory?: boolean; dirty?: boolean } = {}) => {
    const shouldPushHistory = options.pushHistory !== false && reason !== "selection" && reason !== "runtime";
    const normalized = cloneSchema(next);
    latestSchemaRef.current = normalized;
    setSchema(current => {
      if (shouldPushHistory) {
        setHistoryPast(past => [...past, cloneSchema(current)].slice(-100));
        setHistoryFuture([]);
      }
      return normalized;
    });
    const nextDirty = options.dirty ?? (reason !== "selection" && reason !== "runtime");
    if (nextDirty) {
      setDirty(true);
      props.onSchemaChange?.(normalized);
    }
    const nextIssues = validateNativeSchema(normalized);
    setIssues(nextIssues);
  }, [props]);

  const handleUndo = useCallback(() => {
    setHistoryPast(past => {
      const previous = past.at(-1);
      if (!previous) {
        return past;
      }
      setHistoryFuture(future => [cloneSchema(latestSchemaRef.current), ...future].slice(0, 100));
      const next = cloneSchema(previous);
      latestSchemaRef.current = next;
      setSchema(next);
      setDirty(schemaWorkflowSignature(next) !== savedSchemaSignatureRef.current);
      props.onSchemaChange?.(next);
      return past.slice(0, -1);
    });
  }, [props]);

  const handleRedo = useCallback(() => {
    setHistoryFuture(future => {
      const nextFuture = future[0];
      if (!nextFuture) {
        return future;
      }
      setHistoryPast(past => [...past, cloneSchema(latestSchemaRef.current)].slice(-100));
      const next = cloneSchema(nextFuture);
      latestSchemaRef.current = next;
      setSchema(next);
      setDirty(schemaWorkflowSignature(next) !== savedSchemaSignatureRef.current);
      props.onSchemaChange?.(next);
      return future.slice(1);
    });
  }, [props]);

  const runValidation = useCallback(async (mode: MicroflowValidationMode = "edit") => {
    setValidationStatus("validating");
    try {
      const localIssues = validateNativeSchema(latestSchemaRef.current, mode);
      setIssues(localIssues);
      const result: ValidateMicroflowResponse = {
        valid: !localIssues.some(item => item.severity === "error"),
        issues: localIssues,
      };
      props.onValidateComplete?.(result);
      props.onValidationStateChange?.({
        microflowId: latestSchemaRef.current.id,
        issues: localIssues,
        status: result.valid ? "valid" : "invalid",
        lastValidatedAt: new Date(),
      });
      setValidationStatus(result.valid ? "valid" : "invalid");
      setLastValidatedAt(new Date());
      return { issues: localIssues, summary: summarizeIssues(localIssues) };
    } catch (error) {
      const validationIssue = issue(`${latestSchemaRef.current.id}:validation-error`, error instanceof Error ? error.message : String(error), "MF_VALIDATION_SERVICE_ERROR");
      setIssues([validationIssue]);
      setValidationStatus("invalid");
      return { issues: [validationIssue], summary: summarizeIssues([validationIssue]) };
    }
  }, [props]);

  const handleSave = useCallback(async (): Promise<boolean> => {
    if (props.readonly || saving) {
      return false;
    }
    setSaving(true);
    try {
      const validation = await runValidation("save");
      const blockers = validation.issues.filter(item => item.blockSave && item.severity === "error");
      if (blockers.length > 0 || validation.summary.errorCount > 0) {
        setBottomDockMode("peek");
        setBottomTab("problems");
        Toast.error(`保存被 ${blockers.length || validation.summary.errorCount} 个校验错误阻止。`);
        return false;
      }
      const response = await props.apiClient?.saveMicroflow({ schema: latestSchemaRef.current });
      savedSchemaSignatureRef.current = schemaWorkflowSignature(latestSchemaRef.current);
      setDirty(false);
      if (response) {
        props.onSaveComplete?.(response);
      }
      Toast.success("保存成功");
      return true;
    } finally {
      setSaving(false);
    }
  }, [props, runValidation, saving]);

  const handleTestRun = useCallback(async () => {
    if (saving || running || props.readonly || !latestSchemaRef.current.id) {
      return;
    }
    const saved = await handleSave();
    if (!saved) {
      return;
    }
    const validation = await runValidation("testRun");
    if (validation.summary.errorCount > 0) {
      setBottomDockMode("peek");
      setBottomTab("problems");
      Toast.warning("存在校验错误，无法运行。");
      return;
    }
    setRuntimeServiceError(undefined);
    setTestRunModalOpen(true);
  }, [handleSave, props.readonly, runValidation, running, saving]);

  const executeTestRun = useCallback(async (input: MicroflowTestRunInput) => {
    if (!props.apiClient) {
      Toast.warning("当前未配置运行适配器。");
      return;
    }
    setRunning(true);
    setRuntimeServiceError(undefined);
    try {
      if (dirty) {
        const saved = await handleSave();
        if (!saved) {
          return;
        }
      }
      const response = await props.apiClient.testRunMicroflow({
        microflowId: latestSchemaRef.current.id,
        input: input.parameters,
        schema: latestSchemaRef.current,
        options: input.options,
      });
      setLastRunSession(response.session);
      if (input.sampleId) {
        setTestRunSamples(current => current.map(sample => sample.id === input.sampleId
          ? {
            ...sample,
            previousResult: sample.lastResult,
            lastResult: response.session.output,
            lastStatus: response.session.status,
            lastRunId: response.session.id,
            lastRunAt: response.session.endedAt ?? response.session.startedAt,
            updatedAt: new Date().toISOString(),
          }
          : sample));
      }
      setBottomDockMode("peek");
      setBottomTab("debug");
      props.onTestRunComplete?.(response);
      Toast[response.status === "succeeded" ? "success" : "error"](`Run ${response.status}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setRuntimeServiceError(message);
      setBottomDockMode("peek");
      setBottomTab("debug");
      Toast.error(message);
    } finally {
      setRunning(false);
    }
  }, [dirty, handleSave, props]);

  const saveTestRunSample = useCallback((sample: Omit<MicroflowTestRunSample, "id" | "updatedAt"> & { id?: string }) => {
    const id = sample.id ?? `sample-${Date.now()}`;
    const now = new Date().toISOString();
    setTestRunSamples(current => [{
      id,
      name: sample.name,
      parameters: sample.parameters,
      expectedResult: sample.expectedResult,
      lastResult: sample.lastResult,
      lastStatus: sample.lastStatus,
      lastRunId: sample.lastRunId,
      lastRunAt: sample.lastRunAt,
      previousResult: sample.previousResult,
      updatedAt: now,
    }, ...current.filter(item => item.id !== id)].slice(0, 20));
  }, []);

  const runAllTestRunSamples = useCallback(async (samples: MicroflowTestRunSample[], options?: MicroflowTestRunInput["options"]) => {
    for (const sample of samples) {
      await executeTestRun({ parameters: sample.parameters, options, sampleId: sample.id });
    }
  }, [executeTestRun]);

  const handlePublish = useCallback(async () => {
    if (!props.onPublish) {
      Toast.warning("Publish handler is not configured.");
      return;
    }
    await props.onPublish(latestSchemaRef.current);
  }, [props]);

  const handleAddNode = useCallback((item: Parameters<NonNullable<React.ComponentProps<typeof MicroflowNodePanel>["onAddNode"]>>[0], options?: { position?: { x: number; y: number } }) => {
    const position = options?.position ?? { x: 360 + schema.workflow.nodes.length * 80, y: 240 };
    const node = createWorkflowNodeFromPanelItem(item, position, schema.workflow.nodes.map(existing => existing.id));
    const next = selectionPatch({
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [...schema.workflow.nodes, node] as MicroflowWorkflowJSON["nodes"],
      },
    }, { objectId: node.id, flowId: undefined, collectionId: "root-collection", objectIds: [node.id], flowIds: [], mode: "single" });
    commitSchema(next, "workflow");
    setRightOpen(true);
  }, [commitSchema, schema]);

  const clearSelection = useCallback(() => {
    setContextMenu(undefined);
    commitSchema(selectionPatch(schema, { mode: "none", objectIds: [], flowIds: [] }), "selection", { pushHistory: false, dirty: false });
  }, [commitSchema, schema]);

  const handleDeleteSelection = useCallback((targetSelection?: FlowGramMicroflowSelection) => {
    if (props.readonly) {
      return;
    }
    const sourceSchema = latestSchemaRef.current;
    const selection = targetSelection ?? sourceSchema.editor.selection;
    const hasSelection = Boolean(selection.objectId || selection.flowId || selection.objectIds?.length || selection.flowIds?.length);
    if (!hasSelection) {
      return;
    }
    setContextMenu(undefined);
    commitSchema(deleteSelection(sourceSchema, selection), "delete");
  }, [commitSchema, props.readonly]);

  const handleAutoLayout = useCallback(() => {
    commitSchema(applyAutoLayout(schema), "layout");
    Toast.success("Auto layout applied.");
  }, [commitSchema, schema]);

  const handleEditorShortcut = useCallback((event: {
    key: string;
    target: EventTarget | null;
    ctrlKey: boolean;
    metaKey: boolean;
    shiftKey: boolean;
    defaultPrevented?: boolean;
    preventDefault: () => void;
  }) => {
    if (event.defaultPrevented || isEditableElement(event.target)) {
      return;
    }
    const key = event.key.toLowerCase();
    if ((event.ctrlKey || event.metaKey) && key === "z" && !event.shiftKey) {
      event.preventDefault();
      handleUndo();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && (key === "y" || (key === "z" && event.shiftKey))) {
      event.preventDefault();
      handleRedo();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "s") {
      event.preventDefault();
      void handleSave();
      return;
    }
    if ((key === "delete" || key === "backspace") && !props.readonly) {
      event.preventDefault();
      handleDeleteSelection();
      return;
    }
    if (key === "escape") {
      event.preventDefault();
      clearSelection();
    }
  }, [clearSelection, handleDeleteSelection, handleRedo, handleSave, handleUndo, props.readonly]);

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    handleEditorShortcut(event);
  };

  useEffect(() => {
    const handleDocumentKeyDown = (event: globalThis.KeyboardEvent) => {
      const shell = shellRef.current;
      if (!shell) {
        return;
      }
      const targetElement = event.target instanceof HTMLElement ? event.target : null;
      const activeElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      const activeInEditor = targetElement ? shell.contains(targetElement) : activeElement ? shell.contains(activeElement) : false;
      if (!activeInEditor) {
        return;
      }
      handleEditorShortcut(event);
    };
    document.addEventListener("keydown", handleDocumentKeyDown, true);
    return () => document.removeEventListener("keydown", handleDocumentKeyDown, true);
  }, [handleEditorShortcut]);

  const layoutState = useMemo<MicroflowWorkbenchLayoutState>(() => ({
    shellMode,
    activeBottomTab: bottomTab,
    bottomDockMode,
    focusMode: false,
    minimapVisible: schema.editor.showMiniMap === true,
    gridVisible: schema.editor.gridEnabled !== false,
  }), [bottomDockMode, bottomTab, schema.editor.gridEnabled, schema.editor.showMiniMap, shellMode]);

  const workbenchStatus = useMemo<MicroflowWorkbenchStatus>(() => ({
    microflowId: schema.id,
    schemaVersion: schema.schemaVersion,
    dirty,
    saving,
    running,
    validationStatus,
    errorCount: issues.filter(item => item.severity === "error").length,
    warningCount: issues.filter(item => item.severity === "warning").length,
    canUndo: historyPast.length > 0,
    canRedo: historyFuture.length > 0,
    zoomPercent: Math.round((schema.editor.viewport?.zoom ?? 1) * 100),
    hasRunSession: false,
    fullscreen: false,
    activeBottomTab: bottomTab,
    bottomDockMode,
    sessionHydrated: false,
    traceHydrated: false,
    debugSessionHydrated: false,
    degradedRunSession: false,
    layout: layoutState,
  }), [bottomDockMode, bottomTab, dirty, historyFuture.length, historyPast.length, issues, layoutState, running, saving, schema.editor.viewport?.zoom, schema.id, schema.schemaVersion, validationStatus]);

  useEffect(() => {
    props.onLayoutStateChange?.(layoutState);
  }, [layoutState, props]);

  useEffect(() => {
    props.onWorkbenchStatusChange?.(workbenchStatus);
  }, [props, workbenchStatus]);

  useEffect(() => {
    if (!contextMenu) {
      return undefined;
    }
    const close = (event: MouseEvent) => {
      const target = event.target instanceof Node ? event.target : null;
      if (target && contextMenuRef.current?.contains(target)) {
        return;
      }
      setContextMenu(undefined);
    };
    const closeOnScroll = () => setContextMenu(undefined);
    window.addEventListener("mousedown", close);
    window.addEventListener("scroll", closeOnScroll, true);
    return () => {
      window.removeEventListener("mousedown", close);
      window.removeEventListener("scroll", closeOnScroll, true);
    };
  }, [contextMenu]);

  useImperativeHandle(props.editorRef, () => ({
    save: async () => {
      await handleSave();
    },
    validate: async () => {
      await runValidation("save");
    },
    runTest: async () => {
      await handleTestRun();
    },
    runDebug: async () => {
      setBottomDockMode("peek");
      setBottomTab("debug");
      await handleTestRun();
    },
    publish: handlePublish,
    undo: handleUndo,
    redo: handleRedo,
    autoLayout: handleAutoLayout,
    fitView: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-fit-view")),
    zoomIn: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { delta: 0.1 } })),
    zoomOut: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { delta: -0.1 } })),
    setZoom: (zoom: number) => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { zoom } })),
    toggleFullscreen: () => undefined,
    toggleFocusMode: () => undefined,
    toggleMinimap: () => {
      commitSchema({ ...schema, editor: { ...schema.editor, showMiniMap: !schema.editor.showMiniMap } }, "layout");
    },
    resetLayout: () => undefined,
    getStatus: () => workbenchStatus as MicroflowEditorStatusSnapshot,
    openBottomTab: (tab: MicroflowWorkbenchBottomTab) => {
      setBottomTab(tab);
      setBottomDockMode("peek");
    },
    setBottomDockMode,
    getLayoutState: () => layoutState,
    configureAllNodeAcceptance120: async () => {
      const next = buildAcceptance120Schema(latestSchemaRef.current);
      commitSchema(next, "workflow");
      setRightOpen(false);
      setLeftOpen(false);
      const saved = await handleSave();
      Toast[saved ? "success" : "warning"](saved ? "已配置并保存全节点验收计算图，期望输出 120。" : "已配置全节点验收计算图，但保存未完成，请查看 Problems。");
    },
  }), [commitSchema, handleAutoLayout, handlePublish, handleRedo, handleSave, handleTestRun, handleUndo, layoutState, runValidation, schema, workbenchStatus]);

  const shellStyle: CSSProperties = {
    display: "grid",
    gridTemplateRows: [
      toolbarMode === "internal" ? "60px" : undefined,
      "minmax(0, 1fr)",
      bottomOpen ? `${BOTTOM_DOCK_PEEK_HEIGHT_PX}px` : undefined,
      `${BOTTOM_STRIP_HEIGHT_PX}px`,
    ].filter(Boolean).join(" "),
    height: "100%",
    minHeight: 0,
    position: "relative",
    background: "var(--semi-color-bg-0, #f7f8fa)",
  };
  const bodyStyle: CSSProperties = {
    display: "grid",
    gridTemplateColumns: `${RAIL_WIDTH_PX}px minmax(0, 1fr) ${RAIL_WIDTH_PX}px`,
    minHeight: 0,
    minWidth: 0,
    overflow: "hidden",
    position: "relative",
  };
  const leftDockStyle: CSSProperties = {
    position: "absolute",
    top: 0,
    bottom: 0,
    left: RAIL_WIDTH_PX,
    zIndex: 34,
    width: LEFT_PANEL_EXPANDED_PX,
    boxShadow: "8px 0 18px rgba(31, 35, 41, 0.08)",
  };
  const rightDockStyle: CSSProperties = {
    position: "absolute",
    top: 0,
    right: RAIL_WIDTH_PX,
    bottom: 0,
    zIndex: 34,
    boxShadow: "-8px 0 18px rgba(31, 35, 41, 0.08)",
  };
  const selectedNode = workflowNodeById(schema.workflow, schema.editor.selection.objectId) as MicroflowWorkflowNodeJSON | undefined;
  const selectedFlow = workflowEdgeById(schema.workflow, schema.editor.selection.flowId) as MicroflowWorkflowEdgeJSON | undefined;

  return (
    <div ref={shellRef} data-testid="microflow-editor-shell" data-microflow-id={schema.id} style={shellStyle} tabIndex={0} onKeyDown={handleKeyDown}>
      {toolbarMode === "internal" ? (
        <div data-testid="microflow-editor-toolbar" style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, padding: "8px 12px", borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
          <Space style={{ minWidth: 0, overflow: "hidden" }}>
            {props.toolbarPrefix}
            <Title heading={5} style={{ margin: 0 }}>{schema.displayName || schema.name}</Title>
            <Tag>{schema.schemaVersion}</Tag>
            {dirty ? <Tag color="orange">dirty</Tag> : <Tag color="green">saved</Tag>}
            <Tag color={issues.some(item => item.severity === "error") ? "red" : "green"}>{issues.length} issues</Tag>
          </Space>
          <Space>
            <Tooltip content={labels.undo}><Button icon={<IconUndo />} disabled={historyPast.length === 0} onClick={handleUndo} /></Tooltip>
            <Tooltip content={labels.redo}><Button icon={<IconRedo />} disabled={historyFuture.length === 0} onClick={handleRedo} /></Tooltip>
            <Button data-testid="microflow-editor-validate" icon={<IconRefresh />} loading={validationStatus === "validating"} onClick={() => void runValidation("save")}>{labels.validate}</Button>
            <Button data-testid="microflow-editor-run" icon={<IconPlay />} loading={running} disabled={saving || props.readonly} onClick={() => void handleTestRun()}>{labels.testRun}</Button>
            <Button data-testid="microflow-editor-save" icon={<IconSave />} type="primary" loading={saving} disabled={saving || props.readonly || !dirty} onClick={() => void handleSave()}>{labels.save}</Button>
            {props.toolbarSuffix}
          </Space>
        </div>
      ) : null}
      <div style={bodyStyle}>
        <button
          type="button"
          data-testid="microflow-node-panel-rail"
          aria-label={leftOpen ? "节点面板已展开" : "展开节点面板"}
          aria-expanded={leftOpen}
          style={{
            border: 0,
            borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
            background: leftOpen ? "var(--semi-color-primary-light-default, #e8f3ff)" : "var(--semi-color-bg-2, #fff)",
            color: leftOpen ? "var(--semi-color-primary, #165dff)" : "inherit",
            cursor: "pointer",
          }}
          onClick={() => setLeftOpen(true)}
        >
          <Text size="small" strong style={{ writingMode: "vertical-rl", textOrientation: "mixed" }}>{labels.nodePanel}</Text>
        </button>
        {leftOpen ? (
          <div data-testid="microflow-editor-left-panel" style={{ ...panelStyle(), ...leftDockStyle }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 8 }}>
              <Text strong>{labels.nodePanel}</Text>
              <Button aria-label="折叠节点面板" size="small" theme="borderless" icon={<IconClose />} onClick={() => setLeftOpen(false)} />
            </div>
            <MicroflowNodePanel
              favoriteNodeKeys={[]}
              onFavoriteChange={() => undefined}
              onAddNode={(item, options) => handleAddNode(item, { position: options?.position })}
              onShowDocumentation={item => {
                Toast.info(item.documentation.summary);
              }}
              labels={props.nodePanelLabels}
              createContext={{ microflowId: schema.id, moduleId: schema.moduleId, metadataAvailable: Boolean(props.metadataCatalog), schemaLoaded: true, readonly: props.readonly }}
            />
          </div>
        ) : null}
        <div data-testid="microflow-canvas" style={{ minWidth: 0, minHeight: 0, display: "contents" }}>
          <FlowGramMicroflowNativeCanvas
            schema={schema}
            validationIssues={issues}
            runtimeTrace={[] as MicroflowTraceFrame[]}
            focusObjectId={focusObjectId}
            focusRequestKey={focusRequestSeq}
            readonly={props.readonly}
            onSchemaChange={(nextSchema, reason) => {
              commitSchema(nextSchema, reason === "flowgramNodeMove" ? "workflow" : "workflow");
            }}
            onSelectionChange={selection => {
              commitSchema(selectionPatch(latestSchemaRef.current, selection), "selection", { pushHistory: false, dirty: false });
              if (selection.objectId || selection.flowId) {
                setRightOpen(true);
              }
            }}
            onCanvasBlankClick={clearSelection}
            onNodeContextMenu={(selection, point) => {
              commitSchema(selectionPatch(latestSchemaRef.current, selection), "selection", { pushHistory: false, dirty: false });
              setContextMenu({ x: point.x, y: point.y, selection });
              setRightOpen(true);
            }}
            onDropRegistryItem={(item, position) => handleAddNode(item, { position })}
            canUndo={historyPast.length > 0}
            canRedo={historyFuture.length > 0}
            onUndo={handleUndo}
            onRedo={handleRedo}
            onAutoLayout={handleAutoLayout}
            onViewportChange={(viewport, options) => {
              commitSchema({ ...schema, editor: { ...schema.editor, viewport, zoom: viewport.zoom } }, "selection", { pushHistory: false, dirty: options?.skipDirty === true ? false : true });
            }}
            onToggleMiniMap={visible => commitSchema({ ...schema, editor: { ...schema.editor, showMiniMap: visible } }, "layout")}
            onToggleGrid={enabled => commitSchema({ ...schema, editor: { ...schema.editor, gridEnabled: enabled } }, "layout")}
            dirty={dirty}
            saving={saving}
            validating={validationStatus === "validating"}
            onOpenProblemsPanel={() => {
              setBottomDockMode("peek");
              setBottomTab("problems");
            }}
          />
        </div>
        <button
          type="button"
          data-testid="microflow-property-panel-rail"
          aria-label={rightOpen ? "属性面板已展开" : "展开属性面板"}
          aria-expanded={rightOpen}
          style={{
            border: 0,
            borderLeft: "1px solid var(--semi-color-border, #e5e6eb)",
            background: rightOpen ? "var(--semi-color-primary-light-default, #e8f3ff)" : "var(--semi-color-bg-2, #fff)",
            color: rightOpen ? "var(--semi-color-primary, #165dff)" : "inherit",
            cursor: "pointer",
          }}
          onClick={() => setRightOpen(true)}
        >
          <IconSetting />
          <Text size="small" strong style={{ writingMode: "vertical-rl", textOrientation: "mixed" }}>{labels.properties}</Text>
        </button>
        {rightOpen ? (
          <div data-testid="microflow-property-panel" style={{ ...propertyPaneStyle(), ...rightDockStyle }}>
            <MicroflowPropertyPanel
              schemaProtocol="design"
              schema={schema}
              validationIssues={issues}
              traceFrames={[] as MicroflowTraceFrame[]}
              readonly={props.readonly}
              onSchemaChange={(next, reason) => commitSchema(next, reason === "deleteNode" || reason === "deleteFlow" ? "delete" : "property")}
              onLocateObject={objectId => {
                setFocusObjectId(objectId);
                setFocusRequestSeq(value => value + 1);
              }}
              onClose={() => setRightOpen(false)}
            />
          </div>
        ) : null}
        {contextMenu ? (
          <div
            ref={contextMenuRef}
            data-testid="microflow-canvas-context-menu"
            data-context-object-id={contextMenu.selection.objectId ?? ""}
            data-context-flow-id={contextMenu.selection.flowId ?? ""}
            data-context-object-ids={(contextMenu.selection.objectIds ?? []).join(",")}
            data-context-flow-ids={(contextMenu.selection.flowIds ?? []).join(",")}
            role="menu"
            style={{
              position: "fixed",
              ...contextMenuPosition(contextMenu),
              zIndex: 1000,
              minWidth: 168,
              padding: 6,
              display: "grid",
              gap: 4,
              border: "1px solid var(--semi-color-border, #e5e6eb)",
              borderRadius: 6,
              boxShadow: "0 8px 24px rgba(31, 35, 41, 0.14)",
              background: "var(--semi-color-bg-2, #fff)",
            }}
            onPointerDownCapture={event => {
              const target = event.target;
              if (!(target instanceof HTMLElement) || !target.closest("[data-menu-action='delete']")) {
                return;
              }
              event.preventDefault();
              event.stopPropagation();
              handleDeleteSelection(contextMenu.selection);
            }}
            onMouseDown={event => event.stopPropagation()}
            onClick={event => event.stopPropagation()}
          >
            <Button
              role="menuitem"
              theme="borderless"
              style={{ justifyContent: "flex-start" }}
              onClick={() => {
                commitSchema(selectionPatch(schema, contextMenu.selection), "selection", { pushHistory: false, dirty: false });
                setRightOpen(true);
                setContextMenu(undefined);
              }}
            >
              属性
            </Button>
            {contextMenu.selection.objectId ? (
              <Button
                role="menuitem"
                theme="borderless"
                style={{ justifyContent: "flex-start" }}
                onClick={() => {
                  setFocusObjectId(contextMenu.selection.objectId);
                  setFocusRequestSeq(value => value + 1);
                  setContextMenu(undefined);
                }}
              >
                定位
              </Button>
            ) : null}
            <button
              type="button"
              role="menuitem"
              data-testid="microflow-canvas-context-menu-delete"
              data-menu-action="delete"
              disabled={props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection)}
              style={{
                minHeight: 32,
                display: "flex",
                alignItems: "center",
                gap: 8,
                justifyContent: "flex-start",
                border: 0,
                borderRadius: 4,
                padding: "6px 12px",
                color: "var(--semi-color-danger, #f93920)",
                background: "transparent",
                cursor: props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection) ? "not-allowed" : "pointer",
                opacity: props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection) ? 0.45 : 1,
                font: "inherit",
              }}
              onPointerDown={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
              onMouseDown={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
              onClick={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
            >
              <IconDelete />
              删除
            </button>
          </div>
        ) : null}
      </div>
      {bottomOpen ? (
        <div
          data-testid="microflow-bottom-panel"
          style={{
            minHeight: 0,
            borderTop: "1px solid var(--semi-color-border, #e5e6eb)",
            background: "var(--semi-color-bg-1, #fff)",
            overflow: "hidden",
            padding: "8px 12px 12px",
          }}
        >
          <Tabs type="line" activeKey={bottomTab} onChange={key => setBottomTab(key as MicroflowWorkbenchBottomTab)}>
            <Tabs.TabPane tab={<Space><IconTickCircle />{labels.problems}<Badge count={issues.length} /></Space>} itemKey="problems">
              <Space vertical align="start" style={{ width: "100%", maxHeight: 200, overflow: "auto" }}>
                {issues.length === 0 ? <Empty title="No problems" /> : issues.map(item => (
                  <Button
                    key={item.id}
                    theme="borderless"
                    type={item.severity === "error" ? "danger" : "tertiary"}
                    onClick={() => {
                      if (item.objectId) {
                        setFocusObjectId(item.objectId);
                        setFocusRequestSeq(value => value + 1);
                      }
                    }}
                  >
                    {item.code}: {item.message}
                  </Button>
                ))}
              </Space>
            </Tabs.TabPane>
            <Tabs.TabPane tab={labels.debug} itemKey="debug">
              <Empty title="Debug trace is generated by the runtime adapter" description="当前原生设计态会在运行前单向编译为 runtime schema。" />
            </Tabs.TabPane>
          </Tabs>
        </div>
      ) : null}
      <div style={{ height: BOTTOM_STRIP_HEIGHT_PX, display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 12px", borderTop: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
        <Space>
          <Tag color={issues.some(item => item.severity === "error") ? "red" : "green"}>{issues.length} issues</Tag>
          <Tag>{workflowNodeCount(schema.workflow)} nodes</Tag>
          <Tag>{workflowEdgeCount(schema.workflow)} edges</Tag>
          {lastValidatedAt ? <Text size="small" type="tertiary">{lastValidatedAt.toLocaleTimeString()}</Text> : null}
        </Space>
        <Space>
          {selectedNode ? <Tag color="blue">Node {selectedNode.id}</Tag> : null}
          {selectedFlow ? <Tag color="blue">Flow {(selectedFlow.data as Partial<FlowGramMicroflowEdgeData> | undefined)?.flowId ?? selectedFlow.id}</Tag> : null}
          <Button size="small" theme="borderless" icon={<IconDelete />} disabled={props.readonly || !(selectedNode || selectedFlow)} onClick={() => handleDeleteSelection()} />
          <Button size="small" theme="borderless" onClick={() => setBottomDockMode(mode => mode === "collapsed" ? "peek" : "collapsed")}>{bottomOpen ? "Hide" : "Problems"}</Button>
        </Space>
      </div>
      <MicroflowTestRunModal
        visible={testRunModalOpen}
        schema={schema}
        running={running}
        dirty={dirty}
        validationErrorCount={issues.filter(item => item.severity === "error").length}
        values={testRunValues}
        lastSession={lastRunSession}
        serviceError={runtimeServiceError}
        samples={testRunSamples}
        onCancel={() => setTestRunModalOpen(false)}
        onValuesChange={setTestRunValues}
        onSaveSample={saveTestRunSample}
        onRunAllSamples={runAllTestRunSamples}
        onRun={executeTestRun}
      />
    </div>
  );
}
