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
  findDeletedFlowId,
  findNewFlowGramEdge,
  flowGramPositionPatch,
} from "../adapters/flowgram-to-authoring-patch";
import { createMicroflowFlowFromPorts } from "../adapters/flowgram-edge-factory";
import { FlowGramMicroflowBridgeService } from "../FlowGramMicroflowEvents";
import type { FlowGramMicroflowPendingLine, FlowGramMicroflowSelection } from "../FlowGramMicroflowTypes";

function portById(schema: MicroflowSchema, portId?: string) {
  if (!portId) {
    return undefined;
  }
  return toEditorGraph(schema).nodes.flatMap(node => node.ports).find(port => port.id === portId);
}

function isBooleanDecisionLine(schema: MicroflowSchema, edge: NonNullable<ReturnType<typeof findNewFlowGramEdge>>): boolean {
  const source = schema.objectCollection.objects.find(object => object.id === edge.sourceNodeID);
  return source?.kind === "exclusiveSplit" && source.splitCondition.resultType?.kind === "boolean";
}

export function useFlowGramMicroflowBridge(params: {
  schema: MicroflowSchema;
  issues: MicroflowSchema["validation"]["issues"];
  traceFrames: MicroflowSchema["debug"]["lastTrace"];
  readonly?: boolean;
  onSchemaChange: (schema: MicroflowSchema, reason: string) => void;
  onSelectionChange: (selection: FlowGramMicroflowSelection) => void;
  onPendingBooleanLine: (line?: FlowGramMicroflowPendingLine) => void;
}) {
  const doc = useService<WorkflowDocument>(WorkflowDocument);
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const bridgeService = useService<FlowGramMicroflowBridgeService>(FlowGramMicroflowBridgeService);
  const reloadingRef = useRef(false);
  const latestSchemaRef = useRef(params.schema);
  latestSchemaRef.current = params.schema;
  bridgeService.setSchema(params.schema);

  const workflowJson = useMemo(
    () => authoringToFlowGram(params.schema, params.issues, params.traceFrames),
    [params.schema, params.issues, params.traceFrames],
  );

  useEffect(() => {
    reloadingRef.current = true;
    void Promise.resolve(doc.fromJSON(workflowJson)).finally(() => {
      reloadingRef.current = false;
    });
  }, [doc, workflowJson]);

  useEffect(() => {
    const disposable = doc.onContentChange(() => {
      if (reloadingRef.current || params.readonly) {
        return;
      }
      const json = doc.toJSON() as WorkflowJSON;
      const schema = latestSchemaRef.current;
      const deletedFlowId = findDeletedFlowId(schema, json);
      if (deletedFlowId) {
        params.onSchemaChange(
          applyEditorGraphPatchToAuthoring(schema, { deletedFlowIds: [deletedFlowId] } as MicroflowEditorGraphPatch),
          "flowgramLineDelete",
        );
        return;
      }
      const newEdge = findNewFlowGramEdge(schema, json);
      if (newEdge) {
        const sourcePort = portById(schema, newEdge.sourcePortID);
        const targetPort = portById(schema, newEdge.targetPortID);
        if (!sourcePort || !targetPort || !canConnectPorts(schema, sourcePort, targetPort).allowed) {
          Toast.warning("当前端口连接不合法。");
          reloadingRef.current = true;
          void Promise.resolve(doc.fromJSON(authoringToFlowGram(schema, schema.validation.issues, schema.debug.lastTrace))).finally(() => {
            reloadingRef.current = false;
          });
          return;
        }
        if (isBooleanDecisionLine(schema, newEdge)) {
          params.onPendingBooleanLine({
            sourcePortId: sourcePort.id,
            targetPortId: targetPort.id,
            sourceObjectId: sourcePort.objectId,
            targetObjectId: targetPort.objectId,
          });
          reloadingRef.current = true;
          void Promise.resolve(doc.fromJSON(authoringToFlowGram(schema, schema.validation.issues, schema.debug.lastTrace))).finally(() => {
            reloadingRef.current = false;
          });
          return;
        }
        const flow = createFlowFromFlowGramEdge(schema, newEdge);
        if (flow) {
          params.onSchemaChange(
            applyEditorGraphPatchToAuthoring(schema, { addFlow: flow, selectedFlowId: flow.id, selectedObjectId: undefined } as MicroflowEditorGraphPatch),
            "flowgramLineAdd",
          );
        }
        return;
      }
      const patch = flowGramPositionPatch(schema, json);
      if (patch.movedNodes?.length) {
        params.onSchemaChange(applyEditorGraphPatchToAuthoring(schema, patch), "flowgramNodeMove");
      }
    });
    return () => disposable.dispose();
  }, [doc, params]);

  useEffect(() => {
    const disposable = selectService.onSelectionChanged(() => {
      const selection = selectService.selection?.[0];
      const id = selection?.id as string | undefined;
      if (!id) {
        params.onSelectionChange({});
        return;
      }
      if (latestSchemaRef.current.flows.some(flow => flow.id === id)) {
        params.onSelectionChange({ flowId: id });
        return;
      }
      params.onSelectionChange({ objectId: id });
    });
    return () => disposable.dispose();
  }, [params, selectService]);

  return {
    createBooleanCaseFlow(caseValues: MicroflowCaseValue[], label: string, pending: FlowGramMicroflowPendingLine) {
      const sourcePort = portById(latestSchemaRef.current, pending.sourcePortId);
      const targetPort = portById(latestSchemaRef.current, pending.targetPortId);
      if (!sourcePort || !targetPort) {
        return;
      }
      const flow = createMicroflowFlowFromPorts(latestSchemaRef.current, sourcePort, targetPort, { caseValues, label });
      params.onSchemaChange(
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
