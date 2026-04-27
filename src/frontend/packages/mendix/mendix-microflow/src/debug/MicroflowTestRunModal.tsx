import { useEffect, useMemo, useState } from "react";
import { Button, Card, Input, Modal, Select, Space, Switch, Tag, TextArea, Typography } from "@douyinfe/semi-ui";

import type { MicroflowDataType, MicroflowSchema } from "../schema";
import { buildDefaultRunInputValues, buildRunInputModel, validateRunInputs, type MicroflowRunInputField } from "./run-input-model";
import type { MicroflowRunSession, MicroflowTestRunInput, MicroflowTestRunOptions } from "./trace-types";

const { Text } = Typography;

export interface MicroflowTestRunModalProps {
  visible: boolean;
  schema: MicroflowSchema;
  running?: boolean;
  dirty?: boolean;
  validationErrorCount?: number;
  values?: Record<string, unknown>;
  lastSession?: MicroflowRunSession;
  serviceError?: string;
  onCancel: () => void;
  onValuesChange?: (values: Record<string, unknown>) => void;
  onRun: (input: MicroflowTestRunInput) => void;
}

export function MicroflowTestRunModal(props: MicroflowTestRunModalProps) {
  const model = useMemo(() => buildRunInputModel(props.schema), [props.schema]);
  const defaults = useMemo(() => buildDefaultRunInputValues(model), [model]);
  const parameters = props.values ?? defaults;
  const [options, setOptions] = useState<MicroflowTestRunOptions>({ decisionBooleanResult: true, loopIterations: 2 });
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (props.visible) {
      setOptions({ decisionBooleanResult: true, loopIterations: 2 });
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

  return (
    <Modal
      title="Run Microflow"
      visible={props.visible}
      onCancel={props.onCancel}
      footer={null}
      width={860}
      maskClosable={!props.running}
    >
      <Space vertical align="start" spacing={14} style={{ width: "100%" }}>
        <Space wrap>
          <Tag color="blue">{props.schema.displayName || props.schema.name}</Tag>
          <Tag>{model.microflowId}</Tag>
          <Tag>{model.schemaVersion}</Tag>
          {props.dirty ? <Tag color="orange">dirty - Save & Run</Tag> : <Tag color="green">saved draft</Tag>}
          {props.validationErrorCount ? <Tag color="red">{props.validationErrorCount} validation errors</Tag> : <Tag color="green">validation gate ready</Tag>}
        </Space>
        <Text type="secondary">Run 会先执行 Stage 20 validation；无 error 后调用真实后端 POST /api/microflows/{model.microflowId}/test-run，并提交当前 schema draft 与输入参数。</Text>
        {model.warnings.map(warning => <Text key={warning} type="warning" size="small">{warning}</Text>)}
        {model.fields.length === 0 ? <Text type="tertiary">当前微流没有输入参数。</Text> : null}
        {model.fields.map(field => (
          <ParameterField
            key={field.parameter.id}
            field={field}
            value={parameters[field.parameter.name]}
            error={errors[field.parameter.name]}
            onChange={value => updateParameter(field.parameter.name, value)}
          />
        ))}
        <div style={{ width: "100%", borderTop: "1px solid var(--semi-color-border)", paddingTop: 12 }}>
          <Text strong>Runtime options</Text>
          <Space vertical align="start" spacing={10} style={{ width: "100%", marginTop: 10 }}>
            <label style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <Switch checked={Boolean(options.allowRealHttp)} onChange={allowRealHttp => setOptions(current => ({ ...current, allowRealHttp }))} />
              <Text>allowRealHttp</Text>
              <Text size="small" type="tertiary">默认关闭，后端策略不允许时会真实返回错误。</Text>
            </label>
            <label style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <Switch checked={Boolean(options.simulateRestError)} onChange={simulateRestError => setOptions(current => ({ ...current, simulateRestError }))} />
              <Text>simulateRestError</Text>
            </label>
            <label style={{ display: "grid", gap: 6, width: "100%" }}>
              <Text size="small" strong>decisionBooleanResult</Text>
              <Select
                value={String(options.decisionBooleanResult ?? true)}
                style={{ width: 220 }}
                optionList={[{ label: "true", value: "true" }, { label: "false", value: "false" }]}
                onChange={value => setOptions(current => ({ ...current, decisionBooleanResult: String(value) === "true" }))}
              />
            </label>
            <label style={{ display: "grid", gap: 6, width: "100%" }}>
              <Text size="small" strong>enumerationCaseValue</Text>
              <Input value={options.enumerationCaseValue ?? ""} placeholder="可选，枚举 case value" onChange={enumerationCaseValue => setOptions(current => ({ ...current, enumerationCaseValue }))} />
            </label>
            <label style={{ display: "grid", gap: 6, width: "100%" }}>
              <Text size="small" strong>objectTypeCase</Text>
              <Input value={options.objectTypeCase ?? ""} placeholder="可选，specialization entity qualified name" onChange={objectTypeCase => setOptions(current => ({ ...current, objectTypeCase }))} />
            </label>
            <label style={{ display: "grid", gap: 6, width: 220 }}>
              <Text size="small" strong>loopIterations</Text>
              <Input value={String(options.loopIterations ?? 2)} onChange={value => setOptions(current => ({ ...current, loopIterations: Number.isFinite(Number(value)) ? Number(value) : 2 }))} />
            </label>
            <label style={{ display: "grid", gap: 6, width: 220 }}>
              <Text size="small" strong>maxSteps</Text>
              <Input value={String(options.maxSteps ?? 200)} onChange={value => setOptions(current => ({ ...current, maxSteps: Number.isFinite(Number(value)) ? Number(value) : 200 }))} />
            </label>
          </Space>
        </div>
        <RunResultPreview session={props.lastSession} serviceError={props.serviceError} />
        <Space style={{ width: "100%", justifyContent: "flex-end" }}>
          <Button onClick={props.onCancel} disabled={props.running}>取消</Button>
          <Button type="primary" loading={props.running} onClick={run}>{props.dirty ? "Save & Run" : "Run"}</Button>
        </Space>
      </Space>
    </Modal>
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
        onChange={next => onChange(String(next) === "true")}
      />
    );
  }
  if (dataType.kind === "integer" || dataType.kind === "long" || dataType.kind === "decimal") {
    return <Input value={value === undefined || value === null ? "" : String(value)} placeholder="输入数字" onChange={onChange} />;
  }
  if (dataType.kind === "dateTime") {
    return <Input value={String(value ?? "")} placeholder="2026-04-28T10:00:00Z" onChange={onChange} />;
  }
  if (dataType.kind === "object" || dataType.kind === "list" || dataType.kind === "json") {
    return <TextArea autosize value={typeof value === "string" ? value : JSON.stringify(value ?? "", null, 2)} onChange={onChange} />;
  }
  if (dataType.kind === "enumeration") {
    return <Input value={String(value ?? "")} placeholder={dataType.enumerationQualifiedName} onChange={onChange} />;
  }
  if (dataType.kind === "void" || dataType.kind === "binary") {
    return <Input disabled value="" placeholder="不可作为运行输入" />;
  }
  return <Input value={String(value ?? "")} onChange={onChange} />;
}

