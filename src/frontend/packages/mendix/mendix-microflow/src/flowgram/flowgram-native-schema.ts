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
import { microflowActionRegistryByKind } from "../node-registry/action-registry";
import type {
  MicroflowAction,
  MicroflowDataType,
  MicroflowDesignSchema,
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
import { forceOrthogonalLineKind } from "./FlowGramMicroflowTypes";
import { getMendixMicroflowNodeSize } from "./flowgram-node-geometry";

export const MICROFLOW_DESIGN_SCHEMA_VERSION = "flowgram.microflow.v1";
export const MICROFLOW_ROOT_COLLECTION_ID = "root-collection";

const defaultNodeSize: MicroflowSize = getMendixMicroflowNodeSize("actionActivity");

type LoopParentingNode = MicroflowWorkflowNodeJSON & {
  blocks?: LoopParentingNode[];
  parentNode?: string;
  extent?: string;
};

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function nodePosition(node: MicroflowWorkflowNodeJSON): MicroflowPoint {
  return {
    x: Number(node.meta?.position?.x ?? 0),
    y: Number(node.meta?.position?.y ?? 0),
  };
}

function nodeObjectKind(node: MicroflowWorkflowNodeJSON): string {
  return String((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type ?? "");
}

function nodeParentObjectId(node: MicroflowWorkflowNodeJSON): string | undefined {
  const data = node.data as Partial<FlowGramMicroflowNodeData> | undefined;
  const parentObjectId = data?.parentObjectId ?? (node.meta?.parentObjectId as string | undefined);
  return typeof parentObjectId === "string" && parentObjectId.length > 0 ? parentObjectId : undefined;
}

function cloneWorkflowNodeWithoutBlocks(node: LoopParentingNode): MicroflowWorkflowNodeJSON {
  const { blocks: _blocks, parentNode: _parentNode, extent: _extent, ...rest } = node;
  return cloneJson(rest) as MicroflowWorkflowNodeJSON;
}

function cloneWorkflowNodeForFlowGramRender(node: LoopParentingNode): LoopParentingNode {
  const { blocks: _blocks, ...rest } = node;
  return cloneJson(rest) as LoopParentingNode;
}

function workflowHasBlocks(nodes: readonly LoopParentingNode[]): boolean {
  return nodes.some(node => Boolean(node.blocks?.length) || workflowHasBlocks(node.blocks ?? []));
}

function withAbsolutePosition(
  node: MicroflowWorkflowNodeJSON,
  position: MicroflowPoint,
  parentObjectId?: string,
): MicroflowWorkflowNodeJSON {
  const data = (node.data ?? {}) as Record<string, unknown>;
  const meta = (node.meta ?? {}) as NonNullable<MicroflowWorkflowNodeJSON["meta"]>;
  const nextData = { ...data };
  const nextMeta = { ...meta, position };
  if (parentObjectId) {
    nextData.parentObjectId = parentObjectId;
    nextMeta.parentObjectId = parentObjectId;
  } else {
    delete nextData.parentObjectId;
    delete nextMeta.parentObjectId;
  }
  return {
    ...node,
    data: nextData,
    meta: nextMeta,
  };
}

/**
 * FlowGram free-layout stores child node coordinates relative to their parent
 * container. Atlas persists microflow coordinates in the root canvas space.
 */
export function flattenFlowGramWorkflowForPersistence(workflow: WorkflowJSON | MicroflowWorkflowJSON): WorkflowJSON {
  const flattenedNodes: MicroflowWorkflowNodeJSON[] = [];
  const inputNodes = (workflow.nodes ?? []) as LoopParentingNode[];
  const treeParentingPresent = workflowHasBlocks(inputNodes);
  const visit = (
    node: LoopParentingNode,
    parentObjectId: string | undefined,
    parentPosition: MicroflowPoint,
  ) => {
    const localPosition = nodePosition(node);
    const absolutePosition = parentObjectId
      ? { x: parentPosition.x + localPosition.x, y: parentPosition.y + localPosition.y }
      : localPosition;
    const cleanNode = cloneWorkflowNodeWithoutBlocks(node);
    const resolvedParentObjectId = parentObjectId ?? (treeParentingPresent ? undefined : nodeParentObjectId(cleanNode));
    flattenedNodes.push(withAbsolutePosition(cleanNode, absolutePosition, resolvedParentObjectId));
    for (const child of node.blocks ?? []) {
      visit(child, String(node.id), absolutePosition);
    }
  };

  for (const node of inputNodes) {
    visit(node, undefined, { x: 0, y: 0 });
  }

  return {
    ...workflow,
    nodes: flattenedNodes as WorkflowJSON["nodes"],
    edges: cloneJson((workflow.edges ?? []) as WorkflowJSON["edges"]) as WorkflowJSON["edges"],
  };
}

function withRelativePositionForParent(
  node: MicroflowWorkflowNodeJSON,
  parent: MicroflowWorkflowNodeJSON,
): LoopParentingNode {
  const childPosition = nodePosition(node);
  const parentPosition = nodePosition(parent);
  const data = (node.data ?? {}) as Record<string, unknown>;
  const meta = (node.meta ?? {}) as NonNullable<MicroflowWorkflowNodeJSON["meta"]>;
  return {
    ...cloneJson(node),
    parentNode: parent.id,
    extent: "parent",
    data: {
      ...data,
      parentObjectId: parent.id,
    },
    meta: {
      ...meta,
      parentObjectId: parent.id,
      position: {
        x: childPosition.x - parentPosition.x,
        y: childPosition.y - parentPosition.y,
      },
    },
  };
}

export function nestLoopChildrenForFlowGram(workflow: WorkflowJSON | MicroflowWorkflowJSON): WorkflowJSON {
  const flattened = flattenFlowGramWorkflowForPersistence(workflow);
  const nodes = cloneJson((flattened.nodes ?? []) as MicroflowWorkflowNodeJSON[]);
  const nodeById = new Map(nodes.map(node => [String(node.id), node]));
  const childrenByParentId = new Map<string, MicroflowWorkflowNodeJSON[]>();

  for (const node of nodes) {
    const parentObjectId = nodeParentObjectId(node);
    const parent = parentObjectId ? nodeById.get(parentObjectId) : undefined;
    if (!parent || nodeObjectKind(parent) !== "loopedActivity") {
      continue;
    }
    const list = childrenByParentId.get(parent.id) ?? [];
    list.push(node);
    childrenByParentId.set(parent.id, list);
  }

  const build = (
    node: MicroflowWorkflowNodeJSON,
    absoluteNode: MicroflowWorkflowNodeJSON = node,
  ): LoopParentingNode => {
    const children = childrenByParentId.get(String(absoluteNode.id)) ?? [];
    const cleanNode = cloneWorkflowNodeForFlowGramRender(node as LoopParentingNode);
    if (!children.length) {
      return cleanNode;
    }
    return {
      ...cleanNode,
      blocks: children.map(child => build(withRelativePositionForParent(child, absoluteNode), child)),
    };
  };

  const rootNodes = nodes.filter(node => {
    const parentObjectId = nodeParentObjectId(node);
    return !parentObjectId || !childrenByParentId.has(parentObjectId) || !nodeById.has(parentObjectId);
  });

  return {
    ...flattened,
    nodes: rootNodes.map(node => build(node)) as WorkflowJSON["nodes"],
  };
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
  return getMendixMicroflowNodeSize(entry ? objectKindFromRegistryItem(entry) : undefined);
}

function createDefaultActionForEntry(entry: MicroflowNodeRegistryEntry | undefined, objectId: string): MicroflowAction | undefined {
  if (!entry?.actionKind) {
    return undefined;
  }
  const actionEntry = microflowActionRegistryByKind.get(entry.actionKind);
  if (!actionEntry) {
    return undefined;
  }
  return actionEntry.createAction({
    id: `action-${objectId}`,
    config: actionEntry.createDefaultConfig(),
    caption: actionEntry.defaultCaption,
  });
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
    action: createDefaultActionForEntry(entry, input.id),
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
  const actionKind = entry?.actionKind ?? input.data?.actionKind;
  const action = input.data?.action ?? createDefaultActionForEntry(entry, input.id);
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
    actionKind,
    action,
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
      defaultPorts: flowGramPortsForObjectKind(objectKind, actionKind),
      collectionId: data.collectionId,
      parentObjectId: data.parentObjectId,
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
    lineKind: forceOrthogonalLineKind(input.data?.lineKind),
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
        position: { x: 360, y: 160 },
        title: "Start",
      }),
      createMicroflowWorkflowNode({
        id: "end",
        registryKey: "endEvent",
        position: { x: 360, y: 420 },
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

export function workflowNodeCount(workflow: WorkflowJSON | MicroflowWorkflowJSON): number {
  return workflow.nodes?.length ?? 0;
}

export function workflowEdgeCount(workflow: WorkflowJSON | MicroflowWorkflowJSON): number {
  return workflow.edges?.length ?? 0;
}

export function workflowNodeById(workflow: WorkflowJSON | MicroflowWorkflowJSON, nodeId?: string) {
  return (flattenFlowGramWorkflowForPersistence(workflow).nodes as MicroflowWorkflowNodeJSON[] | undefined)?.find(node => node.id === nodeId);
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
