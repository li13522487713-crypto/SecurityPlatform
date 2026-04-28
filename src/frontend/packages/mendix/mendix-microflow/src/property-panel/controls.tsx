import { useMemo, useState } from "react";
import { Button, Input, Modal, Select, Space, TextArea, Typography } from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import type { MicroflowExpression, MicroflowTypeRef } from "../schema";
import type {
  MicroflowEntitySelectorProps,
  MicroflowExpressionEditorProps,
  MicroflowVariableSelectorProps
} from "./types";
import { AssociationSelector as MetadataAssociationSelector, AttributeSelector as MetadataAttributeSelector, EntitySelector as MetadataEntitySelector } from "./selectors";

const { Text } = Typography;

export function createExpression(text = ""): MicroflowExpression {
  return {
    id: `expr-${Date.now()}-${Math.round(Math.random() * 10000)}`,
    language: "mendix",
    text,
    raw: text,
    referencedVariables: []
  };
}

export function primitiveType(name: string): MicroflowTypeRef {
  return { kind: "primitive", name };
}

export function FieldLabel({ label, required }: { label: string; required?: boolean }) {
  return (
    <Text strong size="small">
      {required ? <span style={{ color: "var(--semi-color-danger)" }}>* </span> : null}
      {label}
    </Text>
  );
}

export function FieldError({ message }: { message?: string }) {
  return message ? <Text type="danger" size="small">{message}</Text> : null;
}

export function FieldRow({
  label,
  required,
  error,
  children
}: {
  label: string;
  required?: boolean;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <Space vertical align="start" spacing={4} style={{ width: "100%" }}>
      <FieldLabel label={label} required={required} />
      {children}
      <FieldError message={error} />
    </Space>
  );
}

export function ExpressionEditor({
  value,
  variables,
  required,
  readonly,
  placeholder = "Enter expression",
  issues = [],
  onChange
}: MicroflowExpressionEditorProps) {
  const expression = value ?? createExpression();
  const [modalOpen, setModalOpen] = useState(false);
  const [draft, setDraft] = useState(expression.text ?? expression.raw);
  const variableOptions = useMemo(() => variables.map(variable => ({
    label: `${variable.name}: ${(variable.type ?? { name: variable.dataType.kind }).name}`,
    value: variable.name
  })), [variables]);

  function updateText(text: string) {
    onChange({ ...expression, text, raw: text });
  }

  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      <TextArea
        autosize
        readonly={readonly}
        value={expression.text ?? expression.raw}
        placeholder={placeholder}
        onChange={updateText}
      />
      <div style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) auto", gap: 8, width: "100%" }}>
        <Select
          multiple
          disabled={readonly}
          style={{ width: "100%" }}
          placeholder="Insert variable"
          value={expression.referencedVariables ?? []}
          optionList={variableOptions}
          onChange={selected => {
            const selectedValues = Array.isArray(selected) ? selected.map(String) : [];
            onChange({ ...expression, referencedVariables: selectedValues });
          }}
        />
        <Button
          disabled={readonly}
          onClick={() => {
            setDraft(expression.text ?? expression.raw);
            setModalOpen(true);
          }}
        >
          Edit
        </Button>
      </div>
      <Text type="tertiary" size="small">Syntax hints and engine validation are reserved for the expression runtime.</Text>
      {required && !(expression.text ?? expression.raw).trim() ? <FieldError message="Expression is required." /> : null}
      {issues.map(issue => <FieldError key={issue} message={issue} />)}
      <Modal
        visible={modalOpen}
        title="Expression editor"
        onCancel={() => setModalOpen(false)}
        onOk={() => {
          updateText(draft);
          setModalOpen(false);
        }}
      >
        <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
          <TextArea autosize={{ minRows: 8 }} value={draft} onChange={setDraft} placeholder={placeholder} />
          <Select
            multiple
            style={{ width: "100%" }}
            placeholder="Referenced variables"
            value={expression.referencedVariables ?? []}
            optionList={variableOptions}
            onChange={selected => {
              const selectedValues = Array.isArray(selected) ? selected.map(String) : [];
              onChange({ ...expression, referencedVariables: selectedValues, text: draft, raw: draft });
            }}
          />
        </Space>
      </Modal>
    </Space>
  );
}

export function VariableSelector({ value, variables, readonly, placeholder = "Select variable", onChange }: MicroflowVariableSelectorProps) {
  return (
    <Select
      filter
      disabled={readonly}
      style={{ width: "100%" }}
      value={value}
      placeholder={placeholder}
      optionList={variables.map(variable => ({ label: `${variable.name}: ${(variable.type ?? { name: variable.dataType.kind }).name}`, value: variable.name }))}
      onChange={selected => onChange(String(selected ?? ""))}
    />
  );
}

export function EntitySelector({ value, readonly, onChange }: MicroflowEntitySelectorProps) {
  return (
    <MetadataEntitySelector
      value={value}
      disabled={readonly}
      onChange={qualified => onChange(qualified ?? "")}
    />
  );
}

export function AssociationSelector({ value, readonly, onChange, startEntityQualifiedName }: {
  value?: string;
  readonly?: boolean;
  onChange: (value: string) => void;
  /** 若 legacy 表单未传，需在选择实体后由上层填入。 */
  startEntityQualifiedName?: string;
}) {
  return (
    <MetadataAssociationSelector
      startEntityQualifiedName={startEntityQualifiedName}
      value={value}
      disabled={readonly}
      onChange={q => onChange(q ?? "")}
    />
  );
}

export function AttributeSelector({
  entity,
  value,
  readonly,
  onChange,
}: {
  entity?: string;
  value?: string;
  readonly?: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <MetadataAttributeSelector
      entityQualifiedName={entity}
      value={value}
      disabled={readonly}
      onChange={q => onChange(q ?? "")}
    />
  );
}

export function KeyValueEditor({
  value = [],
  readonly,
  keyPlaceholder = "Key",
  valuePlaceholder = "Value",
  onChange
}: {
  value?: Array<{ key: string; value: string }>;
  readonly?: boolean;
  keyPlaceholder?: string;
  valuePlaceholder?: string;
  onChange: (value: Array<{ key: string; value: string }>) => void;
}) {
  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      {value.map((item, index) => (
        <div key={`${item.key}-${index}`} style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto", gap: 6, width: "100%" }}>
          <Input readonly={readonly} value={item.key} placeholder={keyPlaceholder} onChange={next => onChange(value.map((row, rowIndex) => rowIndex === index ? { ...row, key: next } : row))} />
          <Input readonly={readonly} value={item.value} placeholder={valuePlaceholder} onChange={next => onChange(value.map((row, rowIndex) => rowIndex === index ? { ...row, value: next } : row))} />
          <Button disabled={readonly} type="danger" theme="borderless" onClick={() => onChange(value.filter((_, rowIndex) => rowIndex !== index))}>Delete</Button>
        </div>
      ))}
      <Button disabled={readonly} icon={<IconPlus />} onClick={() => onChange([...value, { key: "", value: "" }])}>Add</Button>
    </Space>
  );
}
