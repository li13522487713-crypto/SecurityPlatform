import { useMemo, useState } from "react";
import { Button, Input, Modal, Select, Space, TextArea, Typography } from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import type { MicroflowExpression, MicroflowTypeRef } from "../schema";
import type {
  MicroflowEntitySelectorProps,
  MicroflowExpressionEditorProps,
  MicroflowVariableSelectorProps
} from "./types";

const { Text } = Typography;

export const mockEntities = ["Order", "OrderItem", "User", "Product", "Inventory"];

export const mockAttributes: Record<string, string[]> = {
  Order: ["Id", "Status", "CreatedDate", "ProcessedDate", "Operator", "TotalAmount"],
  Product: ["Id", "Name", "Stock", "Price"],
  OrderItem: ["Id", "Quantity", "Price", "Product"],
  User: ["Id", "Name", "Email"],
  Inventory: ["Id", "Product", "Stock"]
};

export function createExpression(text = ""): MicroflowExpression {
  return {
    id: `expr-${Date.now()}-${Math.round(Math.random() * 10000)}`,
    language: "mendix",
    text,
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
  const [draft, setDraft] = useState(expression.text);
  const variableOptions = useMemo(() => variables.map(variable => ({
    label: `${variable.name}: ${variable.type.name}`,
    value: variable.name
  })), [variables]);

  function updateText(text: string) {
    onChange({ ...expression, text });
  }

  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      <TextArea
        autosize
        readonly={readonly}
        value={expression.text}
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
            setDraft(expression.text);
            setModalOpen(true);
          }}
        >
          Edit
        </Button>
      </div>
      <Text type="tertiary" size="small">Syntax hints and engine validation are reserved for the expression runtime.</Text>
      {required && !expression.text.trim() ? <FieldError message="Expression is required." /> : null}
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
              onChange({ ...expression, referencedVariables: selectedValues, text: draft });
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
      optionList={variables.map(variable => ({ label: `${variable.name}: ${variable.type.name}`, value: variable.name }))}
      onChange={selected => onChange(String(selected ?? ""))}
    />
  );
}

export function EntitySelector({ value, readonly, onChange }: MicroflowEntitySelectorProps) {
  return (
    <Select
      filter
      disabled={readonly}
      style={{ width: "100%" }}
      value={value}
      placeholder="Select entity"
      optionList={mockEntities.map(entity => ({ label: entity, value: entity }))}
      onChange={selected => onChange(String(selected ?? ""))}
    />
  );
}

export function AssociationSelector({ value, readonly, onChange }: { value?: string; readonly?: boolean; onChange: (value: string) => void }) {
  return (
    <Select
      filter
      disabled={readonly}
      style={{ width: "100%" }}
      value={value}
      placeholder="Select association"
      optionList={["Order/Items", "Order/Customer", "Product/Inventory"].map(item => ({ label: item, value: item }))}
      onChange={selected => onChange(String(selected ?? ""))}
    />
  );
}

export function AttributeSelector({
  entity,
  value,
  readonly,
  onChange
}: {
  entity?: string;
  value?: string;
  readonly?: boolean;
  onChange: (value: string) => void;
}) {
  const options = (entity ? mockAttributes[entity] : undefined) ?? mockAttributes.Order;
  return (
    <Select
      filter
      disabled={readonly}
      style={{ width: "100%" }}
      value={value}
      placeholder="Select attribute"
      optionList={options.map(attribute => ({ label: attribute, value: attribute }))}
      onChange={selected => onChange(String(selected ?? ""))}
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