function RunResultPreview({ session, serviceError }: { session?: MicroflowRunSession; serviceError?: string }) {
  if (serviceError) {
    return <Card style={{ width: "100%" }} bodyStyle={{ padding: 12 }}><Text type="danger">{serviceError}</Text></Card>;
  }
  if (!session) {
    return <Text type="tertiary">运行结果会显示在这里，并同步到底部 Debug 面板。</Text>;
  }
  const durationMs = session.endedAt ? Math.max(0, new Date(session.endedAt).getTime() - new Date(session.startedAt).getTime()) : undefined;
  const nodeOutputs = session.trace.filter(frame => frame.output !== undefined).map(frame => ({
    objectId: frame.objectId,
    status: frame.status,
    output: frame.output,
  }));
  return (
    <Card style={{ width: "100%" }} bodyStyle={{ padding: 12 }}>
      <Space vertical align="start" style={{ width: "100%" }}>
        <Space wrap>
          <Tag color={session.status === "success" ? "green" : session.status === "failed" ? "red" : "orange"}>{session.status}</Tag>
          <Tag>runId {session.id}</Tag>
          {durationMs !== undefined ? <Tag>{durationMs}ms</Tag> : null}
          <Tag>{session.trace.length} frames</Tag>
          <Tag>{session.logs.length} logs</Tag>
        </Space>
        {session.error ? <Text type="danger">{session.error.code}: {session.error.message}</Text> : null}
        <pre style={{ whiteSpace: "pre-wrap", margin: 0, maxHeight: 220, overflow: "auto", width: "100%" }}>
          {JSON.stringify({ output: session.output, nodeOutputs, logs: session.logs }, null, 2)}
        </pre>
      </Space>
    </Card>
  );
}
