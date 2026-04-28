import { memo } from "react";
import type { MicroflowObject, MicroflowVariableSymbol } from "../schema";
import { FlowEdgeForm } from "./forms/flow-edge-form";
import { MicroflowDocumentPropertiesForm } from "./forms/microflow-document-properties-form";
import { ObjectPanel } from "./forms/object-panel";
import type { MicroflowNodeFormRegistry, MicroflowPropertyPanelProps } from "./types";

export * from "./types";
export * from "./utils";
export { createExpression, primitiveType, FieldLabel, FieldRow, KeyValueEditor } from "./controls";
export * from "./common";
export * from "./selectors";
export * from "./expression";

export function getMicroflowNodeFormKey(object: MicroflowObject): string {
  return object.kind === "actionActivity" ? `activity:${object.action.kind}` : object.kind;
}

export const microflowNodeFormRegistry: MicroflowNodeFormRegistry = {};

export function buildVariablesForPropertyPanel(schema: { variables?: Record<string, Record<string, MicroflowVariableSymbol>> }): MicroflowVariableSymbol[] {
  return Object.values(schema.variables ?? {}).flatMap(group => Object.values(group));
}

export const MicroflowPropertyPanel = memo(function MicroflowPropertyPanel(props: MicroflowPropertyPanelProps) {
  if (!props.selectedObject && !props.selectedFlow) {
    return <MicroflowDocumentPropertiesForm {...props} />;
  }
  return (
    <div style={{ height: "100%", minHeight: 0, overflow: "auto", background: "var(--semi-color-bg-1, #fff)" }}>
      {props.selectedFlow ? <FlowEdgeForm {...props} /> : <ObjectPanel {...props} />}
    </div>
  );
});
