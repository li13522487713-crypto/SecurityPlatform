import { useEffect, useMemo, useState } from "react";
import { Button, Card, Input, Modal, Select, Space, Switch, Tag, TextArea, Toast, Typography } from "@douyinfe/semi-ui";

import type { MicroflowDataType, MicroflowDesignSchema, MicroflowSchema } from "../schema";
import { buildDefaultRunInputValues, buildRunInputModel, validateRunInputs, type MicroflowRunInputField } from "./run-input-model";
import type { MicroflowRunSession, MicroflowTestRunInput, MicroflowTestRunOptions, MicroflowTestRunSample } from "./trace-types";
import { formatMendixMicroflowTemplate, getMendixMicroflowCopy } from "../i18n/copy";

const { Text } = Typography;

export interface MicroflowTestRunModalProps {
  visible: boolean;
  schema: MicroflowSchema | MicroflowDesignSchema;
  running?: boolean;
  dirty?: boolean;
  validationErrorCount?: number;
  values?: Record<string, unknown>;
  lastSession?: MicroflowRunSession;
  serviceError?: string;
  samples?: MicroflowTestRunSample[];
  onCancel: () => void;
  onValuesChange?: (values: Record<string, unknown>) => void;
  onSaveSample?: (sample: Omit<MicroflowTestRunSample, "id" | "updatedAt"> & { id?: string }) => void;
  onRunAllSamples?: (samples: MicroflowTestRunSample[], options?: MicroflowTestRunOptions) => void | Promise<void>;
  onRun: (input: MicroflowTestRunInput) => void | Promise<void>;
}

