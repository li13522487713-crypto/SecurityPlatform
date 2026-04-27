import { useEffect, useMemo, useState } from "react";
import { Input, InputNumber, Select, Switch, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowFlow, MicroflowSequenceFlow } from "../../schema";
import type { MicroflowPropertyTabKey } from "../../schema/types";
import { getCaseEditorKind, getCaseOptionsForSource, caseValueKey } from "../../flowgram/adapters/flowgram-case-options";
import { useMetadataStatus, useMicroflowMetadataCatalog } from "../../metadata";
import { ValidationIssueList } from "../common";
import type { MicroflowEdgePatch, MicroflowPropertyPanelProps } from "../types";
import { Header, PropertyTabs, Field, flowPatch, getFlowTabs, issuesFor, objectName } from "../panel-shared";

const { Text } = Typography;

function CaseValueField({ props, flow, patch }: {
  props: MicroflowPropertyPanelProps;
  flow: MicroflowSequenceFlow;
  patch: (next: MicroflowFlow) => void;
}) {
  const catalog = useMicroflowMetadataCatalog();
  const { loading: metadataLoading, error: metadataError } = useMetadataStatus();
  const caseKind = getCaseEditorKind(props.schema, flow.originObjectId);
  if (!caseKind || flow.isErrorHandler || flow.editor.edgeKind === "sequence") {
    return (
      <Field label="Case Values">
        <TextArea
          autosize
          disabled={props.readonly}
          value={flow.caseValues.map(item => item.kind === "boolean" ? item.persistedValue : item.kind === "enumeration" ? item.value : item.kind === "inheritance" ? item.entityQualifiedName : item.kind).join("\n")}
          onChange={value => patch(flowPatch(flow, {
            caseValues: value.split("\n").map(item => item.trim()).filter(Boolean).map(item => item === "true" || item === "false"
              ? { kind: "boolean" as const, officialType: "Microflows$EnumerationCase" as const, value: item === "true", persistedValue: item as "true" | "false" }
              : { kind: "enumeration" as const, officialType: "Microflows$EnumerationCase" as const, enumerationQualifiedName: "", value: item })
          }))}
        />
      </Field>
    );
  }
  if (metadataError) {
    return <Text type="danger">元数据加载失败，无法配置分支条件。</Text>;
  }
  if (metadataLoading && !catalog) {
    return <Text type="tertiary">元数据加载中…</Text>;
  }
  if (!catalog) {
    return <Text type="warning">元数据未加载</Text>;
  }
  const options = getCaseOptionsForSource(props.schema, flow.originObjectId, flow.id, catalog);
  const current = flow.caseValues[0];
  const currentKey = current ? caseValueKey(current) : undefined;
  return (
    <Field label={caseKind === "enumeration" ? "Enumeration Case" : caseKind === "objectType" ? "Object Type Case" : "Boolean Case"}>
      <Select
        value={currentKey}
        disabled={props.readonly}
        style={{ width: "100%" }}
        onChange={value => {
          const option = options.find(item => item.key === String(value));
          if (!option) {
            return;
          }
          patch(flowPatch(flow, {
            caseValues: [option.caseValue],
            editor: {
              ...flow.editor,
              edgeKind: caseKind === "objectType" ? "objectTypeCondition" : "decisionCondition",
              label: option.label,
            },
          }));
        }}
        optionList={options.map(option => ({
          label: option.reason ? `${option.label} - ${option.reason}` : option.label,
          value: option.key,
          disabled: option.disabled,
        }))}
      />
    </Field>
  );
}

