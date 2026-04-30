import type { WorkflowEdgeJSON, WorkflowJSON, WorkflowNodeJSON } from "@flowgram-adapter/free-layout-editor";

import {
  defaultMicroflowNodePanelRegistry,
  getMicroflowNodeRegistryKey,
  microflowNodeRegistryByKey,
  objectKindFromRegistryItem,
  officialTypeFromRegistryItem,
  type MicroflowNodeRegistryEntry,
  type MicroflowNodeRegistryItem,
} from "../node-registry";
import type {
  MicroflowAuthoringSchema,
  MicroflowDataType,
  MicroflowDesignSchema,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectBase,
  MicroflowParameter,
  MicroflowPoint,
  MicroflowSize,
  MicroflowVariable,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import { createDefaultEditorState } from "../schema/utils/schema-utils";
import { createStableId } from "../schema/utils/ids";
import { flowGramPortsForObjectKind } from "./adapters/flowgram-port-factory";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData } from "./FlowGramMicroflowTypes";

export const MICROFLOW_DESIGN_SCHEMA_VERSION = "flowgram.microflow.v1";
export const MICROFLOW_ROOT_COLLECTION_ID = "root-collection";

const defaultNodeSize: MicroflowSize = { width: 160, height: 76 };

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function nowIso(): string {
  return new Date().toISOString();
}

function registryEntryForKey(key: string): MicroflowNodeRegistryEntry | undefined {
  return microflowNodeRegistryByKey.get(key)
    ?? defaultMicroflowNodePanelRegistry.find(item => getMicroflowNodeRegistryKey(item) === key);
}

function entryForObjectKind(kind: string): MicroflowNodeRegistryEntry | undefined {
  return defaultMicroflowNodePanelRegistry.find(item => objectKindFromRegistryItem(item) === kind);
}

function nodeSizeForEntry(entry: MicroflowNodeRegistryEntry | undefined): MicroflowSize {
  return {
    width: entry?.render.width ?? entry?.defaultSize?.width ?? defaultNodeSize.width,
    height: entry?.render.height ?? entry?.defaultSize?.height ?? defaultNodeSize.height,
  };
}

function nodeDataForEntry(entry: MicroflowNodeRegistryEntry | undefined, input: {
  id: string;
  objectKind: FlowGramMicroflowNodeData["objectKind"];
  title: string;
  subtitle?: string;
  officialType: string;
}): FlowGramMicroflowNodeData {
  return {
    objectId: input.id,
    objectKind: input.objectKind,
    collectionId: MICROFLOW_ROOT_COLLECTION_ID,
    actionKind: entry?.actionKind,
    title: input.title,
    subtitle: input.subtitle,
    officialType: input.officialType,
    availability: entry?.availability,
    availabilityReason: entry?.availabilityReason,
    disabled: entry?.availability === "requiresConnector" || entry?.availability === "nanoflowOnlyDisabled",
    validationState: "valid",
    runtimeState: "idle",
    issueCount: 0,
  };
}

export function createMicroflowWorkflowNode(input: {
  id: string;
  registryKey?: string;
  objectKind?: FlowGramMicroflowNodeData["objectKind"];
  position: MicroflowPoint;
  title?: string;
  subtitle?: string;
  officialType?: string;
  data?: Partial<FlowGramMicroflowNodeData> & Record<string, unknown>;
}): WorkflowNodeJSON {
  const entry = input.registryKey ? registryEntryForKey(input.registryKey) : undefined;
  const objectKind = input.objectKind ?? (entry ? objectKindFromRegistryItem(entry) : "actionActivity");
  const officialType = input.officialType ?? (entry ? officialTypeFromRegistryItem(entry) : objectKind);
  const title = input.title ?? entry?.titleZh ?? entry?.title ?? objectKind;
  const size = nodeSizeForEntry(entry);
  const data: FlowGramMicroflowNodeData = {
    ...nodeDataForEntry(entry, {
      id: input.id,
      objectKind,
      title,
      subtitle: input.subtitle ?? officialType,
      officialType,
    }),
    ...input.data,
    objectId: input.id,
    objectKind,
    collectionId: input.data?.collectionId ?? MICROFLOW_ROOT_COLLECTION_ID,
  };
  return {
    id: input.id,
    type: objectKind,
    data,
    meta: {
      position: input.position,
      size,
      nodeDTOType: objectKind,
      useDynamicPort: true,
      defaultPorts: flowGramPortsForObjectKind(objectKind),
      collectionId: data.collectionId,
    },
  };
}

export function createMicroflowWorkflowEdge(input: {
  id?: string;
  sourceNodeID: string;
  targetNodeID: string;
  sourcePortID?: string;
  targetPortID?: string;
  data?: Partial<FlowGramMicroflowEdgeData> & Record<string, unknown>;
}): WorkflowEdgeJSON {
  const id = input.id ?? createStableId("flow");
  const data: FlowGramMicroflowEdgeData = {
    flowId: id,
    flowKind: "sequence",
    edgeKind: input.data?.edgeKind ?? "sequence",
    isErrorHandler: false,
    caseValues: [],
    validationState: "valid",
    runtimeState: "idle",
    ...input.data,
  };
  return {
    id,
    sourceNodeID: input.sourceNodeID,
    targetNodeID: input.targetNodeID,
    sourcePortID: input.sourcePortID,
    targetPortID: input.targetPortID,
    data,
  } as WorkflowEdgeJSON;
}

