import { useEffect, useMemo, useState } from "react";
import { Input, Select, Space, Switch, Tag, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowAction, MicroflowObject, MicroflowParameter, MicroflowVariableIndex, MicroflowVariableSymbol } from "../../schema";
import type { MicroflowPropertyTabKey } from "../../schema/types";
import { EMPTY_MICROFLOW_METADATA_CATALOG, useMetadataStatus, useMicroflowMetadataCatalog } from "../../metadata";
import { buildVariableIndex, getOutputVariablesForObject, variableSourceLabel } from "../../variables";
import { collectFlowsRecursive } from "../../schema/utils/object-utils";
import { ValidationIssueList } from "../common";
import type { MicroflowPropertyPanelProps } from "../types";
import {
  dataTypeLabel,
  Field,
  getObjectTabs,
  Header,
  issuesFor,
  objectTitle,
  PropertyTabs,
  updateAction,
  updateObjectAdvanced,
  updateObjectDocumentation,
} from "../panel-shared";
import { ActionActivityForm } from "./action-activity-form";
import { AnnotationObjectForm } from "./annotation-object-form";
import { EventNodesForm } from "./event-nodes-form";
import { ExclusiveSplitForm } from "./exclusive-split-form";
import { genericOutputSummary } from "./generic-action-fields-form";
import { InheritanceSplitForm } from "./inheritance-split-form";
import { LoopNodeForm } from "./loop-node-form";
import { MergeNodeForm } from "./merge-node-form";
import { ObjectBaseForm } from "./object-base-form";
import { ParameterObjectForm } from "./parameter-object-form";

const { Text } = Typography;

function p0OutputSummary(action: MicroflowAction): string {
  if (action.kind === "retrieve") {
    return `${action.outputVariableName || "(missing)"}: inferred retrieve result`;
  }
  if (action.kind === "createObject") {
    return `${action.outputVariableName || "(missing)"}: object:${action.entityQualifiedName || "unknown"}`;
  }
  if (action.kind === "createVariable") {
    return `${action.variableName || "(missing)"}: ${dataTypeLabel(action.dataType)}`;
  }
  if (action.kind === "callMicroflow") {
    return action.returnValue.storeResult ? `${action.returnValue.outputVariableName || "(missing)"}: ${dataTypeLabel(action.returnValue.dataType)}` : "该节点不产生变量";
  }
  if (action.kind === "restCall") {
    const rows: string[] = [];
    if (action.response.handling.kind !== "ignore") {
      rows.push(`${action.response.handling.outputVariableName || "(missing)"}: ${action.response.handling.kind}`);
    }
    if (action.response.statusCodeVariableName) {
      rows.push(`${action.response.statusCodeVariableName}: integer`);
    }
    if (action.response.headersVariableName) {
      rows.push(`${action.response.headersVariableName}: json`);
    }
    return rows.length ? rows.join("\n") : "该节点不产生变量";
  }
  if (action.kind === "logMessage" || action.kind === "commit" || action.kind === "delete" || action.kind === "rollback" || action.kind === "changeMembers" || action.kind === "changeVariable") {
    return "该节点不产生变量";
  }
  return genericOutputSummary(action) ?? "该节点不产生变量";
}

function outputLine(symbol: MicroflowVariableSymbol): string {
  return `${symbol.name}: ${dataTypeLabel(symbol.dataType)} | ${variableSourceLabel(symbol)} | ${symbol.scope.kind ?? "collection"} | ${symbol.visibility ?? "definite"}`;
}

function outputSymbolsForObject(index: MicroflowVariableIndex, object: MicroflowObject, parameter?: MicroflowParameter): MicroflowVariableSymbol[] {
  const direct = getOutputVariablesForObject(index, object.id);
  if (direct.length) {
    return direct;
  }
  if (object.kind === "parameterObject" && parameter) {
    return index.byName?.[parameter.name] ?? [];
  }
  if (object.kind === "loopedActivity") {
    return (index.all ?? []).filter(symbol => symbol.scope.loopObjectId === object.id);
  }
  return [];
}

