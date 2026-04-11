import { Button, Form, Input, InputNumber, Select, Switch, Space } from "antd";
import { useMemo } from "react";
import type { FormFieldSchema, FormSectionSchema } from "../node-registry";
import { getValueByPath, setValueByPath } from "./path-utils";

interface SchemaFormProps {
  sections: FormSectionSchema[];
  config: Record<string, unknown>;
  onChange: (next: Record<string, unknown>) => void;
}

function asString(value: unknown): string {
  return typeof value === "string" ? value : "";
}

function asNumber(value: unknown): number | undefined {
  return typeof value === "number" ? value : undefined;
}

function asBoolean(value: unknown): boolean {
  return typeof value === "boolean" ? value : false;
}

function toKeyValueRows(raw: unknown): Array<{ key: string; value: string }> {
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) {
    return [];
  }
  return Object.entries(raw as Record<string, unknown>).map(([key, value]) => ({
    key,
    value: typeof value === "string" ? value : JSON.stringify(value)
  }));
}

function fromKeyValueRows(rows: Array<{ key: string; value: string }>): Record<string, string> {
  const result: Record<string, string> = {};
  for (const row of rows) {
    const key = row.key.trim();
    if (!key) {
      continue;
    }
    result[key] = row.value;
  }
  return result;
}

function KeyValueEditor(props: {
  value: unknown;
  onChange: (next: Record<string, string>) => void;
  keyPlaceholder?: string;
  valuePlaceholder?: string;
}) {
  const rows = useMemo(() => toKeyValueRows(props.value), [props.value]);
  return (
    <Space direction="vertical" style={{ width: "100%" }} size={6}>
      {rows.map((row, index) => (
        <Space key={`${row.key}-${index}`} style={{ width: "100%" }}>
          <Input
            size="small"
            value={row.key}
            placeholder={props.keyPlaceholder ?? "key"}
            onChange={(event) => {
              const nextRows = [...rows];
              nextRows[index] = { ...nextRows[index], key: event.target.value };
              props.onChange(fromKeyValueRows(nextRows));
            }}
          />
          <Input
            size="small"
            value={row.value}
            placeholder={props.valuePlaceholder ?? "value"}
            onChange={(event) => {
              const nextRows = [...rows];
              nextRows[index] = { ...nextRows[index], value: event.target.value };
              props.onChange(fromKeyValueRows(nextRows));
            }}
          />
          <Button
            size="small"
            onClick={() => {
              const nextRows = rows.filter((_, rowIndex) => rowIndex !== index);
              props.onChange(fromKeyValueRows(nextRows));
            }}
          >
            删除
          </Button>
        </Space>
      ))}
      <Button
        size="small"
        type="dashed"
        onClick={() => {
          const nextRows = [...rows, { key: "", value: "" }];
          props.onChange(fromKeyValueRows(nextRows));
        }}
      >
        添加映射
      </Button>
    </Space>
  );
}

function renderField(
  field: FormFieldSchema,
  config: Record<string, unknown>,
  onChange: (next: Record<string, unknown>) => void
) {
  const current = getValueByPath(config, field.path);
  const updatePath = (nextValue: unknown) => onChange(setValueByPath(config, field.path, nextValue));

  if (field.kind === "text") {
    return <Input size="small" value={asString(current)} placeholder={field.placeholder} onChange={(event) => updatePath(event.target.value)} />;
  }
  if (field.kind === "textarea") {
    return (
      <Input.TextArea
        rows={field.rows ?? 4}
        value={asString(current)}
        placeholder={field.placeholder}
        onChange={(event) => updatePath(event.target.value)}
      />
    );
  }
  if (field.kind === "number") {
    return <InputNumber min={field.min} max={field.max} value={asNumber(current)} style={{ width: "100%" }} onChange={(value) => updatePath(value)} />;
  }
  if (field.kind === "switch") {
    return <Switch checked={asBoolean(current)} onChange={(checked) => updatePath(checked)} />;
  }
  if (field.kind === "select") {
    return <Select value={current as string | number | boolean | undefined} options={field.options} onChange={(value) => updatePath(value)} />;
  }
  if (field.kind === "keyValue") {
    return <KeyValueEditor value={current} onChange={(next) => updatePath(next)} />;
  }
  if (field.kind === "json") {
    return (
      <Input.TextArea
        rows={field.rows ?? 8}
        value={typeof current === "string" ? current : JSON.stringify(current ?? {}, null, 2)}
        onChange={(event) => updatePath(event.target.value)}
      />
    );
  }
  return <Input size="small" value={asString(current)} onChange={(event) => updatePath(event.target.value)} />;
}

export function SchemaForm(props: SchemaFormProps) {
  return (
    <Form layout="vertical" size="small">
      {props.sections.map((section) => (
        <div key={section.key} className="wf-react-section">
          {section.fields.length > 0 ? <div className="wf-react-section-title">{section.title}</div> : null}
          {section.fields.map((field) => (
            <Form.Item key={field.key} label={field.label} required={field.required}>
              {renderField(field, props.config, props.onChange)}
            </Form.Item>
          ))}
        </div>
      ))}
    </Form>
  );
}