export function MicroflowTestRunModal(props: MicroflowTestRunModalProps) {
  const copy = getMendixMicroflowCopy();
  const model = useMemo(() => buildRunInputModel(props.schema), [props.schema]);
  const defaults = useMemo(() => buildDefaultRunInputValues(model), [model]);
  const parameters = props.values ?? defaults;
  const [options, setOptions] = useState<MicroflowTestRunOptions>({ maxSteps: 200 });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [sampleName, setSampleName] = useState("");
  const [expectedResultJson, setExpectedResultJson] = useState("");

  useEffect(() => {
    if (props.visible) {
      setOptions({ maxSteps: 200 });
      setErrors({});
    }
  }, [props.visible]);

  useEffect(() => {
    if (props.visible && props.values === undefined) {
      props.onValuesChange?.(defaults);
    }
  }, [defaults, props.onValuesChange, props.values, props.visible]);

  const run = () => {
    const validation = validateRunInputs(model, parameters);
    setErrors(validation.errors);
    if (!validation.valid) {
      return;
    }
    props.onRun({ parameters: validation.values, options });
  };

  const updateParameter = (name: string, value: unknown) => {
    props.onValuesChange?.({ ...parameters, [name]: value });
  };

  const saveSample = () => {
    const validation = validateRunInputs(model, parameters);
    setErrors(validation.errors);
    if (!validation.valid) {
      return;
    }
    const expected = parseOptionalJson(expectedResultJson);
    if (!expected.ok) {
      Toast.error(copy.testRun.invalidExpectedResult);
      return;
    }
    props.onSaveSample?.({
      name: sampleName.trim() || `${model.microflowId} sample ${(props.samples?.length ?? 0) + 1}`,
      parameters: validation.values,
      expectedResult: expected.value,
    });
    setSampleName("");
  };

  const loadSample = (sample: MicroflowTestRunSample) => {
    props.onValuesChange?.(sample.parameters);
    setSampleName(sample.name);
    setExpectedResultJson(sample.expectedResult === undefined ? "" : JSON.stringify(sample.expectedResult, null, 2));
  };

  const runSample = (sample: MicroflowTestRunSample) => {
    props.onValuesChange?.(sample.parameters);
    void props.onRun({ parameters: sample.parameters, options, sampleId: sample.id });
  };

  const copyTrace = () => {
    if (!props.lastSession) {
      return;
    }
    const payload = JSON.stringify(props.lastSession.trace, null, 2);
    if (typeof navigator !== "undefined" && navigator.clipboard) {
      void navigator.clipboard.writeText(payload).then(() => Toast.success(copy.testRun.traceCopied));
    }
  };

  return (
    <Modal
      title={copy.testRun.title}
      visible={props.visible}
      onCancel={props.onCancel}
      footer={null}
      width={860}
      maskClosable={!props.running}
      data-testid="microflow-test-run-modal"
    >
      <Space vertical align="start" spacing={14} style={{ width: "100%" }}>
        <Space wrap>
          <Tag color="blue">{props.schema.displayName || props.schema.name}</Tag>
          <Tag>{model.microflowId}</Tag>
          <Tag>{model.schemaVersion}</Tag>
          {props.dirty ? <Tag color="orange">{copy.testRun.dirtyTag}</Tag> : <Tag color="green">{copy.testRun.savedTag}</Tag>}
          {props.validationErrorCount ? (
            <Tag color="red">
              {formatMendixMicroflowTemplate(copy.testRun.validationErrorsTag, { count: props.validationErrorCount })}
            </Tag>
          ) : (
            <Tag color="green">{copy.testRun.validationReadyTag}</Tag>
          )}
        </Space>
        <Text type="secondary">
          {copy.testRun.runDescription}
        </Text>
        {model.warnings.map(warning => <Text key={warning} type="warning" size="small">{warning}</Text>)}
        {model.fields.length === 0 ? <Text type="tertiary">{copy.testRun.noInputParameters}</Text> : null}
        {model.fields.map(field => (
          <ParameterField
            key={field.parameter.id}
            field={field}
            value={parameters[field.parameter.name]}
            error={errors[field.parameter.name]}
            onChange={value => updateParameter(field.parameter.name, value)}
          />
        ))}
        <TestSamplesPanel
          samples={props.samples ?? []}
          expectedResultJson={expectedResultJson}
          sampleName={sampleName}
          running={props.running}
          onExpectedResultJsonChange={setExpectedResultJson}
          onSampleNameChange={setSampleName}
          onSave={saveSample}
          onLoad={loadSample}
          onRun={runSample}
          onRunAll={() => void props.onRunAllSamples?.(props.samples ?? [], options)}
        />
        <div style={{ width: "100%", borderTop: "1px solid var(--semi-color-border)", paddingTop: 12 }}>
          <Text strong>{copy.testRun.runtimeOptions}</Text>
          <Space vertical align="start" spacing={10} style={{ width: "100%", marginTop: 10 }}>
            <label style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <Switch checked={Boolean(options.allowRealHttp)} onChange={allowRealHttp => setOptions(current => ({ ...current, allowRealHttp }))} />
              <Text>{copy.testRun.allowRealHttp}</Text>
              <Text size="small" type="tertiary">{copy.testRun.allowRealHttpHint}</Text>
            </label>
            <label style={{ display: "grid", gap: 6, width: 220 }}>
              <Text size="small" strong>{copy.testRun.maxSteps}</Text>
              <Input value={String(options.maxSteps ?? 200)} onChange={value => setOptions(current => ({ ...current, maxSteps: Number.isFinite(Number(value)) ? Number(value) : 200 }))} />
            </label>
          </Space>
        </div>
        <RunResultPreview session={props.lastSession} serviceError={props.serviceError} />
        <Space style={{ width: "100%", justifyContent: "flex-end" }}>
          <Button onClick={copyTrace} disabled={!props.lastSession}>{copy.testRun.copyTrace}</Button>
          <Button data-testid="microflow-test-run-cancel" onClick={props.onCancel} disabled={props.running}>{copy.testRun.cancel}</Button>
          <Button data-testid="microflow-test-run-submit" type="primary" loading={props.running} onClick={run}>{props.dirty ? copy.testRun.saveAndRun : copy.testRun.run}</Button>
        </Space>
      </Space>
    </Modal>
  );
}

