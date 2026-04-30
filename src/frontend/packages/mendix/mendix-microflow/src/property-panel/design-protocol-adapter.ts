import type { WorkflowNodeJSON } from "@flowgram-adapter/free-layout-editor";

import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData } from "../flowgram/FlowGramMicroflowTypes";
import {
  MICROFLOW_ROOT_COLLECTION_ID,
  compileMicroflowDesignToRuntime,
  createMicroflowWorkflowNode,
  workflowEdgeById,
  workflowNodeById,
} from "../flowgram/flowgram-native-schema";
import {
  createActionActivityFromActionRegistry,
  createObjectFromNodeRegistry,
  defaultMicroflowNodePanelRegistry,
  getMicroflowNodeRegistryKey,
  objectKindFromRegistryItem,
} from "../node-registry";
import type {
  MicroflowActionActivity,
  MicroflowAuthoringSchema,
  MicroflowDesignSchema,
  MicroflowFlow,
  MicroflowObject,
  MicroflowPoint,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema";
import { createStableId } from "../schema/utils/ids";
import type { MicroflowEdgePatch, MicroflowNodePatch } from "./types";

type NodeDataWithPropertyObject = Partial<FlowGramMicroflowNodeData> & {
  propertyObject?: MicroflowObject;
  config?: Record<string, unknown>;
  editor?: Record<string, unknown>;
};

type EdgeDataWithPropertyFlow = Partial<FlowGramMicroflowEdgeData> & {
  propertyFlow?: MicroflowFlow;
};

export interface DesignPropertyPanelModel {
  authoringSchema: MicroflowAuthoringSchema;
  selectedObject: MicroflowObject | null;
  selectedFlow: MicroflowFlow | null;
}

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function nodePosition(node: MicroflowWorkflowNodeJSON): MicroflowPoint {
  return {
    x: Number(node.meta?.position?.x ?? 0),
    y: Number(node.meta?.position?.y ?? 0),
  };
}

function nodeSize(node: MicroflowWorkflowNodeJSON) {
  return node.meta?.size ?? { width: 160, height: 76 };
}

function findRegistryKeyForNode(node: MicroflowWorkflowNodeJSON): string | undefined {
  const data = node.data as NodeDataWithPropertyObject | undefined;
  const objectKind = data?.objectKind ?? node.type;
  const actionKind = data?.actionKind;
  const entry = defaultMicroflowNodePanelRegistry.find(item => {
    if (actionKind && item.actionKind === actionKind) {
      return true;
    }
    return objectKindFromRegistryItem(item) === objectKind;
  });
  return entry ? getMicroflowNodeRegistryKey(entry) : undefined;
}

function defaultObjectForNode(node: MicroflowWorkflowNodeJSON, fallback?: MicroflowObject): MicroflowObject {
  const data = node.data as NodeDataWithPropertyObject | undefined;
  const position = nodePosition(node);
  if ((data?.objectKind ?? node.type) === "actionActivity" && data?.actionKind) {
    try {
      return createActionActivityFromActionRegistry({
        actionRegistryKey: data.actionKind,
        id: node.id,
        position,
        overrides: {
          caption: data.title ?? fallback?.caption,
          documentation: data.documentation ?? fallback?.documentation,
          disabled: data.disabled ?? (fallback as MicroflowActionActivity | undefined)?.disabled,
          action: data.action ?? (fallback as MicroflowActionActivity | undefined)?.action,
        } as Partial<MicroflowActionActivity>,
      });
    } catch {
      // Fall through to compiled object when an unknown action kind is stored in the design schema.
    }
  }
  const registryKey = findRegistryKeyForNode(node);
  if (registryKey) {
    try {
      return createObjectFromNodeRegistry({
        registryKey,
        id: node.id,
        position,
        overrides: {
          caption: data?.title ?? fallback?.caption,
          documentation: data?.documentation ?? fallback?.documentation,
          disabled: data?.disabled,
        } as Partial<MicroflowObject>,
      }).object;
    } catch {
      // Fall through to compiled object when the registry cannot construct this node.
    }
  }
  return fallback ?? ({
    id: node.id,
    stableId: node.id,
    kind: data?.objectKind ?? node.type,
    officialType: data?.officialType ?? node.type,
    caption: data?.title ?? node.id,
    documentation: data?.documentation ?? "",
    relativeMiddlePoint: position,
    size: nodeSize(node),
    editor: { iconKey: data?.objectKind ?? node.type },
    disabled: data?.disabled ?? false,
  } as MicroflowObject);
}

function propertyObjectForNode(node: MicroflowWorkflowNodeJSON, fallback?: MicroflowObject): MicroflowObject {
  const data = node.data as NodeDataWithPropertyObject | undefined;
  const base = defaultObjectForNode(node, fallback);
  const stored = data?.propertyObject && data.propertyObject.id === node.id ? data.propertyObject : undefined;
  return {
    ...base,
    ...stored,
    id: node.id,
    stableId: stored?.stableId ?? base.stableId ?? node.id,
    kind: stored?.kind ?? base.kind,
    officialType: stored?.officialType ?? base.officialType,
    caption: data?.title ?? stored?.caption ?? base.caption,
    documentation: data?.documentation ?? stored?.documentation ?? base.documentation,
    disabled: data?.disabled ?? ("disabled" in base ? base.disabled : undefined),
    relativeMiddlePoint: nodePosition(node),
    size: nodeSize(node),
  } as MicroflowObject;
}

function propertyFlowForEdge(edge: MicroflowWorkflowEdgeJSON, fallback?: MicroflowFlow): MicroflowFlow {
  const data = edge.data as EdgeDataWithPropertyFlow | undefined;
  const flowId = data?.flowId ?? edge.id ?? createStableId("flow");
  const stored = data?.propertyFlow && data.propertyFlow.id === flowId ? data.propertyFlow : undefined;
  const base = stored ?? fallback ?? ({
    id: flowId,
    stableId: flowId,
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
  } as MicroflowFlow);
  return {
    ...base,
    id: flowId,
    stableId: base.stableId ?? flowId,
    originObjectId: edge.sourceNodeID,
    destinationObjectId: edge.targetNodeID,
    caseValues: data?.caseValues ?? base.caseValues ?? [],
    isErrorHandler: data?.isErrorHandler ?? base.isErrorHandler ?? false,
    editor: {
      ...base.editor,
      edgeKind: data?.edgeKind ?? base.editor.edgeKind ?? "sequence",
      label: data?.label ?? base.editor.label,
      description: data?.description ?? base.editor.description,
      branchOrder: data?.branchOrder ?? (base.editor as { branchOrder?: number }).branchOrder,
    },
  } as MicroflowFlow;
}

function flowMatchesEdge(edge: MicroflowWorkflowEdgeJSON, flowId?: string): boolean {
  const data = edge.data as Partial<FlowGramMicroflowEdgeData> | undefined;
  return Boolean(flowId && (edge.id === flowId || data?.flowId === flowId));
}

function withSelection(schema: MicroflowDesignSchema, objectId?: string, flowId?: string): MicroflowDesignSchema {
  return {
    ...schema,
    editor: {
      ...schema.editor,
      selectedObjectId: objectId,
      selectedFlowId: flowId,
      selectedCollectionId: objectId ? MICROFLOW_ROOT_COLLECTION_ID : undefined,
      selection: {
        objectId,
        flowId,
        collectionId: objectId ? MICROFLOW_ROOT_COLLECTION_ID : undefined,
        objectIds: objectId ? [objectId] : [],
        flowIds: flowId ? [flowId] : [],
        mode: objectId || flowId ? "single" : "none",
      },
    },
  };
}

export function buildDesignPropertyPanelModel(schema: MicroflowDesignSchema): DesignPropertyPanelModel {
  const authoringSchema = compileMicroflowDesignToRuntime(schema);
  const runtimeObjects = new Map(authoringSchema.objectCollection.objects.map(object => [object.id, object]));
  const runtimeFlows = new Map(authoringSchema.flows.map(flow => [flow.id, flow]));
  authoringSchema.objectCollection.objects = schema.workflow.nodes.map(node => propertyObjectForNode(node, runtimeObjects.get(node.id)));
  authoringSchema.flows = schema.workflow.edges.map(edge => {
    const data = edge.data as Partial<FlowGramMicroflowEdgeData> | undefined;
    return propertyFlowForEdge(edge, runtimeFlows.get(data?.flowId ?? edge.id ?? ""));
  });
  const selectedObjectId = schema.editor.selection?.objectId ?? schema.editor.selectedObjectId;
  const selectedFlowId = schema.editor.selection?.flowId ?? schema.editor.selectedFlowId;
  return {
    authoringSchema,
    selectedObject: selectedObjectId ? authoringSchema.objectCollection.objects.find(object => object.id === selectedObjectId) ?? null : null,
    selectedFlow: selectedFlowId ? authoringSchema.flows.find(flow => flow.id === selectedFlowId) ?? null : null,
  };
}

export function applyDesignDocumentSchema(schema: MicroflowDesignSchema, nextAuthoringSchema: MicroflowAuthoringSchema): MicroflowDesignSchema {
  return {
    ...schema,
    description: nextAuthoringSchema.description,
    documentation: nextAuthoringSchema.documentation,
    returnType: nextAuthoringSchema.returnType,
    returnVariableName: nextAuthoringSchema.returnVariableName,
    parameters: nextAuthoringSchema.parameters,
    audit: {
      ...schema.audit,
      updatedAt: new Date().toISOString(),
    },
  };
}

export function applyDesignObjectPatch(schema: MicroflowDesignSchema, objectId: string, patch: MicroflowNodePatch): MicroflowDesignSchema {
  const nextObject = patch.object as MicroflowObject | undefined;
  if (!nextObject) {
    return schema;
  }
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes: schema.workflow.nodes.map(node => {
        if (node.id !== objectId) {
          return node;
        }
        const existingData = node.data as NodeDataWithPropertyObject | undefined;
        const nextData: NodeDataWithPropertyObject = {
          ...existingData,
          propertyObject: cloneJson(nextObject),
          objectId,
          objectKind: nextObject.kind,
          collectionId: existingData?.collectionId ?? String(node.meta?.collectionId ?? MICROFLOW_ROOT_COLLECTION_ID),
          title: nextObject.caption,
          documentation: nextObject.documentation,
          officialType: nextObject.officialType,
          disabled: "disabled" in nextObject ? Boolean(nextObject.disabled) : existingData?.disabled ?? false,
          actionKind: nextObject.kind === "actionActivity" ? nextObject.action.kind : existingData?.actionKind,
          action: nextObject.kind === "actionActivity" ? nextObject.action : existingData?.action,
        };
        return {
          ...node,
          type: nextObject.kind,
          data: nextData as Record<string, unknown>,
          meta: {
            ...node.meta,
            position: node.meta?.position,
            size: nextObject.size ?? node.meta?.size,
            nodeDTOType: nextObject.kind,
            collectionId: nextData.collectionId,
          },
        };
      }),
    },
    audit: {
      ...schema.audit,
      updatedAt: new Date().toISOString(),
    },
  };
}

