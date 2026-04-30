import { useEffect, useMemo, useRef } from "react";

import {
  WorkflowDocument,
  WorkflowDragService,
  FlowNodeTransformData,
  WorkflowSelectService,
  type WorkflowNodeEntity,
  type WorkflowJSON,
  useService,
} from "@flowgram-adapter/free-layout-editor";
import { Toast } from "@douyinfe/semi-ui";

import { applyEditorGraphPatchToAuthoring, toEditorGraph } from "../../adapters";
import { canConnectPorts } from "../../node-registry";
import type { MicroflowCaseValue, MicroflowEditorGraphPatch, MicroflowSchema } from "../../schema";
import { authoringToFlowGram } from "../adapters/authoring-to-flowgram";
import {
  createFlowFromFlowGramEdge,
  findDeletedObjectId,
  findDeletedFlowId,
  findNewFlowGramEdge,
  flowGramPositionPatch,
} from "../adapters/flowgram-to-authoring-patch";
import {
  flowGramEdgeIdentitySignature,
  flowGramNodeIdentitySignature,
  flowGramPositionSignature,
  toFlowGramNodeId,
} from "../adapters/flowgram-identity";
import { selectionFromFlowGramEntityIds } from "../adapters/flowgram-selection-sync";
import { createMicroflowFlowFromPorts } from "../adapters/flowgram-edge-factory";
import { getCaseEditorKind } from "../adapters/flowgram-case-options";
import {
  FlowGramMicroflowBridgeService,
  FlowGramMicroflowBridgeServiceToken,
} from "../FlowGramMicroflowEvents";
import type { FlowGramMicroflowPendingLine, FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";
import { MICROFLOW_GRID_SIZE } from "../adapters/flowgram-coordinate";

export function hasSameNodeAndEdgeIdentity(a: WorkflowJSON | undefined, b: WorkflowJSON | undefined): boolean {
  return flowGramNodeIdentitySignature(a) === flowGramNodeIdentitySignature(b)
    && flowGramEdgeIdentitySignature(a) === flowGramEdgeIdentitySignature(b);
}

function getWorkflowIdentity(json: WorkflowJSON): string {
  return JSON.stringify({
    nodes: flowGramNodeIdentitySignature(json),
    edges: flowGramEdgeIdentitySignature(json),
  });
}

function movedNodeSignature(patch: MicroflowEditorGraphPatch): string {
  return JSON.stringify((patch.movedNodes ?? [])
    .map(node => `${node.objectId}:${node.position.x}:${node.position.y}`)
    .sort());
}

function resizedNodeSignature(patch: MicroflowEditorGraphPatch): string {
  return JSON.stringify((patch.resizedNodes ?? [])
    .map(node => `${node.objectId}:${node.size.width}:${node.size.height}`)
    .sort());
}

function syncFlowGramPositionsToSchema(doc: WorkflowDocument, schema: MicroflowSchema): boolean {
  const graph = toEditorGraph(schema);
  let synced = false;
  for (const node of graph.nodes) {
    const entity = doc.getNode?.(toFlowGramNodeId(node.objectId)) as WorkflowNodeEntity | undefined;
    const transform = entity?.getData?.(FlowNodeTransformData);
    if (!transform) {
      continue;
    }
    const current = transform.position ?? transform.bounds;
    if (!current) {
      continue;
    }
    if (Math.abs(current.x - node.position.x) <= 0.5 && Math.abs(current.y - node.position.y) <= 0.5) {
      continue;
    }
    transform.transform.update({
      position: {
        x: node.position.x,
        y: node.position.y,
      },
    });
    synced = true;
  }
  return synced;
}

function portById(schema: MicroflowSchema, portId?: string) {
  if (!portId) {
    return undefined;
  }
  return toEditorGraph(schema).nodes.flatMap(node => node.ports).find(port => port.id === portId);
}

export function useFlowGramMicroflowBridge(params: {
  schema: MicroflowSchema;
  issues: MicroflowSchema["validation"]["issues"];
  traceFrames?: NonNullable<MicroflowSchema["debug"]>["lastTrace"];
  readonly?: boolean;
  onSchemaChange: (schema: MicroflowSchema, reason: string) => void;
  onSelectionChange: (selection: FlowGramMicroflowSelection) => void;
  onPendingCaseLine: (line?: FlowGramMicroflowPendingLine) => void;
}) {
  type FlowGramChangeKind = "nodePosition" | "nodeStructure" | "edgeStructure";

  const doc = useService<WorkflowDocument>(WorkflowDocument);
  const dragService = useService<WorkflowDragService>(WorkflowDragService);
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const bridgeService = useService<FlowGramMicroflowBridgeService>(FlowGramMicroflowBridgeServiceToken);
  const reloadingFromSchemaRef = useRef(false);
  const draggingNodeRef = useRef(false);
  const applyingPositionPatchRef = useRef(false);
  const lastWorkflowIdentityRef = useRef<string>();
  const lastPositionSignatureRef = useRef<string>();
  const positionSettleTimerRef = useRef<number>();
  const latestSchemaRef = useRef(params.schema);
  const paramsRef = useRef(params);
  const lastAppliedPatchSignatureRef = useRef<string>();
  paramsRef.current = params;
  latestSchemaRef.current = params.schema;
  bridgeService.setSchema(params.schema);
  bridgeService.setGrid(params.schema.editor.gridEnabled !== false, MICROFLOW_GRID_SIZE);

  const workflowJson = useMemo(
    () => authoringToFlowGram(params.schema, params.issues, params.traceFrames),
    [params.schema.flows, params.schema.objectCollection, params.issues, params.traceFrames],
  );

  const rememberSchemaPositionSignature = (schema: MicroflowSchema) => {
    lastPositionSignatureRef.current = flowGramPositionSignature(
      authoringToFlowGram(schema, paramsRef.current.issues, paramsRef.current.traceFrames),
    );
  };

  const reloadFromSchema = (schema: MicroflowSchema) => {
    const nextJson = authoringToFlowGram(schema, schema.validation.issues, schema.debug?.lastTrace);
    const nextIdentity = getWorkflowIdentity(nextJson);
    const nextPositionSignature = flowGramPositionSignature(nextJson);
    reloadingFromSchemaRef.current = true;
    void Promise.resolve(doc.fromJSON(nextJson)).finally(() => {
      lastWorkflowIdentityRef.current = nextIdentity;
      lastPositionSignatureRef.current = nextPositionSignature;
      reloadingFromSchemaRef.current = false;
    });
  };

  useEffect(() => {
    const nextIdentity = getWorkflowIdentity(workflowJson);
    const nextPositionSignature = flowGramPositionSignature(workflowJson);
    if (lastWorkflowIdentityRef.current === nextIdentity && lastPositionSignatureRef.current === nextPositionSignature) {
      return;
    }
    if ((applyingPositionPatchRef.current || draggingNodeRef.current) && lastWorkflowIdentityRef.current === nextIdentity) {
      lastPositionSignatureRef.current = nextPositionSignature;
      return;
    }
    if (lastWorkflowIdentityRef.current === nextIdentity) {
      syncFlowGramPositionsToSchema(doc, params.schema);
      lastPositionSignatureRef.current = nextPositionSignature;
      return;
    }
    reloadingFromSchemaRef.current = true;
    void Promise.resolve(doc.fromJSON(workflowJson)).finally(() => {
      lastWorkflowIdentityRef.current = nextIdentity;
      lastPositionSignatureRef.current = nextPositionSignature;
      reloadingFromSchemaRef.current = false;
    });
  }, [doc, workflowJson]);

  useEffect(() => () => {
    if (positionSettleTimerRef.current !== undefined) {
      window.clearTimeout(positionSettleTimerRef.current);
    }
  }, []);

  const releaseDragPositionGuardSoon = () => {
    if (positionSettleTimerRef.current !== undefined) {
      window.clearTimeout(positionSettleTimerRef.current);
    }
    positionSettleTimerRef.current = window.setTimeout(() => {
      positionSettleTimerRef.current = undefined;
      draggingNodeRef.current = false;
      applyingPositionPatchRef.current = false;
    }, 120);
  };

  const commitFinalDraggedPositions = () => {
    const schema = latestSchemaRef.current;
    const json = doc.toJSON() as WorkflowJSON;
    const grid = bridgeService.getGridConfig();
    const patch = flowGramPositionPatch(schema, json, {
      gridEnabled: grid.enabled,
      gridSize: grid.size,
    });
    const patchSignature = `${movedNodeSignature(patch)}|${resizedNodeSignature(patch)}`;
    if (!(patch.movedNodes?.length || patch.resizedNodes?.length) || patchSignature === lastAppliedPatchSignatureRef.current) {
      releaseDragPositionGuardSoon();
      return;
    }
    const nextSchema = applyEditorGraphPatchToAuthoring(schema, patch);
    latestSchemaRef.current = nextSchema;
    bridgeService.setSchema(nextSchema);
    rememberSchemaPositionSignature(nextSchema);
    lastAppliedPatchSignatureRef.current = patchSignature;
    applyingPositionPatchRef.current = true;
    syncFlowGramPositionsToSchema(doc, nextSchema);
    paramsRef.current.onSchemaChange(nextSchema, "flowgramNodeMove");
    releaseDragPositionGuardSoon();
  };

  useEffect(() => {
    bridgeService.registerDeleteFlowHandler((flowId: string) => {
      const schema = latestSchemaRef.current;
      paramsRef.current.onSchemaChange(
        applyEditorGraphPatchToAuthoring(schema, { deleteFlowId: flowId } as MicroflowEditorGraphPatch),
        "flowgramLineDelete",
      );
    });
  }, [bridgeService]);

  useEffect(() => {
    const disposable = dragService.onNodesDrag(event => {
      if (paramsRef.current.readonly) {
        return;
      }
      if (event.type === "onDragStart") {
        draggingNodeRef.current = true;
        applyingPositionPatchRef.current = false;
        lastAppliedPatchSignatureRef.current = undefined;
        if (positionSettleTimerRef.current !== undefined) {
          window.clearTimeout(positionSettleTimerRef.current);
          positionSettleTimerRef.current = undefined;
        }
        return;
      }
      if (event.type === "onDragEnd") {
        commitFinalDraggedPositions();
      }
    });
    return () => disposable.dispose();
  }, [dragService]);

  useEffect(() => {
    const disposable = doc.onContentChange(() => {
      if (reloadingFromSchemaRef.current || paramsRef.current.readonly) {
        return;
      }
      const json = doc.toJSON() as WorkflowJSON;
      const schema = latestSchemaRef.current;
      const currentJson = authoringToFlowGram(schema, schema.validation.issues, schema.debug?.lastTrace);
      const changeKind: FlowGramChangeKind = !hasSameNodeAndEdgeIdentity(currentJson, json)
        ? flowGramNodeIdentitySignature(currentJson) !== flowGramNodeIdentitySignature(json)
          ? "nodeStructure"
          : "edgeStructure"
        : "nodePosition";
      const grid = bridgeService.getGridConfig();
      const patch = flowGramPositionPatch(schema, json, {
        gridEnabled: grid.enabled,
        gridSize: grid.size,
      });
      if (changeKind === "nodePosition") {
        return;
      }
      const deletedObjectId = findDeletedObjectId(schema, json);
      if (deletedObjectId) {
        const nextSchema = applyEditorGraphPatchToAuthoring(schema, {
          ...patch,
          deleteObjectId: deletedObjectId,
        } as MicroflowEditorGraphPatch);
        latestSchemaRef.current = nextSchema;
        bridgeService.setSchema(nextSchema);
        rememberSchemaPositionSignature(nextSchema);
        paramsRef.current.onSchemaChange(
          nextSchema,
          "flowgramNodeDelete",
        );
        return;
      }
      const deletedFlowId = findDeletedFlowId(schema, json);
      if (deletedFlowId) {
        const nextSchema = applyEditorGraphPatchToAuthoring(schema, {
          ...patch,
          deleteFlowId: deletedFlowId,
        } as MicroflowEditorGraphPatch);
        latestSchemaRef.current = nextSchema;
        bridgeService.setSchema(nextSchema);
        rememberSchemaPositionSignature(nextSchema);
        paramsRef.current.onSchemaChange(
          nextSchema,
          "flowgramLineDelete",
        );
        return;
      }
      const newEdge = findNewFlowGramEdge(schema, json);
      if (newEdge) {
        const sourcePort = portById(schema, newEdge.sourcePortID === undefined ? undefined : String(newEdge.sourcePortID));
        const targetPort = portById(schema, newEdge.targetPortID === undefined ? undefined : String(newEdge.targetPortID));
        const check = sourcePort && targetPort ? canConnectPorts(schema, sourcePort, targetPort) : undefined;
        if (!sourcePort || !targetPort || !check?.allowed) {
          Toast.warning(check?.message ?? "The selected ports cannot be connected.");
          reloadFromSchema(schema);
          return;
        }
        const caseKind = getCaseEditorKind(schema, sourcePort.objectId);
        if (caseKind && (check.suggestedEdgeKind === "decisionCondition" || check.suggestedEdgeKind === "objectTypeCondition")) {
          paramsRef.current.onPendingCaseLine({
            caseKind,
            sourcePortId: sourcePort.id,
            targetPortId: targetPort.id,
            sourceObjectId: sourcePort.objectId,
            targetObjectId: targetPort.objectId,
          });
          reloadFromSchema(schema);
          return;
        }
        const flow = createFlowFromFlowGramEdge(schema, newEdge);
        if (!flow) {
          Toast.warning("无法识别连线端口，已回退本次连线。");
          reloadFromSchema(schema);
          return;
        }
        const nextSchema = applyEditorGraphPatchToAuthoring(schema, {
          ...patch,
          addFlow: flow,
          selectedFlowId: flow.id,
          selectedObjectId: undefined,
        } as MicroflowEditorGraphPatch);
        latestSchemaRef.current = nextSchema;
        bridgeService.setSchema(nextSchema);
        rememberSchemaPositionSignature(nextSchema);
        paramsRef.current.onSchemaChange(
          nextSchema,
          "flowgramLineAdd",
        );
        return;
      }
      reloadFromSchema(schema);
    });
    return () => disposable.dispose();
  }, [doc]);

  useEffect(() => {
    const disposable = selectService.onSelectionChanged(() => {
      const ids = (selectService.selection ?? [])
        .map(selection => selection?.id)
        .filter((id): id is string => typeof id === "string" && id.length > 0);
      paramsRef.current.onSelectionChange(selectionFromFlowGramEntityIds(latestSchemaRef.current, ids));
    });
    return () => disposable.dispose();
  }, [selectService]);

  return {
    createCaseFlow(caseValues: MicroflowCaseValue[], label: string, pending: FlowGramMicroflowPendingLine) {
      const sourcePort = portById(latestSchemaRef.current, pending.sourcePortId);
      const targetPort = portById(latestSchemaRef.current, pending.targetPortId);
      if (!sourcePort || !targetPort) {
        return;
      }
      const flow = createMicroflowFlowFromPorts(latestSchemaRef.current, sourcePort, targetPort, { caseValues, label });
      paramsRef.current.onSchemaChange(
        applyEditorGraphPatchToAuthoring(latestSchemaRef.current, {
          addFlow: flow,
          selectedFlowId: flow.id,
          selectedObjectId: undefined,
        } as MicroflowEditorGraphPatch),
        "flowgramLineAdd",
      );
    },
  };
}