export function createBlankMicroflowWorkflow(): WorkflowJSON {
  return {
    nodes: [
      createMicroflowWorkflowNode({
        id: "start",
        registryKey: "startEvent",
        position: { x: 320, y: 220 },
        title: "Start",
      }),
      createMicroflowWorkflowNode({
        id: "end",
        registryKey: "endEvent",
        position: { x: 620, y: 220 },
        title: "End",
      }),
    ],
    edges: [],
  };
}

export function createMicroflowDesignSchema(input: {
  id: string;
  name: string;
  displayName?: string;
  description?: string;
  moduleId: string;
  moduleName?: string;
  parameters?: MicroflowParameter[];
  returnType?: MicroflowDataType;
  returnVariableName?: string;
  ownerName?: string;
  workflow?: WorkflowJSON;
}): MicroflowDesignSchema {
  const timestamp = nowIso();
  return {
    schemaVersion: MICROFLOW_DESIGN_SCHEMA_VERSION,
    id: input.id,
    stableId: input.id,
    name: input.name,
    displayName: input.displayName || input.name,
    description: input.description,
    moduleId: input.moduleId,
    moduleName: input.moduleName,
    workflow: cloneJson(input.workflow ?? createBlankMicroflowWorkflow()) as MicroflowWorkflowJSON,
    editor: createDefaultEditorState(),
    parameters: input.parameters ?? [],
    returnType: input.returnType ?? { kind: "void" },
    returnVariableName: input.returnVariableName,
    variables: [] satisfies MicroflowVariable[],
    validation: { issues: [] },
    audit: {
      version: "0.1.0",
      status: "draft",
      createdBy: input.ownerName ?? "Current User",
      createdAt: timestamp,
      updatedBy: input.ownerName ?? "Current User",
      updatedAt: timestamp,
    },
  };
}

function nodePosition(node: MicroflowWorkflowNodeJSON): MicroflowPoint {
  const position = node.meta?.position;
  return {
    x: typeof position?.x === "number" ? position.x : 0,
    y: typeof position?.y === "number" ? position.y : 0,
  };
}

function nodeSize(node: MicroflowWorkflowNodeJSON): MicroflowSize {
  const size = node.meta?.size;
  return {
    width: typeof size?.width === "number" ? size.width : defaultNodeSize.width,
    height: typeof size?.height === "number" ? size.height : defaultNodeSize.height,
  };
}

function compileNodeToRuntimeObject(node: MicroflowWorkflowNodeJSON): MicroflowObject {
  const data = node.data as (Partial<FlowGramMicroflowNodeData> & { propertyObject?: MicroflowObject }) | undefined;
  const objectKind = (data?.objectKind ?? node.type) as FlowGramMicroflowNodeData["objectKind"];
  const entry = entryForObjectKind(objectKind);
  const officialType = data?.officialType ?? (entry ? officialTypeFromRegistryItem(entry) : objectKind);
  const base: MicroflowObjectBase = {
    id: node.id,
    stableId: node.id,
    kind: objectKind,
    officialType,
    caption: data?.title ?? String(node.type ?? node.id),
    documentation: data?.documentation ?? "",
    relativeMiddlePoint: nodePosition(node),
    size: nodeSize(node),
    editor: { iconKey: entry?.iconKey ?? objectKind },
  };
  if (data?.propertyObject?.id === node.id) {
    return {
      ...base,
      ...data.propertyObject,
      id: node.id,
      stableId: data.propertyObject.stableId ?? node.id,
      kind: data.propertyObject.kind,
      officialType: data.propertyObject.officialType,
      caption: data?.title ?? data.propertyObject.caption ?? base.caption,
      documentation: data?.documentation ?? data.propertyObject.documentation ?? base.documentation,
      relativeMiddlePoint: nodePosition(node),
      size: nodeSize(node),
    } as MicroflowObject;
  }
  if (objectKind === "startEvent") {
    return { ...base, kind: "startEvent", officialType: "Microflows$StartEvent", trigger: { type: "manual" } };
  }
  if (objectKind === "endEvent") {
    return { ...base, kind: "endEvent", officialType: "Microflows$EndEvent", endBehavior: { type: "normalReturn" } };
  }
  if (objectKind === "annotation") {
    return { ...base, kind: "annotation", officialType: "Microflows$Annotation", text: data?.title ?? base.caption } as MicroflowObject;
  }
  return {
    ...base,
    kind: objectKind,
    officialType,
    disabled: data?.disabled ?? false,
  } as MicroflowObject;
}

