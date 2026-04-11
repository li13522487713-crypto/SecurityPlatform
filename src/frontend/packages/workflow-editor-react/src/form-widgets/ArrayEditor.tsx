import { Button, Input, InputNumber, Select, Space, Switch } from "antd";
import type { FormFieldSchema } from "../node-registry";

type ArrayItem = Record<string, unknown> | string;

interface ArrayEditorProps {
  value: unknown;
  onChange: (next: ArrayItem[]) => void;
  itemFields?: FormFieldSchema["itemFields"];
}

function toRows(value: unknown): ArrayItem[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.map((item) => {
    if (item && typeof item === "object" && !Array.isArray(item)) {
      return { ...(item as Record<string, unknown>) };
    }
    return typeof item === "string" ? item : String(item ?? "");
  });
}

function createEmptyItem(itemFields?: FormFieldSchema["itemFields"]): ArrayItem {
  if (!itemFields || itemFields.length === 0) {
    return "";
  }
  const next: Record<string, unknown> = {};
  for (const field of itemFields) {
    if (field.kind === "number") {
      next[field.key] = 0;
      continue;
    }
    if (field.kind === "switch") {
      next[field.key] = false;
      continue;
    }
    next[field.key] = "";
  }
  return next;
}

function renderFieldEditor(
  row: ArrayItem,
  field: NonNullable<FormFieldSchema["itemFields"]>[number],
  onChange: (nextValue: unknown) => void
) {
  const value = typeof row === "object" && !Array.isArray(row) ? row[field.key] : undefined;
  if (field.kind === "number") {
    return <InputNumber style={{ width: "100%" }} value={typeof value === "number" ? value : undefined} onChange={(next) => onChange(next ?? 0)} />;
  }
  if (field.kind === "switch") {
    return <Switch checked={Boolean(value)} onChange={(next) => onChange(next)} />;
  }
  if (field.kind === "select") {
    return <Select options={field.options} value={value as string | number | boolean | undefined} onChange={(next) => onChange(next)} />;
  }
  if (field.kind === "textarea") {
    return <Input.TextArea rows={2} value={typeof value === "string" ? value : ""} onChange={(event) => onChange(event.target.value)} />;
  }
  return <Input size="small" value={typeof value === "string" ? value : ""} onChange={(event) => onChange(event.target.value)} />;
}

export function ArrayEditor(props: ArrayEditorProps) {
  const rows = toRows(props.value);
  return (
    <Space direction="vertical" style={{ width: "100%" }} size={6}>
      {rows.map((row, rowIndex) => (
        <Space key={`array-row-${rowIndex}`} direction="vertical" style={{ width: "100%" }}>
          {props.itemFields && props.itemFields.length > 0 ? (
            props.itemFields.map((field) => (
              <div key={`${rowIndex}-${field.key}`} style={{ width: "100%" }}>
                <div style={{ fontSize: 12, marginBottom: 4 }}>{field.label}</div>
                {renderFieldEditor(row, field, (nextValue) => {
                  const nextRows = [...rows];
                  const current = typeof nextRows[rowIndex] === "object" && !Array.isArray(nextRows[rowIndex]) ? { ...(nextRows[rowIndex] as Record<string, unknown>) } : {};
                  current[field.key] = nextValue;
                  nextRows[rowIndex] = current;
                  props.onChange(nextRows);
                })}
              </div>
            ))
          ) : (
            <Input
              size="small"
              value={typeof row === "string" ? row : JSON.stringify(row)}
              onChange={(event) => {
                const nextRows = [...rows];
                nextRows[rowIndex] = event.target.value;
                props.onChange(nextRows);
              }}
            />
          )}
          <Button
            size="small"
            onClick={() => {
              const nextRows = rows.filter((_, index) => index !== rowIndex);
              props.onChange(nextRows);
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
          props.onChange([...rows, createEmptyItem(props.itemFields)]);
        }}
      >
        添加项
      </Button>
    </Space>
  );
}

