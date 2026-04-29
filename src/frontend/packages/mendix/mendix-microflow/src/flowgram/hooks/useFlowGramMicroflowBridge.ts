import { useEffect, useMemo, useRef } from "react";

import {
  WorkflowDocument,
  WorkflowSelectService,
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
import { selectionFromFlowGramEntityId } from "../adapters/flowgram-selection-sync";
import { createMicroflowFlowFromPorts } from "../adapters/flowgram-edge-factory";
import { getCaseEditorKind } from "../adapters/flowgram-case-options";
import { FlowGramMicroflowBridgeService } from "../FlowGramMicroflowEvents";
import type { FlowGramMicroflowPendingLine, FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";

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
  const bridgeService = useService<FlowGramMicroflowBridgeService>(FlowGramMicroflowBridgeService);
  const reloadingRef = useRef(false);
  const pendingInternalPositionSyncRef = useRef(false);
  const latestSchemaRef = useRef(params.schema);
  const paramsRef = useRef(params);
  paramsRef.current = params;
  latestSchemaRef.current = params.schema;
  bridgeService.setSchema(params.schema);

  const workflowJson = useMemo(
    () => authoringToFlowGram(params.schema, params.issues, params.traceFrames),
    [params.schema.flows, params.schema.objectCollection, params.issues, params.traceFrames],
  );

  useEffect(() => {
    if (
      pendingInternalPositionSyncRef.current &&
      hasSameNodeAndEdgeIdentity(doc.toJSON() as WorkflowJSON, workflowJson)
    ) {
      pendingInternalPositionSyncRef.current = false;
      return;
    }
    reloadingRef.current = true;
    void Promise.resolve(doc.fromJSON(workflowJson)).finally(() => {
      pendingInternalPositionSyncRef.current = false;
      reloadingRef.current = false;
    });
  }, [doc, workflowJson]);

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
        if (flow) {
          paramsRef.current.onSchemaChange(
            applyEditorGraphPatchToAuthoring(schema, { addFlow: flow, selectedFlowId: flow.id, selectedObjectId: undefined } as MicroflowEditorGraphPatch),
            "flowgramLineAdd",
          );
        }
        return;
      }
      const patch = flowGramPositionPatch(schema, json, {
        gridEnabled: schema.editor.gridEnabled !== false,
      });
      if (patch.movedNodes?.length || patch.resizedNodes?.length) {
        pendingInternalPositionSyncRef.current = true;
        paramsRef.current.onSchemaChange(applyEditorGraphPatchToAuthoring(schema, patch), "flowgramNodeMove");
      }
    });
    return () => disposable.dispose();
  }, [doc]);

  useEffect(() => {
    const disposable = selectService.onSelectionChanged(() => {
      const selection = selectService.selection?.[0];
      const id = selection?.id as string | undefined;
      paramsRef.current.onSelectionChange(selectionFromFlowGramEntityId(latestSchemaRef.current, id));
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
