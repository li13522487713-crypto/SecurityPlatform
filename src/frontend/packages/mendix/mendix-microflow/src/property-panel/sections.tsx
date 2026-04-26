import { Button, Checkbox, Collapse, Input, Select, Space, Switch, TextArea, Typography } from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import type { LegacyMicroflowNode, MicroflowNodeAdvancedConfig, MicroflowNodeDocumentation, MicroflowNodeOutput, MicroflowObject, MicroflowTypeRef } from "../schema";
import { flattenObjectCollection } from "../adapters";
import { FieldRow, primitiveType } from "./controls";
import type { MicroflowNodeFormProps } from "./types";

const { Text } = Typography;

export function patchConfig<T extends object>(props: MicroflowNodeFormProps, configPatch: Partial<T> & Record<string, unknown>) {
  props.onPatch({ config: configPatch });
}

export function MicroflowBasicSection({ props }: { props: MicroflowNodeFormProps }) {
  const { node: rawNode, readonly, onPatch } = props;
  const node = rawNode as unknown as LegacyMicroflowNode;
  const typeLabel = node.type === "activity" ? node.config.activityType : node.type;
  return (
    <Collapse defaultActiveKey={["basic"]} style={{ width: "100%" }}>
      <Collapse.Panel itemKey="basic" header="Basic information">
        <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
          <FieldRow label="Node name" required error={!node.title.trim() ? "Node name is required." : undefined}>
            <Input readonly={readonly} value={node.title} onChange={title => onPatch({ node: { title } })} />
          </FieldRow>
          <FieldRow label="Alias">
            <Input readonly={readonly} value={node.alias ?? ""} onChange={alias => onPatch({ node: { alias } })} />
          </FieldRow>
          <FieldRow label="Description">
            <TextArea autosize readonly={readonly} value={node.description ?? ""} onChange={description => onPatch({ node: { description } })} />
          </FieldRow>
          <FieldRow label="Activity type">
            <Input readonly value={typeLabel} />
          </FieldRow>
          <FieldRow label="Enabled">
            <Switch disabled={readonly} checked={node.enabled !== false} onChange={enabled => onPatch({ node: { enabled } })} />
          </FieldRow>
          <FieldRow label="Tags">
            <Select
              multiple
              disabled={readonly}
              style={{ width: "100%" }}
              value={node.tags ?? []}
              placeholder="Add labels"
              optionList={(node.tags ?? []).map(tag => ({ label: tag, value: tag }))}
              onChange={selected => onPatch({ node: { tags: Array.isArray(selected) ? selected.map(String) : [] } })}
            />
          </FieldRow>
        </Space>
      </Collapse.Panel>
    </Collapse>
  );
}

