import { Button, Input, Space } from "antd";

interface ObjectEditorProps {
  value: unknown;
  onChange: (next: Record<string, string>) => void;
}

interface RowItem {
  key: string;
  value: string;
}

function toRows(value: unknown): RowItem[] {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return [];
  }
  return Object.entries(value as Record<string, unknown>).map(([key, itemValue]) => ({
    key,
    value: typeof itemValue === "string" ? itemValue : JSON.stringify(itemValue)
  }));
}

function toObject(rows: RowItem[]): Record<string, string> {
  const next: Record<string, string> = {};
  for (const row of rows) {
    const key = row.key.trim();
    if (!key) {
      continue;
    }
    next[key] = row.value;
  }
  return next;
}

export function ObjectEditor(props: ObjectEditorProps) {
  const rows = toRows(props.value);

  return (
    <Space direction="vertical" style={{ width: "100%" }} size={6}>
      {rows.map((row, index) => (
        <Space key={`${row.key}-${index}`} style={{ width: "100%" }}>
          <Input
            size="small"
            value={row.key}
            placeholder="key"
            onChange={(event) => {
              const nextRows = [...rows];
              nextRows[index] = { ...nextRows[index], key: event.target.value };
              props.onChange(toObject(nextRows));
            }}
          />
          <Input
            size="small"
            value={row.value}
            placeholder="value"
            onChange={(event) => {
              const nextRows = [...rows];
              nextRows[index] = { ...nextRows[index], value: event.target.value };
              props.onChange(toObject(nextRows));
            }}
          />
          <Button
            size="small"
            onClick={() => {
              const nextRows = rows.filter((_, rowIndex) => rowIndex !== index);
              props.onChange(toObject(nextRows));
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
          props.onChange(toObject([...rows, { key: "", value: "" }]));
        }}
      >
        添加键值
      </Button>
    </Space>
  );
}