function TestSamplesPanel(props: {
  samples: MicroflowTestRunSample[];
  sampleName: string;
  expectedResultJson: string;
  running?: boolean;
  onSampleNameChange: (value: string) => void;
  onExpectedResultJsonChange: (value: string) => void;
  onSave: () => void;
  onLoad: (sample: MicroflowTestRunSample) => void;
  onRun: (sample: MicroflowTestRunSample) => void;
  onRunAll: () => void;
}) {
  const copy = getMendixMicroflowCopy();
  return (
    <div style={{ width: "100%", borderTop: "1px solid var(--semi-color-border)", paddingTop: 12 }}>
      <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
        <Space style={{ width: "100%", justifyContent: "space-between" }} align="center">
          <Text strong>{copy.testRun.samplesTitle}</Text>
          <Button size="small" disabled={props.running || props.samples.length === 0} onClick={props.onRunAll}>{copy.testRun.runAllSamples}</Button>
        </Space>
        <Space align="start" style={{ width: "100%" }}>
          <Input
            value={props.sampleName}
            placeholder={copy.testRun.sampleNamePlaceholder}
            onChange={props.onSampleNameChange}
            style={{ width: 180 }}
          />
          <TextArea
            autosize
            value={props.expectedResultJson}
            placeholder={copy.testRun.expectedResultPlaceholder}
            onChange={props.onExpectedResultJsonChange}
            style={{ flex: 1 }}
          />
          <Button type="secondary" disabled={props.running} onClick={props.onSave}>{copy.testRun.saveSample}</Button>
        </Space>
        {props.samples.length === 0 ? <Text type="tertiary">{copy.testRun.noSamples}</Text> : null}
        {props.samples.map(sample => (
          <Card key={sample.id} style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
            <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
              <Space style={{ width: "100%", justifyContent: "space-between" }} align="center">
                <Space wrap>
                  <Text strong>{sample.name}</Text>
                  {sample.lastStatus ? <Tag color={sample.lastStatus === "success" ? "green" : "red"}>{sample.lastStatus}</Tag> : null}
                  {sample.lastResult !== undefined && sample.expectedResult !== undefined
                    ? <Tag color={stableJson(sample.lastResult) === stableJson(sample.expectedResult) ? "green" : "red"}>
                      {stableJson(sample.lastResult) === stableJson(sample.expectedResult) ? copy.testRun.matchTag : copy.testRun.mismatchTag}
                    </Tag>
                    : null}
                  {sample.lastRunId ? <Tag>{sample.lastRunId}</Tag> : null}
                </Space>
                <Space>
                  <Button size="small" onClick={() => props.onLoad(sample)}>{copy.testRun.loadSample}</Button>
                  <Button size="small" type="primary" loading={props.running} onClick={() => props.onRun(sample)}>{copy.testRun.runSample}</Button>
                </Space>
              </Space>
              <pre style={{ whiteSpace: "pre-wrap", margin: 0, width: "100%", maxHeight: 120, overflow: "auto" }}>
                {JSON.stringify({
                  parameters: sample.parameters,
                  [copy.testRun.expectedLabel]: sample.expectedResult,
                  [copy.testRun.actualLabel]: sample.lastResult,
                  [copy.testRun.previousLabel]: sample.previousResult,
                }, null, 2)}
              </pre>
            </Space>
          </Card>
        ))}
      </Space>
    </div>
  );
}

function ParameterField(props: {
  field: MicroflowRunInputField;
  value: unknown;
  error?: string;
  onChange: (value: unknown) => void;
}) {
  const parameter = props.field.parameter;
  return (
    <label style={{ display: "grid", gap: 6, width: "100%" }}>
      <Space>
        <Text size="small" strong>{parameter.name}{parameter.required ? " *" : ""}</Text>
        <Tag size="small">{props.field.typeLabel}</Tag>
      </Space>
      {renderInput(parameter.dataType, props.value, props.onChange)}
      {parameter.documentation || parameter.description ? <Text type="tertiary" size="small">{parameter.documentation ?? parameter.description}</Text> : null}
      {props.field.warning ? <Text type="warning" size="small">{props.field.warning}</Text> : null}
      {props.error ? <Text type="danger" size="small">{props.error}</Text> : null}
    </label>
  );
}

function renderInput(dataType: MicroflowDataType, value: unknown, onChange: (value: unknown) => void) {
  if (dataType.kind === "boolean") {
    return (
      <Select
        value={String(value ?? false)}
        optionList={[{ label: "true", value: "true" }, { label: "false", value: "false" }]}
        onChange={(next: unknown) => onChange(String(next) === "true")}
      />
    );
  }
  if (dataType.kind === "integer" || dataType.kind === "long" || dataType.kind === "decimal") {
    const copy = getMendixMicroflowCopy();
    return <Input value={value === undefined || value === null ? "" : String(value)} placeholder={copy.testRun.numberPlaceholder} onChange={onChange} />;
  }
  if (dataType.kind === "dateTime") {
    const copy = getMendixMicroflowCopy();
    return <Input value={String(value ?? "")} placeholder={copy.testRun.dateTimePlaceholder} onChange={onChange} />;
  }
  if (dataType.kind === "object" || dataType.kind === "list" || dataType.kind === "json") {
    return <TextArea autosize value={typeof value === "string" ? value : JSON.stringify(value ?? "", null, 2)} onChange={onChange} />;
  }
  if (dataType.kind === "enumeration") {
    return <Input value={String(value ?? "")} placeholder={dataType.enumerationQualifiedName} onChange={onChange} />;
  }
  if (dataType.kind === "void" || dataType.kind === "binary") {
    const copy = getMendixMicroflowCopy();
    return <Input disabled value="" placeholder={copy.testRun.unsupportedInputPlaceholder} />;
  }
  return <Input value={String(value ?? "")} onChange={onChange} />;
}

