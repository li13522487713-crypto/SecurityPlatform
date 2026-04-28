import { useEffect, useMemo, useRef } from "react";

import {
  WorkflowDocument,
  WorkflowSelectService,
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
  const latestSchemaRef = useRef(params.schema);
  const paramsRef = useRef(params);
  paramsRef.current = params;
  latestSchemaRef.current = params.schema;
  bridgeService.setSchema(params.schema);

  const workflowJson = useMemo(
    () => authoringToFlowGram(params.schema, params.issues, params.traceFrames),
    [params.schema.objectCollection, params.issues, params.traceFrames],
  );

  useEffect(() => {
    reloadingRef.current = true;
    void Promise.resolve(doc.fromJSON(workflowJson)).finally(() => {
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
      const patch = flowGramPositionPatch(schema, json);
      if (patch.movedNodes?.length || patch.resizedNodes?.length) {
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