export function FlowEdgeForm(props: MicroflowPropertyPanelProps) {
  const flow = props.selectedFlow;
  if (!flow) {
    return null;
  }
  const tabs = useMemo(() => getFlowTabs(flow), [flow]);
  const [activeTab, setActiveTab] = useState<MicroflowPropertyTabKey>(tabs[0] ?? "properties");
  useEffect(() => {
    setActiveTab(tabs[0] ?? "properties");
  }, [flow.id, tabs]);
  const issues = issuesFor(props, undefined, flow.id);
  const patch = (next: MicroflowFlow) => props.onFlowChange?.(flow.id, next);
  return (
    <>
      <Header
        props={props}
        title={flow.kind === "sequence" ? flow.editor.label ?? "Sequence Flow" : flow.editor.label ?? "Annotation Flow"}
        subtitle={flow.officialType}
        onDelete={() => props.onDeleteFlow?.(flow.id)}
      />
      <PropertyTabs tabs={tabs} activeKey={activeTab} onChange={setActiveTab} />
      <div style={{ padding: 14, display: "grid", gap: 12 }}>
        <ValidationIssueList issues={issues} />
        {activeTab === "properties" ? (
          <>
        <Field label="Flow ID">
          <Input value={flow.id} disabled />
        </Field>
        <Field label="Flow Kind">
          <Input value={flow.kind} disabled />
        </Field>
        <Field label="Official Type">
          <Input value={flow.officialType} disabled />
        </Field>
        <Field label="Origin Object">
          <Input value={objectName(props.schema, flow.originObjectId)} disabled />
        </Field>
        <Field label="Destination Object">
          <Input value={objectName(props.schema, flow.destinationObjectId)} disabled />
        </Field>
        <Field label="Runtime Effect">
          <Input value={flow.kind === "annotation" ? "annotationOnly" : flow.kind === "sequence" && flow.isErrorHandler ? "errorFlow" : "controlFlow"} disabled />
        </Field>
        <Field label="Origin Connection Index">
          <InputNumber value={flow.originConnectionIndex ?? 0} disabled />
        </Field>
        <Field label="Destination Connection Index">
          <InputNumber value={flow.destinationConnectionIndex ?? 0} disabled />
        </Field>
        <Field label="Line Routing">
          <Select
            value={flow.line.kind}
            disabled={props.readonly}
            style={{ width: "100%" }}
            onChange={kind => patch(flowPatch(flow, { line: { ...flow.line, kind: String(kind) as typeof flow.line.kind } }))}
            optionList={["orthogonal", "polyline", "bezier"].map(value => ({ label: value, value }))}
          />
        </Field>
        {flow.kind === "sequence" ? (
          <>
            <Field label="Editor Edge Kind">
              <Input value={flow.editor.edgeKind} disabled />
            </Field>
            <Field label="Error Handler">
              <Switch checked={flow.isErrorHandler} disabled={props.readonly} onChange={isErrorHandler => patch(flowPatch(flow, { isErrorHandler, editor: { ...flow.editor, edgeKind: isErrorHandler ? "errorHandler" : flow.editor.edgeKind } }))} />
            </Field>
            {flow.isErrorHandler ? (
              <>
                <Field label="Expose latestError">
                  <Switch checked={flow.exposeLatestError ?? true} disabled={props.readonly} onChange={exposeLatestError => patch(flowPatch(flow, { exposeLatestError }))} />
                </Field>
                <Field label="Expose latestHttpResponse">
                  <Switch checked={Boolean(flow.exposeLatestHttpResponse)} disabled={props.readonly} onChange={exposeLatestHttpResponse => patch(flowPatch(flow, { exposeLatestHttpResponse }))} />
                </Field>
                <Field label="Expose latestSoapFault">
                  <Switch checked={Boolean(flow.exposeLatestSoapFault)} disabled={props.readonly} onChange={exposeLatestSoapFault => patch(flowPatch(flow, { exposeLatestSoapFault }))} />
                </Field>
                <Field label="Error variable name">
                  <Input value={flow.targetErrorVariableName ?? ""} disabled={props.readonly} onChange={targetErrorVariableName => patch(flowPatch(flow, { targetErrorVariableName }))} />
                </Field>
              </>
            ) : null}
            <CaseValueField props={props} flow={flow} patch={patch} />
            <Field label="Branch Order">
              <InputNumber value={flow.editor.branchOrder ?? 0} disabled={props.readonly} onChange={branchOrder => patch(flowPatch(flow, { editor: { ...flow.editor, branchOrder: Number(branchOrder) } }))} />
            </Field>
            <Field label="Label">
              <Input value={flow.editor.label ?? ""} disabled={props.readonly} onChange={label => patch(flowPatch(flow, { editor: { ...flow.editor, label } } as MicroflowEdgePatch))} />
            </Field>
            <Field label="Description">
              <TextArea value={flow.editor.description ?? ""} autosize disabled={props.readonly} onChange={description => patch(flowPatch(flow, { editor: { ...flow.editor, description } } as MicroflowEdgePatch))} />
            </Field>
          </>
        ) : (
          <>
            <Field label="Show In Export">
              <Switch checked={flow.editor.showInExport} disabled={props.readonly} onChange={showInExport => patch(flowPatch(flow, { editor: { ...flow.editor, showInExport } }))} />
            </Field>
            <Field label="Attachment Mode">
              <Select
                value={flow.attachmentMode ?? "edge"}
                disabled={props.readonly}
                style={{ width: "100%" }}
                onChange={attachmentMode => patch(flowPatch(flow, { attachmentMode: String(attachmentMode) as "node" | "edge" | "canvas" }))}
                optionList={["node", "edge", "canvas"].map(value => ({ label: value, value }))}
              />
            </Field>
            <Field label="Label">
              <Input value={flow.editor.label ?? ""} disabled={props.readonly} onChange={label => patch(flowPatch(flow, { editor: { ...flow.editor, label } } as MicroflowEdgePatch))} />
            </Field>
            <Field label="Description">
              <TextArea value={flow.editor.description ?? ""} autosize disabled={props.readonly} onChange={description => patch(flowPatch(flow, { editor: { ...flow.editor, description } } as MicroflowEdgePatch))} />
            </Field>
          </>
        )}
          </>
        ) : null}
        {activeTab === "documentation" ? (
          <>
            <Field label="Label">
              <Input value={flow.editor.label ?? ""} disabled={props.readonly} onChange={label => patch(flowPatch(flow, { editor: { ...flow.editor, label } } as MicroflowEdgePatch))} />
            </Field>
            <Field label="Description">
              <TextArea value={flow.editor.description ?? ""} autosize disabled={props.readonly} onChange={description => patch(flowPatch(flow, { editor: { ...flow.editor, description } } as MicroflowEdgePatch))} />
            </Field>
          </>
        ) : null}
        {activeTab === "errorHandling" && flow.kind === "sequence" ? (
          <>
            <Field label="Error Handler">
              <Switch checked={flow.isErrorHandler} disabled={props.readonly} onChange={isErrorHandler => patch(flowPatch(flow, { isErrorHandler, editor: { ...flow.editor, edgeKind: isErrorHandler ? "errorHandler" : "sequence" } }))} />
            </Field>
            <Field label="Expose latestError">
              <Switch checked={flow.exposeLatestError ?? true} disabled={props.readonly} onChange={exposeLatestError => patch(flowPatch(flow, { exposeLatestError }))} />
            </Field>
            <Field label="Error variable name">
              <Input value={flow.targetErrorVariableName ?? "$latestError"} disabled={props.readonly} onChange={targetErrorVariableName => patch(flowPatch(flow, { targetErrorVariableName }))} />
            </Field>
          </>
        ) : null}
      </div>
    </>
  );
}