function RunResultPreview({ session, serviceError }: { session?: MicroflowRunSession; serviceError?: string }) {
  if (serviceError) {
    return <Card style={{ width: "100%" }} bodyStyle={{ padding: 12 }}><Text type="danger">{serviceError}</Text></Card>;
  }
  if (!session) {
    const copy = getMendixMicroflowCopy();
    return <Text type="tertiary">{copy.testRun.resultPlaceholder}</Text>;
  }
  const copy = getMendixMicroflowCopy();
  const durationMs = session.endedAt ? Math.max(0, new Date(session.endedAt).getTime() - new Date(session.startedAt).getTime()) : undefined;
  const nodeOutputs = session.trace.filter(frame => frame.output !== undefined).map(frame => ({
    objectId: frame.objectId,
    microflowId: frame.microflowId,
    status: frame.status,
    output: frame.output,
  }));
  const childRuns = session.childRuns ?? [];
  return (
    <Card style={{ width: "100%" }} bodyStyle={{ padding: 12 }}>
      <Space vertical align="start" style={{ width: "100%" }}>
        <Space wrap>
          <Tag color={session.status === "success" ? "green" : session.status === "failed" ? "red" : "orange"}>{session.status}</Tag>
          <Tag>{formatMendixMicroflowTemplate(copy.testRun.runIdTag, { id: session.id })}</Tag>
          {session.callDepth !== undefined ? <Tag>{formatMendixMicroflowTemplate(copy.testRun.callDepthTag, { depth: session.callDepth })}</Tag> : null}
          {durationMs !== undefined ? <Tag>{durationMs}ms</Tag> : null}
          <Tag>{formatMendixMicroflowTemplate(copy.testRun.traceFramesTag, { count: session.trace.length })}</Tag>
          <Tag>{formatMendixMicroflowTemplate(copy.testRun.logsTag, { count: session.logs.length })}</Tag>
          {childRuns.length > 0 ? <Tag color="blue">{formatMendixMicroflowTemplate(copy.testRun.childRunsTag, { count: childRuns.length })}</Tag> : null}
        </Space>
        {session.error ? <Text type="danger">{session.error.code}: {session.error.message}</Text> : null}
        {session.error?.callStack && session.error.callStack.length > 0 ? (
          <Text type="warning">{copy.testRun.callStackPrefix}: {session.error.callStack.join(" -> ")}</Text>
        ) : null}
        <pre data-testid="microflow-test-run-output-json" style={{ whiteSpace: "pre-wrap", margin: 0, maxHeight: 220, overflow: "auto", width: "100%" }}>
          {JSON.stringify({
            output: session.output,
            nodeOutputs,
            childRuns: childRuns.map(item => ({
              id: item.id,
              status: item.status,
              error: item.error,
              trace: item.trace.map(frame => ({
                objectId: frame.objectId,
                microflowId: frame.microflowId,
                status: frame.status,
                output: frame.output,
                error: frame.error,
              })),
            })),
            logs: session.logs,
          }, null, 2)}
        </pre>
      </Space>
    </Card>
  );
}

function parseOptionalJson(value: string): { ok: true; value?: unknown } | { ok: false } {
  if (!value.trim()) {
    return { ok: true };
  }
  try {
    return { ok: true, value: JSON.parse(value) };
  } catch {
    return { ok: false };
  }
}

function stableJson(value: unknown): string {
  return JSON.stringify(value, Object.keys(flattenKeys(value)).sort());
}

function flattenKeys(value: unknown, keys: Record<string, true> = {}): Record<string, true> {
  if (!value || typeof value !== "object") {
    return keys;
  }
  for (const key of Object.keys(value as Record<string, unknown>)) {
    keys[key] = true;
    flattenKeys((value as Record<string, unknown>)[key], keys);
  }
  return keys;
}
