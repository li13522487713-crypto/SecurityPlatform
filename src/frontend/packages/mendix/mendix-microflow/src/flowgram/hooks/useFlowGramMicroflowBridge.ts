import { useEffect, useMemo, useRef } from "react";

import {
  WorkflowDocument,
  FlowNodeTransformData,
  WorkflowSelectService,
  type WorkflowNodeEntity,
  type WorkflowEdgeJSON,
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
  if (!a || !b) {
    return false;
  }
  const nodeIdentity = (json: WorkflowJSON) => (json.nodes ?? [])
    .map(node => {
      const meta = node.meta as { parentObjectId?: string; collectionId?: string } | undefined;
      return [String(node.id), String(node.type ?? ""), meta?.parentObjectId ?? "", meta?.collectionId ?? ""].join("::");
    })
    .sort()
    .join("|");
  const edgeIdentity = (json: WorkflowJSON) => (json.edges ?? [])
    .map(edge => {
      const edgeData = edge as WorkflowEdgeJSON & { id?: string };
      return [
        String(edgeData.id ?? ""),
        String(edge.sourceNodeID ?? ""),
        String(edge.sourcePortID ?? ""),
        String(edge.targetNodeID ?? ""),
        String(edge.targetPortID ?? ""),
      ].join("::");
    })
    .sort()
    .join("|");
  return nodeIdentity(a) === nodeIdentity(b) && edgeIdentity(a) === edgeIdentity(b);
}

