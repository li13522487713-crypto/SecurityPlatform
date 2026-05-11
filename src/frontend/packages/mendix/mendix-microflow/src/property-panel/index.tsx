import { memo } from "react";
import type { MicroflowVariableSymbol } from "../schema";
import { FlowEdgeForm } from "./forms/flow-edge-form";
import { MicroflowDocumentPropertiesForm } from "./forms/microflow-document-properties-form";
import { ObjectPanel } from "./forms/object-panel";
import type { MicroflowAuthoringSchema } from "../schema";
import {
  applyDesignDocumentSchema,
  applyDesignFlowPatch,
  applyDesignObjectPatch,
  buildDesignPropertyPanelModel,
  deleteDesignFlow,
  deleteDesignObject,
  duplicateDesignObject,
} from "./design-protocol-adapter";
import type { MicroflowDesignPropertyPanelProps, MicroflowEdgePatch, MicroflowNodePatch, MicroflowPropertyPanelProps, MicroflowPropertyPanelRuntimeProps } from "./types";

export * from "./types";
export * from "./utils";
export { createExpression, primitiveType, FieldLabel, FieldRow, KeyValueEditor } from "./controls";
export * from "./common";
export * from "./selectors";
export * from "./expression";
export {
  getMicroflowNodeFormKey,
  microflowNodeFormRegistry,
  registerMicroflowNodeForm,
  unregisterMicroflowNodeForm,
  getMicroflowNodeFormForObject,
  type RegisterMicroflowNodeFormOptions,
} from "./node-form-registry";

export function buildVariablesForPropertyPanel(schema: { variables?: Record<string, Record<string, MicroflowVariableSymbol>> }): MicroflowVariableSymbol[] {
  return Object.values(schema.variables ?? {}).flatMap(group => Object.values(group));
}

function AuthoringMicroflowPropertyPanel(props: MicroflowPropertyPanelProps) {
  if (!props.selectedObject && !props.selectedFlow) {
    return (
      <div style={{ height: "100%", minHeight: 0, overflow: "auto", background: "var(--semi-color-bg-1, #fff)" }}>
        {props.onClose ? (
          <div style={{ display: "flex", justifyContent: "flex-end", padding: "0 0 8px" }}>
            <button
              type="button"
              aria-label="close"
              style={{
                width: 28,
                height: 28,
                border: 0,
                borderRadius: 4,
                color: "var(--semi-color-primary, #165dff)",
                background: "transparent",
                cursor: "pointer",
                fontSize: 20,
                lineHeight: "28px",
              }}
              onClick={props.onClose}
            >
              ×
            </button>
          </div>
        ) : null}
        <MicroflowDocumentPropertiesForm {...props} />
      </div>
    );
  }
  return (
    <div style={{ height: "100%", minHeight: 0, overflow: "auto", background: "var(--semi-color-bg-1, #fff)" }}>
      {props.selectedFlow ? <FlowEdgeForm {...props} /> : <ObjectPanel {...props} />}
    </div>
  );
}

function DesignMicroflowPropertyPanel(props: MicroflowDesignPropertyPanelProps) {
  const model = buildDesignPropertyPanelModel(props.schema);
  const authoringProps: MicroflowPropertyPanelProps = {
    schemaProtocol: "authoring",
    selectedObject: model.selectedObject,
    selectedFlow: model.selectedFlow,
    schema: model.authoringSchema,
    validationIssues: props.validationIssues,
    traceFrames: props.traceFrames,
    readonly: props.readonly,
    highlightedVariableName: props.highlightedVariableName,
    onClose: props.onClose,
    onLocateObject: props.onLocateObject,
    onHighlightVariableUsage: props.onHighlightVariableUsage,
    onSchemaChange: (nextSchema: MicroflowAuthoringSchema, reason: string) => {
      props.onSchemaChange?.(applyDesignDocumentSchema(props.schema, nextSchema), reason);
    },
    onObjectChange: (objectId: string, patch: MicroflowNodePatch) => {
      props.onSchemaChange?.(applyDesignObjectPatch(props.schema, objectId, patch), patch.object && "action" in patch.object ? "updateActionProperty" : "updateNodeProperty");
    },
    onFlowChange: (flowId: string, patch: MicroflowEdgePatch) => {
      props.onSchemaChange?.(applyDesignFlowPatch(props.schema, flowId, patch), "updateEdgeProperty");
    },
    onDuplicateObject: (objectId: string) => {
      props.onSchemaChange?.(duplicateDesignObject(props.schema, objectId), "duplicateNode");
    },
    onDeleteObject: (objectId: string) => {
      props.onSchemaChange?.(deleteDesignObject(props.schema, objectId), "deleteNode");
    },
    onDeleteFlow: (flowId: string) => {
      props.onSchemaChange?.(deleteDesignFlow(props.schema, flowId), "deleteFlow");
    },
  };
  return <AuthoringMicroflowPropertyPanel {...authoringProps} />;
}

export const MicroflowPropertyPanel = memo(function MicroflowPropertyPanel(props: MicroflowPropertyPanelRuntimeProps) {
  if (props.schemaProtocol === "design") {
    return <DesignMicroflowPropertyPanel {...props} />;
  }
  return <AuthoringMicroflowPropertyPanel {...props} />;
});
