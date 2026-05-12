import { useEffect, useMemo, useState } from "react";
import { Button, Input, Select, Space, Switch, TextArea, Tooltip, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject, MicroflowParameter } from "../../schema";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowVariableIndex } from "../../schema/types";
import { collectFlowsRecursive, collectLoopObjects, createStableId, getBreakContinueWarnings, getParameterNameWarning, removeMicroflowParameter, updateEndEventReturnValue, updateMicroflowReturnType, upsertMicroflowParameter } from "../../schema/utils";
import { FieldError } from "../common";
import { ExpressionEditor } from "../expression";
import { DataTypeSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject, updateParameter } from "../utils";
import { expression, Field } from "../panel-shared";

const { Text } = Typography;

function renderLegalState(messages: string[], defaultMessage: string) {
  return (
    <Space vertical align="start" spacing={4}>
      {messages.length
        ? messages.map(message => <Text key={message} type="warning" size="small">{message}</Text>)
        : <Text type="tertiary" size="small">{defaultMessage}</Text>}
    </Space>
  );
}

function uniqueMessages(messages: string[]) {
  return Array.from(new Set(messages.filter(Boolean)));
}

function withDisabledReason(disabledReason: string, enabledHint: string, control: JSX.Element) {
  return (
    <Tooltip content={disabledReason || enabledHint}>
      <span style={{ display: "inline-flex", width: "100%" }}>{control}</span>
    </Tooltip>
  );
}