export function applyDesignFlowPatch(schema: MicroflowDesignSchema, flowId: string, patch: MicroflowEdgePatch): MicroflowDesignSchema {
  const nextFlow = patch as MicroflowFlow;
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      edges: schema.workflow.edges.map(edge => {
        if (!flowMatchesEdge(edge, flowId)) {
          return edge;
        }
        const existingData = edge.data as EdgeDataWithPropertyFlow | undefined;
        const edgeKind = nextFlow.kind === "annotation" ? "annotation" : nextFlow.isErrorHandler ? "errorHandler" : nextFlow.editor.edgeKind;
        const nextData: EdgeDataWithPropertyFlow = {
          ...existingData,
          propertyFlow: cloneJson(nextFlow),
          flowId: nextFlow.id,
          flowKind: nextFlow.kind,
          edgeKind,
          isErrorHandler: nextFlow.kind === "sequence" ? nextFlow.isErrorHandler : false,
          caseValues: nextFlow.kind === "sequence" ? nextFlow.caseValues : nextFlow.caseValues ?? [],
          label: nextFlow.editor.label,
          description: nextFlow.editor.description,
          branchOrder: (nextFlow.editor as { branchOrder?: number }).branchOrder,
          showInExport: nextFlow.kind === "annotation" ? nextFlow.editor.showInExport : existingData?.showInExport,
          validationState: existingData?.validationState ?? "valid",
          runtimeState: existingData?.runtimeState ?? "idle",
        };
        return {
          ...edge,
          id: nextFlow.id,
          sourceNodeID: nextFlow.originObjectId,
          targetNodeID: nextFlow.destinationObjectId,
          data: nextData as Record<string, unknown>,
        };
      }),
    },
    audit: {
      ...schema.audit,
      updatedAt: new Date().toISOString(),
    },
  };
}