function getWorkflowIdentity(json: WorkflowJSON): string {
  const nodes = (json.nodes ?? [])
    .map(node => {
      const meta = node.meta as { parentObjectId?: string; collectionId?: string } | undefined;
      return `${String(node.id)}:${String(node.type ?? "")}:${meta?.parentObjectId ?? ""}:${meta?.collectionId ?? ""}`;
    })
    .sort();
  const edges = (json.edges ?? [])
    .map(edge => {
      const edgeData = edge as WorkflowEdgeJSON & { id?: string };
      return `${String(edgeData.id ?? "")}:${String(edge.sourceNodeID ?? "")}:${String(edge.sourcePortID ?? "")}:${String(edge.targetNodeID ?? "")}:${String(edge.targetPortID ?? "")}`;
    })
    .sort();
  return JSON.stringify({ nodes, edges });
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

function hasMeaningfulSnapDelta(rawPatch: MicroflowEditorGraphPatch, snappedPatch: MicroflowEditorGraphPatch, gridSize: number): boolean {
  const rawMap = new Map((rawPatch.movedNodes ?? []).map(node => [node.objectId, node.position]));
  const threshold = Math.max(1, gridSize / 2);
  return (snappedPatch.movedNodes ?? []).some(node => {
    const raw = rawMap.get(node.objectId);
    if (!raw) {
      return false;
    }
    return Math.abs(raw.x - node.position.x) >= threshold || Math.abs(raw.y - node.position.y) >= threshold;
  });
}

function syncFlowGramPositionsToSchema(doc: WorkflowDocument, schema: MicroflowSchema): boolean {
  const graph = toEditorGraph(schema);
  let synced = false;
  for (const node of graph.nodes) {
    const entity = doc.getNode?.(node.objectId) as WorkflowNodeEntity | undefined;
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
  const doc = useService<WorkflowDocument>(WorkflowDocument);
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const bridgeService = useService<FlowGramMicroflowBridgeService>(FlowGramMicroflowBridgeServiceToken);
  const reloadingRef = useRef(false);
  const internalPositionChangeRef = useRef(false);
  const lastLoadedIdentityRef = useRef<string>();
  const snapReconcileTimerRef = useRef<number>();
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

  useEffect(() => {
    const nextIdentity = getWorkflowIdentity(workflowJson);
    if (internalPositionChangeRef.current && lastLoadedIdentityRef.current === nextIdentity) {
      return;
    }
    reloadingRef.current = true;
    void Promise.resolve(doc.fromJSON(workflowJson)).finally(() => {
      lastLoadedIdentityRef.current = nextIdentity;
      reloadingRef.current = false;
    });
  }, [doc, workflowJson]);

  useEffect(() => () => {
    if (snapReconcileTimerRef.current !== undefined) {
      window.clearTimeout(snapReconcileTimerRef.current);
    }
  }, []);

  useEffect(() => {
    const disposable = doc.onContentChange(() => {
      if (reloadingRef.current || paramsRef.current.readonly) {
        return;
      }
      const json = doc.toJSON() as WorkflowJSON;
      const schema = latestSchemaRef.current;
      const deletedObjectId = findDeletedObjectId(schema, json);
      if (deletedObjectId) {
        paramsRef.current.onSchemaChange(
          applyEditorGraphPatchToAuthoring(schema, { deleteObjectId: deletedObjectId } as MicroflowEditorGraphPatch),
          "flowgramNodeDelete",
        );
        return;
      }
      const deletedFlowId = findDeletedFlowId(schema, json);
      if (deletedFlowId) {
        paramsRef.current.onSchemaChange(
          applyEditorGraphPatchToAuthoring(schema, { deleteFlowId: deletedFlowId } as MicroflowEditorGraphPatch),
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
          reloadingRef.current = true;
          void Promise.resolve(doc.fromJSON(authoringToFlowGram(schema, schema.validation.issues, schema.debug?.lastTrace))).finally(() => {
            reloadingRef.current = false;
          });
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
          reloadingRef.current = true;
          void Promise.resolve(doc.fromJSON(authoringToFlowGram(schema, schema.validation.issues, schema.debug?.lastTrace))).finally(() => {
            reloadingRef.current = false;
          });
          return;
        }
        const flow = createFlowFromFlowGramEdge(schema, newEdge);
        if (!flow) {
          Toast.warning("无法识别连线端口，已回退本次连线。");
          reloadingRef.current = true;
          void Promise.resolve(doc.fromJSON(authoringToFlowGram(schema, schema.validation.issues, schema.debug?.lastTrace))).finally(() => {
            reloadingRef.current = false;
          });
          return;
        }
        paramsRef.current.onSchemaChange(
          applyEditorGraphPatchToAuthoring(schema, { addFlow: flow, selectedFlowId: flow.id, selectedObjectId: undefined } as MicroflowEditorGraphPatch),
          "flowgramLineAdd",
        );
        return;
      }
      const grid = bridgeService.getGridConfig();
      const rawPatch = flowGramPositionPatch(schema, json, { gridEnabled: false });
      const patch = flowGramPositionPatch(schema, json, {
        gridEnabled: grid.enabled,
        gridSize: grid.size,
      });
      const patchSignature = `${movedNodeSignature(patch)}|${resizedNodeSignature(patch)}`;
      if (patchSignature === lastAppliedPatchSignatureRef.current && (patch.movedNodes?.length || patch.resizedNodes?.length)) {
        return;
      }
      if (patch.movedNodes?.length || patch.resizedNodes?.length) {
        const nextSchema = applyEditorGraphPatchToAuthoring(schema, patch);
        const snapChanged = movedNodeSignature(rawPatch) !== movedNodeSignature(patch);
        const shouldReconcileSnap = snapChanged
          && (patch.movedNodes?.length ?? 0) > 0
          && hasMeaningfulSnapDelta(rawPatch, patch, Math.max(2, grid.size));
        internalPositionChangeRef.current = true;
        latestSchemaRef.current = nextSchema;
        bridgeService.setSchema(nextSchema);
        lastAppliedPatchSignatureRef.current = patchSignature;
        paramsRef.current.onSchemaChange(nextSchema, "flowgramNodeMove");
        if (shouldReconcileSnap) {
          if (snapReconcileTimerRef.current !== undefined) {
            window.clearTimeout(snapReconcileTimerRef.current);
          }
          snapReconcileTimerRef.current = window.setTimeout(() => {
            snapReconcileTimerRef.current = undefined;
            reloadingRef.current = true;
            const directlySynced = syncFlowGramPositionsToSchema(doc, nextSchema);
            if (directlySynced) {
              reloadingRef.current = false;
              return;
            }
            void Promise.resolve(
              doc.fromJSON(authoringToFlowGram(nextSchema, nextSchema.validation.issues, nextSchema.debug?.lastTrace)),
            ).finally(() => {
              lastLoadedIdentityRef.current = getWorkflowIdentity(authoringToFlowGram(nextSchema, nextSchema.validation.issues, nextSchema.debug?.lastTrace));
              reloadingRef.current = false;
            });
          }, 80);
        }
        queueMicrotask(() => {
          internalPositionChangeRef.current = false;
        });
      }
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