export function EventNodesForm({ props, object, issues, metadata, variableIndex, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  patch: (next: MicroflowObject) => void;
}) {
  const readonlyDisabledReason = props.readonly ? "Readonly mode cannot edit event settings." : "";
  const flows = collectFlowsRecursive(props.schema);
  const objects = props.schema.objectCollection.objects;
  const endCount = objects.filter(item => item.kind === "endEvent").length;
  const incomingSummary = flows.filter(flow => flow.destinationObjectId === object.id).map(flow => `${flow.id}: ${flow.originObjectId}`).join("\n");
  const outgoingSummary = flows.filter(flow => flow.originObjectId === object.id).map(flow => `${flow.id}: ${flow.destinationObjectId}`).join("\n");
  const startParameters = props.schema.parameters ?? [];
  const [selectedParameterId, setSelectedParameterId] = useState<string>(() => startParameters[0]?.id ?? "");
  useEffect(() => {
    if (!startParameters.some(parameter => parameter.id === selectedParameterId)) {
      setSelectedParameterId(startParameters[0]?.id ?? "");
    }
  }, [selectedParameterId, startParameters]);
  const selectedParameter = useMemo(
    () => startParameters.find(parameter => parameter.id === selectedParameterId),
    [selectedParameterId, startParameters],
  );

  const patchParameter = (parameterId: string, parameterPatch: Partial<MicroflowParameter>) => {
    if (!props.onSchemaChange) {
      return;
    }
    props.onSchemaChange(updateParameter(props.schema, parameterId, parameterPatch), "updateParameter");
  };

  const addParameter = () => {
    if (!props.onSchemaChange) {
      return;
    }
    const parameterId = createStableId("param");
    const nextParameter = {
      id: parameterId,
      stableId: parameterId,
      name: `Parameter${startParameters.length + 1}`,
      dataType: { kind: "string" as const },
      type: { kind: "primitive" as const, name: "string" },
      required: true,
    };
    props.onSchemaChange(upsertMicroflowParameter(props.schema, nextParameter), "addParameter");
    setSelectedParameterId(parameterId);
  };

  const removeParameter = (parameterId: string) => {
    if (!props.onSchemaChange) {
      return;
    }
    props.onSchemaChange(removeMicroflowParameter(props.schema, parameterId), "removeParameter");
  };

  if (object.kind === "startEvent") {
    const parameterNameWarning = selectedParameter ? getParameterNameWarning(props.schema, selectedParameter.id, selectedParameter.name) : undefined;
    const canEditParameter = Boolean(selectedParameter && props.onSchemaChange && !props.readonly);
    return (
      <>
        <Field label="Input Parameters">
          <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
            {startParameters.map(parameter => (
              <div key={parameter.id} style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) auto", gap: 8, width: "100%" }}>
                <Button
                  theme={parameter.id === selectedParameterId ? "solid" : "light"}
                  type={parameter.id === selectedParameterId ? "primary" : "tertiary"}
                  onClick={() => setSelectedParameterId(parameter.id)}
                  disabled={props.readonly}
                  style={{ justifyContent: "flex-start" }}
                >
                  {parameter.name || "(missing)"} [{parameter.dataType.kind}]
                </Button>
                {withDisabledReason(
                  readonlyDisabledReason,
                  "Delete parameter",
                  <Button
                    type="danger"
                    theme="borderless"
                    disabled={props.readonly}
                    onClick={() => removeParameter(parameter.id)}
                  >
                    Delete
                  </Button>,
                )}
              </div>
            ))}
            {withDisabledReason(
              readonlyDisabledReason,
              "Add parameter",
              <Button disabled={props.readonly} onClick={addParameter}>Add parameter</Button>,
            )}
          </Space>
          {startParameters.length === 0 ? <Text type="warning" size="small">No input parameters defined.</Text> : null}
        </Field>
        <Field label="Parameter Name">
          <Input
            value={selectedParameter?.name ?? ""}
            disabled={!canEditParameter}
            onChange={name => {
              if (!selectedParameter) {
                return;
              }
              patchParameter(selectedParameter.id, { name });
            }}
          />
          {parameterNameWarning ? <Text type="warning" size="small">{parameterNameWarning}</Text> : null}
        </Field>
        <Field label="Data Type">
          <DataTypeSelector
            value={selectedParameter?.dataType ?? { kind: "unknown", reason: "missing parameter type" }}
            disabled={!canEditParameter}
            allowVoid={false}
            onChange={dataType => {
              if (!selectedParameter) {
                return;
              }
              patchParameter(selectedParameter.id, { dataType });
            }}
          />
        </Field>
        <Field label="Required">
          <Switch
            checked={Boolean(selectedParameter?.required)}
            disabled={!canEditParameter}
            onChange={required => {
              if (!selectedParameter) {
                return;
              }
              patchParameter(selectedParameter.id, { required });
            }}
          />
        </Field>
      </>
    );
  }
  if (object.kind === "endEvent") {
    return (
      <>
        <Field label="Return Type">
          {withDisabledReason(
            readonlyDisabledReason,
            "Return type",
            <DataTypeSelector
              value={props.schema.returnType}
              disabled={props.readonly}
              onChange={returnType => {
                props.onSchemaChange?.(updateMicroflowReturnType(props.schema, returnType), "updateReturnType");
              }}
            />
          )}
          <Text type="tertiary" size="small">Return type is defined at microflow level and shared by all End Events.</Text>
        </Field>
        <Field label="Return Value">
          <ExpressionEditor
            value={object.returnValue}
            schema={props.schema}
            metadata={metadata}
            variableIndex={variableIndex}
            objectId={object.id}
            fieldPath="returnValue"
            expectedType={props.schema.returnType}
            required={props.schema.returnType.kind !== "void"}
            readonly={props.readonly || props.schema.returnType.kind === "void"}
            onChange={returnValue => {
              if (props.onSchemaChange) {
                props.onSchemaChange(updateEndEventReturnValue(props.schema, object.id, returnValue), "updateEndReturnValue");
                return;
              }
              patch({ ...object, returnValue });
            }}
          />
          <FieldError issues={getIssuesForField(issues, "returnValue")} />
        </Field>
        {endCount > 1 ? <Text type="warning" size="small">Multiple End Events share the same return type.</Text> : null}
      </>
    );
  }
  if (object.kind === "errorEvent") {
    const legalStateMessages = uniqueMessages(issues.filter(issue => issue.code.startsWith("MF_ERROR_EVENT")).map(issue => issue.message));
    return (
      <>
        <Field label="Incoming Flows">
          <TextArea value={incomingSummary || "No incoming flow"} autosize disabled />
        </Field>
        <Field label="Outgoing Flows">
          <TextArea value={outgoingSummary || "No outgoing flow"} autosize disabled />
        </Field>
        <Field label="Error Variable">
          <Input value={object.error.sourceVariableName} disabled />
          <Text type="tertiary" size="small">Error Event only works at the end of an error handler flow and typically rethrows $latestError to the caller.</Text>
        </Field>
        <Field label="Message Expression">
          <ExpressionEditor
            value={object.error.messageExpression ?? expression("", { kind: "string" })}
            schema={props.schema}
            metadata={metadata}
            variableIndex={variableIndex}
            objectId={object.id}
            fieldPath="error.messageExpression"
            expectedType={{ kind: "string" }}
            readonly={props.readonly}
            onChange={messageExpression => patch({ ...object, error: { ...object.error, messageExpression } })}
          />
          <FieldError issues={getIssuesForField(issues, "error.messageExpression")} />
          <Text type="tertiary" size="small">If provided, this message becomes the rethrown error summary. Triggering Error Event rolls back the transaction.</Text>
        </Field>
        <Field label="Legal State">
          {renderLegalState(
            legalStateMessages,
            "Valid when reached only by an error handler SequenceFlow and used as a terminal rethrow node.",
          )}
        </Field>
      </>
    );
  }
  if (object.kind === "breakEvent" || object.kind === "continueEvent") {
    const loopObjects = collectLoopObjects(props.schema);
    const warnings = getBreakContinueWarnings(props.schema, object.id);
    const legalStateMessages = uniqueMessages([
      ...issues
        .filter(issue => issue.code.startsWith(object.kind === "breakEvent" ? "MF_BREAK_" : "MF_CONTINUE_"))
        .map(issue => issue.message),
      ...warnings,
    ]);
    const outgoingSummaryForControl = flows.filter(flow => flow.originObjectId === object.id).map(flow => `${flow.id}: ${flow.destinationObjectId}`).join("\n");
    return (
      <>
        <Field label="Incoming Flows">
          <TextArea value={incomingSummary || "No incoming flow"} autosize disabled />
        </Field>
        <Field label="Outgoing Flows">
          <TextArea value={outgoingSummaryForControl || "No outgoing flow"} autosize disabled />
        </Field>
        <Field label="Target Loop">
          {withDisabledReason(
            props.readonly ? "Readonly mode cannot edit event settings." : (loopObjects.length === 0 ? "No loop nodes available in this microflow." : ""),
            "Target loop",
            <Select
              value={object.targetLoopObjectId}
              disabled={props.readonly || loopObjects.length === 0}
              showClear
              style={{ width: "100%" }}
              placeholder={loopObjects.length === 1 ? `Implicit: ${loopObjects[0].caption ?? loopObjects[0].id}` : "Select target loop"}
              onClear={() => patch({ ...object, targetLoopObjectId: undefined })}
              onChange={targetLoopObjectId => patch({ ...object, targetLoopObjectId: targetLoopObjectId ? String(targetLoopObjectId) : undefined })}
              optionList={loopObjects.map(loop => ({ label: loop.caption ?? loop.id, value: loop.id }))}
            />
          )}
        </Field>
        <Field label="Legal State">
          {renderLegalState(
            legalStateMessages,
            `${object.kind === "breakEvent" ? "Break" : "Continue"} Event must resolve to a valid Loop body target and should not emit normal outgoing SequenceFlows.`,
          )}
        </Field>
      </>
    );
  }
  return null;
}