export function MicroflowDocumentationSection({ props }: { props: MicroflowNodeFormProps }) {
  const n = props.node as unknown as LegacyMicroflowNode;
  const rawDoc = n.documentation;
  const doc: MicroflowNodeDocumentation =
    rawDoc && typeof rawDoc === "object" && !Array.isArray(rawDoc)
      ? (rawDoc as MicroflowNodeDocumentation)
      : typeof rawDoc === "string"
        ? { summary: rawDoc }
        : {};
  const update = (patch: MicroflowNodeDocumentation) => props.onPatch({ documentation: { ...doc, ...patch } });
  return (
    <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
      <FieldRow label="Business description">
        <TextArea autosize readonly={props.readonly} value={doc.business ?? ""} onChange={business => update({ business })} />
      </FieldRow>
      <FieldRow label="Technical notes">
        <TextArea autosize readonly={props.readonly} value={doc.technical ?? ""} onChange={technical => update({ technical })} />
      </FieldRow>
      <FieldRow label="Input description">
        <TextArea autosize readonly={props.readonly} value={doc.inputs ?? ""} onChange={inputs => update({ inputs })} />
      </FieldRow>
      <FieldRow label="Output description">
        <TextArea autosize readonly={props.readonly} value={doc.outputs ?? ""} onChange={outputs => update({ outputs })} />
      </FieldRow>
      <FieldRow label="Notes">
        <TextArea autosize readonly={props.readonly} value={doc.notes ?? ""} onChange={notes => update({ notes })} />
      </FieldRow>
      <FieldRow label="Example">
        <TextArea autosize readonly={props.readonly} value={doc.example ?? ""} onChange={example => update({ example })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowErrorHandlingSection({ props }: { props: MicroflowNodeFormProps }) {
  const n = props.node as unknown as LegacyMicroflowNode;
  if (n.type !== "activity") {
    return <Text type="tertiary">This node does not expose error handling.</Text>;
  }
  const config = n.config;
  const errorHandling = config.errorHandling ?? { mode: "rollback" as const, errorVariableName: "latestError" };
  return (
    <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
      <FieldRow label="Error handling mode" required>
        <Select
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={errorHandling.mode}
          optionList={[
            { label: "rollback", value: "rollback" },
            { label: "customWithRollback", value: "customWithRollback" },
            { label: "customWithoutRollback", value: "customWithoutRollback" },
            { label: "continue", value: "continue" }
          ]}
          onChange={mode => patchConfig(props, { errorHandling: { ...errorHandling, mode: String(mode) } })}
        />
      </FieldRow>
      <FieldRow label="Custom microflow">
        <Input readonly={props.readonly} value={config.customErrorMicroflowId ?? ""} onChange={customErrorMicroflowId => patchConfig(props, { customErrorMicroflowId })} />
      </FieldRow>
      <FieldRow label="Error variable">
        <Input
          readonly={props.readonly}
          value={errorHandling.errorVariableName ?? "latestError"}
          onChange={errorVariableName => patchConfig(props, { errorHandling: { ...errorHandling, errorVariableName } })}
        />
      </FieldRow>
      <FieldRow label="Log error">
        <Switch disabled={props.readonly} checked={config.errorLogEnabled ?? true} onChange={errorLogEnabled => patchConfig(props, { errorLogEnabled })} />
      </FieldRow>
      <FieldRow label="Target node after error">
        <Select
          filter
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={errorHandling.targetNodeId}
          placeholder="Select node"
          optionList={flattenObjectCollection(props.schema.objectCollection).filter(object => object.id !== props.object.id).map(object => ({ label: object.caption ?? object.id, value: object.id }))}
          onChange={targetNodeId => patchConfig(props, { errorHandling: { ...errorHandling, targetNodeId: String(targetNodeId ?? "") } })}
        />
      </FieldRow>
      <FieldRow label="Description">
        <TextArea autosize readonly={props.readonly} value={config.errorDescription ?? ""} onChange={errorDescription => patchConfig(props, { errorDescription })} />
      </FieldRow>
    </Space>
  );
}

function outputTypeName(type?: MicroflowTypeRef): string {
  return type?.name ?? "Unknown";
}

export function MicroflowOutputSection({ props }: { props: MicroflowNodeFormProps }) {
  const n = props.node as unknown as LegacyMicroflowNode;
  const outputs: MicroflowNodeOutput[] = n.outputs ?? inferNodeOutputs({ ...props, node: n as unknown as MicroflowObject });
  const updateOutputs = (nextOutputs: MicroflowNodeOutput[]) => props.onPatch({ outputs: nextOutputs });
  return (
    <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
      {outputs.map((output, index) => (
        <div key={output.id} style={{ display: "grid", gridTemplateColumns: "1fr 92px 72px auto", gap: 6, width: "100%" }}>
          <Input readonly={props.readonly} value={output.name} placeholder="Variable" onChange={name => updateOutputs(outputs.map((item, itemIndex) => itemIndex === index ? { ...item, name } : item))} />
          <Input readonly value={outputTypeName(output.type)} />
          <Input readonly value={output.source} />
          <Button disabled={props.readonly} type="danger" theme="borderless" onClick={() => updateOutputs(outputs.filter((_, itemIndex) => itemIndex !== index))}>Delete</Button>
        </div>
      ))}
      <Button
        disabled={props.readonly}
        icon={<IconPlus />}
        onClick={() => updateOutputs([...outputs, {
          id: `output-${Date.now()}`,
          name: "result",
          type: primitiveType("String"),
          source: "Manual",
          downstreamAvailable: true
        }])}
      >
        Add output variable
      </Button>
    </Space>
  );
}

export function MicroflowAdvancedSection({ props }: { props: MicroflowNodeFormProps }) {
  const n = props.node as unknown as LegacyMicroflowNode;
  const advanced: MicroflowNodeAdvancedConfig = n.advanced ?? {};
  const update = (patch: MicroflowNodeAdvancedConfig) => props.onPatch({ advanced: { ...advanced, ...patch } });
  return (
    <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
      <FieldRow label="Execution timeout (ms)">
        <Input readonly={props.readonly} value={String(advanced.timeoutMs ?? "")} onChange={timeoutMs => update({ timeoutMs: Number(timeoutMs) || undefined })} />
      </FieldRow>
      <FieldRow label="Enable retry">
        <Switch disabled={props.readonly} checked={Boolean(advanced.retryEnabled)} onChange={retryEnabled => update({ retryEnabled })} />
      </FieldRow>
      <FieldRow label="Retry count">
        <Input readonly={props.readonly} value={String(advanced.retryCount ?? "")} onChange={retryCount => update({ retryCount: Number(retryCount) || undefined })} />
      </FieldRow>
      <FieldRow label="Retry interval (ms)">
        <Input readonly={props.readonly} value={String(advanced.retryIntervalMs ?? "")} onChange={retryIntervalMs => update({ retryIntervalMs: Number(retryIntervalMs) || undefined })} />
      </FieldRow>
      <FieldRow label="Verbose logging">
        <Switch disabled={props.readonly} checked={Boolean(advanced.verboseLogging)} onChange={verboseLogging => update({ verboseLogging })} />
      </FieldRow>
      <FieldRow label="Ignore non-critical errors">
        <Checkbox disabled={props.readonly} checked={Boolean(advanced.ignoreNonCriticalErrors)} onChange={event => update({ ignoreNonCriticalErrors: Boolean(event.target.checked) })} />
      </FieldRow>
      <FieldRow label="Permission context">
        <Input readonly={props.readonly} value={advanced.permissionContext ?? ""} onChange={permissionContext => update({ permissionContext })} />
      </FieldRow>
      <FieldRow label="Transaction boundary">
        <Select
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={advanced.transactionBoundary ?? "inherit"}
          optionList={[
            { label: "Inherit", value: "inherit" },
            { label: "Requires new", value: "requiresNew" },
            { label: "None", value: "none" }
          ]}
          onChange={transactionBoundary => update({ transactionBoundary: String(transactionBoundary) as MicroflowNodeAdvancedConfig["transactionBoundary"] })}
        />
      </FieldRow>
      <FieldRow label="Performance tag">
        <Input readonly={props.readonly} value={advanced.performanceTag ?? ""} onChange={performanceTag => update({ performanceTag })} />
      </FieldRow>
      {n.type === "activity" && n.config.activityType === "callRest" ? (
        <FieldRow label="Follow redirects">
          <Switch disabled={props.readonly} checked={advanced.followRedirects ?? true} onChange={followRedirects => update({ followRedirects })} />
        </FieldRow>
      ) : null}
    </Space>
  );
}

function inferNodeOutputs(props: MicroflowNodeFormProps): MicroflowNodeOutput[] {
  const { node: raw } = props;
  const node = raw as unknown as LegacyMicroflowNode;
  if (node.type === "activity") {
    if (node.config.activityType === "objectRetrieve" && node.config.listVariableName) {
      return [{ id: `${node.id}-retrieve`, name: node.config.listVariableName, type: { kind: "list", name: node.config.entity ?? "Object" }, source: "Retrieve result", downstreamAvailable: true }];
    }
    if (node.config.resultVariableName) {
      return [{ id: `${node.id}-result`, name: node.config.resultVariableName, type: primitiveType("Object"), source: node.config.activityType, downstreamAvailable: true }];
    }
  }
  if (node.type === "decision") {
    return [
      { id: `${node.id}-true`, name: "true", type: primitiveType("Branch"), source: "Decision", downstreamAvailable: true },
      { id: `${node.id}-false`, name: "false", type: primitiveType("Branch"), source: "Decision", downstreamAvailable: true }
    ];
  }
  return [];
}