function compileEdgeToRuntimeFlow(edge: MicroflowWorkflowEdgeJSON): MicroflowFlow {
  const data = edge.data as (Partial<FlowGramMicroflowEdgeData> & { propertyFlow?: MicroflowFlow }) | undefined;
  const id = data?.flowId ?? edge.id ?? createStableId("flow");
  if (data?.propertyFlow?.id === id) {
    return {
      ...data.propertyFlow,
      id,
      stableId: data.propertyFlow.stableId ?? id,
      originObjectId: edge.sourceNodeID,
      destinationObjectId: edge.targetNodeID,
      caseValues: data.caseValues ?? data.propertyFlow.caseValues ?? [],
      isErrorHandler: data.isErrorHandler ?? data.propertyFlow.isErrorHandler ?? false,
      editor: {
        ...data.propertyFlow.editor,
        edgeKind: data.edgeKind ?? data.propertyFlow.editor.edgeKind,
        label: data.label ?? data.propertyFlow.editor.label,
        description: data.description ?? data.propertyFlow.editor.description,
        branchOrder: data.branchOrder ?? (data.propertyFlow.editor as { branchOrder?: number }).branchOrder,
      },
    } as MicroflowFlow;
  }
  return {
    id,
    stableId: id,
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId: edge.sourceNodeID,
    destinationObjectId: edge.targetNodeID,
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    caseValues: data?.caseValues ?? [],
    isErrorHandler: data?.isErrorHandler ?? false,
    line: {
      kind: "orthogonal",
      points: [],
      routing: { mode: "auto", bendPoints: [] },
      style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
    },
    editor: {
      edgeKind: data?.edgeKind ?? "sequence",
      label: data?.label,
      description: data?.description,
      branchOrder: data?.branchOrder,
    },
  } as MicroflowFlow;
}

export function compileMicroflowDesignToRuntime(schema: MicroflowDesignSchema): MicroflowAuthoringSchema {
  const timestamp = nowIso();
  const workflow = schema.workflow as MicroflowWorkflowJSON;
  return {
    schemaVersion: "1.0.0",
    mendixProfile: "mx10",
    id: schema.id,
    stableId: schema.stableId ?? schema.id,
    name: schema.name,
    displayName: schema.displayName,
    description: schema.description,
    documentation: schema.documentation,
    moduleId: schema.moduleId,
    moduleName: schema.moduleName,
    parameters: schema.parameters,
    returnType: schema.returnType,
    returnVariableName: schema.returnVariableName,
    objectCollection: {
      id: MICROFLOW_ROOT_COLLECTION_ID,
      officialType: "Microflows$MicroflowObjectCollection",
      objects: workflow.nodes.map(compileNodeToRuntimeObject),
    },
    flows: workflow.edges.map(compileEdgeToRuntimeFlow),
    security: { applyEntityAccess: true, allowedModuleRoleIds: [] },
    concurrency: { allowConcurrentExecution: true, errorMicroflowId: null },
    exposure: {
      exportLevel: "module",
      markAsUsed: true,
      asMicroflowAction: { enabled: false },
      asWorkflowAction: { enabled: false },
      url: { enabled: false },
    },
    variables: {
      schemaId: schema.id,
      builtAt: timestamp,
      all: [],
      parameters: {},
      localVariables: {},
      objectOutputs: {},
      listOutputs: {},
      loopVariables: {},
      errorVariables: {},
      systemVariables: {},
    },
    validation: schema.validation,
    editor: schema.editor,
    audit: schema.audit,
    debug: schema.debug,
  };
}

export function workflowNodeCount(workflow: WorkflowJSON | MicroflowWorkflowJSON): number {
  return workflow.nodes?.length ?? 0;
}

export function workflowEdgeCount(workflow: WorkflowJSON | MicroflowWorkflowJSON): number {
  return workflow.edges?.length ?? 0;
}

export function workflowNodeById(workflow: WorkflowJSON | MicroflowWorkflowJSON, nodeId?: string) {
  return (workflow.nodes as MicroflowWorkflowNodeJSON[] | undefined)?.find(node => node.id === nodeId);
}

export function workflowEdgeById(workflow: WorkflowJSON | MicroflowWorkflowJSON, edgeId?: string) {
  return (workflow.edges as MicroflowWorkflowEdgeJSON[] | undefined)?.find(edge => {
    const data = edge.data as Partial<FlowGramMicroflowEdgeData> | undefined;
    return edge.id === edgeId || data?.flowId === edgeId;
  });
}

export function createWorkflowNodeFromPanelItem(
  item: MicroflowNodeRegistryItem,
  position: MicroflowPoint,
  existingNodeIds: Iterable<string>,
): WorkflowNodeJSON {
  const existing = new Set(existingNodeIds);
  const prefix = getMicroflowNodeRegistryKey(item).replace(/[^a-zA-Z0-9]+/g, "-").toLowerCase() || "node";
  let id = createStableId(prefix);
  while (existing.has(id)) {
    id = createStableId(prefix);
  }
  return createMicroflowWorkflowNode({
    id,
    registryKey: getMicroflowNodeRegistryKey(item),
    position,
  });
}