export function ObjectPanel(props: MicroflowPropertyPanelProps) {
  const object = props.selectedObject;
  if (!object) {
    return null;
  }
  const catalog = useMicroflowMetadataCatalog();
  const { version: metadataVersion } = useMetadataStatus();
  const effectiveCatalog = catalog ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const variableIndex = useMemo(() => buildVariableIndex(props.schema, effectiveCatalog), [props.schema, effectiveCatalog, metadataVersion]);
  const tabs = useMemo(() => getObjectTabs(object), [object]);
  const [activeTab, setActiveTab] = useState<MicroflowPropertyTabKey>(tabs[0] ?? "properties");
  useEffect(() => {
    setActiveTab(tabs[0] ?? "properties");
  }, [object.id, tabs]);
  const issues = issuesFor(props, object.id, undefined, object.kind === "actionActivity" ? object.action.id : undefined);
  const patch = (next: MicroflowObject) => props.onObjectChange(object.id, { object: next });
  const parameter = object.kind === "parameterObject"
    ? props.schema.parameters.find(item => item.id === object.parameterId)
    : undefined;
  return (
    <>
      <Header
        props={props}
        title={objectTitle(object)}
        subtitle={object.officialType}
        onDelete={() => props.onDeleteObject?.(object.id)}
        onDuplicate={() => props.onDuplicateObject?.(object.id)}
      />
      <PropertyTabs tabs={tabs} activeKey={activeTab} onChange={setActiveTab} />
      <div style={{ padding: 14, display: "grid", gap: 12 }}>
        <ValidationIssueList issues={issues} />
        {activeTab === "properties" ? (
          <>
            <ObjectBaseForm object={object} readonly={props.readonly} patch={patch} />
            <EventNodesForm props={props} object={object} issues={issues} metadata={effectiveCatalog} variableIndex={variableIndex} patch={patch} />
            <ExclusiveSplitForm props={props} object={object} issues={issues} metadata={effectiveCatalog} variableIndex={variableIndex} patch={patch} />
            <InheritanceSplitForm props={props} object={object} issues={issues} metadata={effectiveCatalog} patch={patch} />
            <MergeNodeForm props={props} object={object} patch={patch} />
            <LoopNodeForm props={props} object={object} issues={issues} metadata={effectiveCatalog} variableIndex={variableIndex} patch={patch} />
            {object.kind === "actionActivity" ? (
              <ActionActivityForm schema={props.schema} object={object} issues={issues} readonly={props.readonly} onPatch={payload => props.onObjectChange(object.id, payload)} />
            ) : null}
            <ParameterObjectForm props={props} object={object} issues={issues} parameter={parameter as MicroflowParameter | undefined} />
            <AnnotationObjectForm object={object} readonly={props.readonly} patch={patch} />
          </>
        ) : null}
        {activeTab === "documentation" ? (
          <Field label="Documentation">
            <TextArea value={object.documentation ?? ""} autosize disabled={props.readonly} onChange={documentation => patch(updateObjectDocumentation(object, documentation))} />
          </Field>
        ) : null}
        {activeTab === "errorHandling" ? (
          <>
            {object.kind === "actionActivity" ? (
              <Field label="Error Handling Type">
                <Select
                  value={object.action.errorHandlingType}
                  disabled={props.readonly}
                  style={{ width: "100%" }}
                  onChange={errorHandlingType => patch(updateAction(object, { errorHandlingType: String(errorHandlingType) as MicroflowAction["errorHandlingType"] }))}
                  optionList={["rollback", "customWithRollback", "customWithoutRollback", "continue"].map(value => ({ label: value, value }))}
                />
              </Field>
            ) : object.kind === "exclusiveSplit" || object.kind === "inheritanceSplit" || object.kind === "loopedActivity" ? (
              <Field label="Error Handling Type">
                <Select
                  value={object.errorHandlingType}
                  disabled={props.readonly}
                  style={{ width: "100%" }}
                  onChange={errorHandlingType => patch({ ...object, errorHandlingType: String(errorHandlingType) as typeof object.errorHandlingType })}
                  optionList={["rollback", "customWithRollback", "customWithoutRollback", "continue"].map(value => ({ label: value, value }))}
                />
              </Field>
            ) : (
              <Text type="tertiary">This object does not expose error handling.</Text>
            )}
            <Text type="tertiary" size="small">Custom error handler branches are represented by errorHandler flows. latestError is available on custom handlers.</Text>
          </>
        ) : null}
        {activeTab === "output" ? (
          <>
            <Field label="Variable Declarations">
              {outputSymbolsForObject(variableIndex, object, parameter).length ? (
                <Space vertical align="start" spacing={6}>
                  {outputSymbolsForObject(variableIndex, object, parameter).map(symbol => (
                    <Space key={symbol.id ?? `${symbol.name}:${symbol.scope.collectionId}`} align="center" spacing={6}>
                      <Text size="small">{outputLine(symbol)}</Text>
                      {symbol.dataType.kind === "unknown" ? <Tag size="small" color="red">unknown</Tag> : null}
                      {symbol.visibility === "maybe" ? <Tag size="small" color="orange">maybe</Tag> : null}
                    </Space>
                  ))}
                </Space>
              ) : <Text type="tertiary">该节点不产生变量</Text>}
            </Field>
            {object.kind === "actionActivity" ? <Field label="Output Spec"><TextArea value={p0OutputSummary(object.action)} disabled autosize /></Field> : null}
            {object.kind === "parameterObject" ? <Field label="Parameter"><Input value={`${parameter?.name ?? object.parameterName ?? ""}: ${dataTypeLabel(parameter?.dataType)}`} disabled /></Field> : null}
            {object.kind === "loopedActivity" && object.loopSource.kind === "iterableList" ? <Field label="Loop Variables"><Input value={`${object.loopSource.iteratorVariableName}, ${object.loopSource.currentIndexVariableName}`} disabled /></Field> : null}
            {object.kind === "endEvent" ? <Field label="Return Value"><Input value={object.returnValue?.raw ?? ""} disabled /></Field> : null}
            {object.kind === "exclusiveSplit" || object.kind === "inheritanceSplit" ? <Field label="Branch Outputs"><TextArea value={collectFlowsRecursive(props.schema).filter(flow => flow.originObjectId === object.id).map(flow => `${flow.id}: ${flow.kind === "sequence" ? flow.caseValues.map(value => value.kind).join(",") || "pending" : "annotation"}`).join("\n")} disabled autosize /></Field> : null}
          </>
        ) : null}
        {activeTab === "advanced" ? (
          <>
            <Field label="Disabled">
              <Switch checked={Boolean(object.disabled)} disabled={props.readonly} onChange={disabled => patch({ ...object, disabled } as MicroflowObject)} />
            </Field>
            <Field label="Performance Tag">
              <Input value={(object.editor as unknown as { advanced?: { performanceTag?: string } }).advanced?.performanceTag ?? ""} disabled={props.readonly} onChange={performanceTag => patch(updateObjectAdvanced(object, { performanceTag }))} />
            </Field>
            <Field label="Execution Timeout">
              <Input value={String((object.editor as unknown as { advanced?: { timeoutMs?: number } }).advanced?.timeoutMs ?? "")} disabled={props.readonly} onChange={timeoutMs => patch(updateObjectAdvanced(object, { timeoutMs: Number(timeoutMs) || undefined }))} />
            </Field>
            <Field label="Retry Enabled">
              <Switch checked={Boolean((object.editor as unknown as { advanced?: { retryEnabled?: boolean } }).advanced?.retryEnabled)} disabled={props.readonly} onChange={retryEnabled => patch(updateObjectAdvanced(object, { retryEnabled }))} />
            </Field>
          </>
        ) : null}
      </div>
    </>
  );
}
