import { useEffect, useMemo, useState } from "react";
import { Button, Input, InputNumber, Modal, Select, Space, Switch, TextArea, Typography } from "@douyinfe/semi-ui";

import type { MicroflowDataType, MicroflowParameter, MicroflowSchema } from "../schema";
import type { MicroflowTestRunInput, MicroflowTestRunOptions } from "./trace-types";

const { Text } = Typography;

export interface MicroflowTestRunModalProps {
  visible: boolean;
  schema: MicroflowSchema;
  running?: boolean;
  onCancel: () => void;
  onRun: (input: MicroflowTestRunInput) => void;
}

export function MicroflowTestRunModal(props: MicroflowTestRunModalProps) {
  const defaults = useMemo(() => buildDefaultParameters(props.schema.parameters), [props.schema.parameters]);
  const [parameters, setParameters] = useState<Record<string, unknown>>(defaults);
  const [options, setOptions] = useState<MicroflowTestRunOptions>({ decisionBooleanResult: true, loopIterations: 2 });
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (props.visible) {
      setParameters(defaults);
      setOptions({ decisionBooleanResult: true, loopIterations: 2 });
      setErrors({});
    }
  }, [defaults, props.visible]);

  const run = () => {
    const nextErrors = validateParameters(props.schema.parameters, parameters);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      return;
    }
    props.onRun({ parameters, options });
  };

  return (
    <Modal
      title="测试运行输入"
      visible={props.visible}
      onCancel={props.onCancel}
      footer={null}
      width={720}
      maskClosable={!props.running}
    >
      <Space vertical align="start" spacing={14} style={{ width: "100%" }}>
        <Text type="secondary">参数将提交给当前运行适配器；HTTP 模式会通过后端 Mock Runtime 生成持久化 RunSession 与 Trace。</Text>
        {props.schema.parameters.length === 0 ? <Text type="tertiary">当前微流没有输入参数。</Text> : null}
        {props.schema.parameters.map(parameter => (
          <ParameterField
            key={parameter.id}
            parameter={parameter}
            value={parameters[parameter.name]}
            error={errors[parameter.name]}
            onChange={value => setParameters(current => ({ ...current, [parameter.name]: value }))}
          />
        ))}
        <div style={{ width: "100%", borderTop: "1px solid var(--semi-color-border)", paddingTop: 12 }}>
          <Text strong>Mock Options</Text>
          <Space vertical align="start" spacing={10} style={{ width: "100%", marginTop: 10 }}>
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
              <InputNumber min={0} max={50} value={options.loopIterations ?? 2} onChange={value => setOptions(current => ({ ...current, loopIterations: typeof value === "number" ? value : 2 }))} />
            </label>
            <label style={{ display: "grid", gap: 6, width: 220 }}>
              <Text size="small" strong>maxSteps</Text>
              <InputNumber min={1} max={1000} value={options.maxSteps ?? 200} onChange={value => setOptions(current => ({ ...current, maxSteps: typeof value === "number" ? value : 200 }))} />
            </label>
          </Space>
        </div>
        <Space style={{ width: "100%", justifyContent: "flex-end" }}>
          <Button onClick={props.onCancel} disabled={props.running}>取消</Button>
          <Button type="primary" loading={props.running} onClick={run}>运行</Button>
        </Space>
      </Space>
    </Modal>
  );
}

function ParameterField(props: {
  parameter: MicroflowParameter;
  value: unknown;
  error?: string;
  onChange: (value: unknown) => void;
}) {
  return (
    <label style={{ display: "grid", gap: 6, width: "100%" }}>
      <Text size="small" strong>{props.parameter.name}{props.parameter.required ? " *" : ""}</Text>
      {renderInput(props.parameter.dataType, props.value, props.onChange)}
      {props.parameter.documentation || props.parameter.description ? <Text type="tertiary" size="small">{props.parameter.documentation ?? props.parameter.description}</Text> : null}
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
    return <InputNumber value={typeof value === "number" ? value : undefined} onChange={next => onChange(typeof next === "number" ? next : undefined)} />;
  }
  if (dataType.kind === "object" || dataType.kind === "list" || dataType.kind === "json") {
    return <TextArea autosize value={typeof value === "string" ? value : JSON.stringify(value ?? "", null, 2)} onChange={text => onChange(parseJsonLike(text))} />;
  }
  if (dataType.kind === "enumeration") {
    return <Input value={String(value ?? "")} placeholder={dataType.enumerationQualifiedName} onChange={onChange} />;
  }
  return <Input value={String(value ?? "")} onChange={onChange} />;
}

function buildDefaultParameters(parameters: MicroflowParameter[]): Record<string, unknown> {
  return Object.fromEntries(parameters.map(parameter => [parameter.name, defaultValue(parameter.dataType, parameter.exampleValue)]));
}

function defaultValue(dataType: MicroflowDataType, example?: string): unknown {
  if (example !== undefined) {
    return example;
  }
  if (dataType.kind === "boolean") {
    return false;
  }
  if (dataType.kind === "integer" || dataType.kind === "long" || dataType.kind === "decimal") {
    return 0;
  }
  if (dataType.kind === "object" || dataType.kind === "json") {
    return {};
  }
  if (dataType.kind === "list") {
    return [];
  }
  return "";
}

function validateParameters(parameters: MicroflowParameter[], values: Record<string, unknown>): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const parameter of parameters) {
    if (!parameter.required) {
      continue;
    }
    const value = values[parameter.name];
    if (value === undefined || value === null || value === "") {
      errors[parameter.name] = "必填参数不能为空";
    }
  }
  return errors;
}

function parseJsonLike(text: string): unknown {
  if (!text.trim()) {
    return "";
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}