export function deleteDesignObject(schema: MicroflowDesignSchema, objectId: string): MicroflowDesignSchema {
  const node = workflowNodeById(schema.workflow, objectId) as MicroflowWorkflowNodeJSON | undefined;
  const kind = (node?.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node?.type;
  if (objectId === "start" || objectId === "end" || kind === "startEvent" || kind === "endEvent") {
    return schema;
  }
  return withSelection({
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes: schema.workflow.nodes.filter(node => node.id !== objectId),
      edges: schema.workflow.edges.filter(edge => edge.sourceNodeID !== objectId && edge.targetNodeID !== objectId),
    },
    audit: {
      ...schema.audit,
      updatedAt: new Date().toISOString(),
    },
  });
}

export function deleteDesignFlow(schema: MicroflowDesignSchema, flowId: string): MicroflowDesignSchema {
  return withSelection({
    ...schema,
    workflow: {
      ...schema.workflow,
      edges: schema.workflow.edges.filter(edge => !flowMatchesEdge(edge, flowId)),
    },
    audit: {
      ...schema.audit,
      updatedAt: new Date().toISOString(),
    },
  });
}

export function duplicateDesignObject(schema: MicroflowDesignSchema, objectId: string): MicroflowDesignSchema {
  const node = workflowNodeById(schema.workflow, objectId) as MicroflowWorkflowNodeJSON | undefined;
  if (!node || objectId === "start") {
    return schema;
  }
  const existingIds = new Set(schema.workflow.nodes.map(item => item.id));
  let id = createStableId(`${objectId}-copy`);
  while (existingIds.has(id)) {
    id = createStableId(`${objectId}-copy`);
  }
  const position = nodePosition(node);
  const clone = createMicroflowWorkflowNode({
    id,
    objectKind: (node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type as FlowGramMicroflowNodeData["objectKind"],
    position: { x: position.x + 48, y: position.y + 48 },
    data: {
      ...(cloneJson(node.data ?? {}) as Partial<FlowGramMicroflowNodeData> & Record<string, unknown>),
      objectId: id,
      title: `${String((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.title ?? node.id)} Copy`,
      propertyObject: undefined,
    },
  }) as WorkflowNodeJSON;
  return withSelection({
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes: [...schema.workflow.nodes, clone as MicroflowWorkflowNodeJSON],
    },
    audit: {
      ...schema.audit,
      updatedAt: new Date().toISOString(),
    },
  }, id);
}
