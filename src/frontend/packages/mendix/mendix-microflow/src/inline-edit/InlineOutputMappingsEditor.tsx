import { Button, Input, Select, Space, Typography } from "@douyinfe/semi-ui";
import { IconMinusCircleStroked, IconPlusCircleStroked } from "@douyinfe/semi-icons";
import { useEffect, useMemo, useState } from "react";

import type { MicroflowOutputMapping, MicroflowOutputMappingSource } from "../flowgram/FlowGramMicroflowTypes";
import { InlineExpressionField } from "./InlineExpressionField";
import { InlineVariableField } from "./InlineVariableField";

const { Text } = Typography;

interface OutputMappingDraft {
  key: string;
  source: MicroflowOutputMappingSource;
  variableName: string;
  constantValueText: string;
  expression: string;
}

function toConstantText(value: unknown): string {
  if (value === undefined) {
    return "";
  }
  if (typeof value === "string") {
    return value;
  }
  return JSON.stringify(value);
}

function parseConstantText(value: string): unknown {
  const trimmed = value.trim();
  if (!trimmed) {
    return "";
  }
  if (trimmed === "true") {
    return true;
  }
  if (trimmed === "false") {
    return false;
  }
  if (/^-?\d+(\.\d+)?$/.test(trimmed)) {
    const numeric = Number(trimmed);
    if (Number.isFinite(numeric)) {
      return numeric;
    }
  }
  if ((trimmed.startsWith("{") && trimmed.endsWith("}")) || (trimmed.startsWith("[") && trimmed.endsWith("]"))) {
    try {
      return JSON.parse(trimmed);
    } catch {
      return value;
    }
  }
  return value;
}

function toDraftRow(mapping: MicroflowOutputMapping): OutputMappingDraft {
  return {
    key: mapping.key ?? "",
    source: mapping.source ?? "variable",
    variableName: mapping.variableName ?? "",
    constantValueText: toConstantText(mapping.constantValue),
    expression: mapping.expression ?? "",
  };
}

function blankRow(): OutputMappingDraft {
  return {
    key: "",
    source: "variable",
    variableName: "",
    constantValueText: "",
    expression: "",
  };
}

function parseRows(value: string): OutputMappingDraft[] {
  if (!value.trim()) {
    return [];
  }
  try {
    const parsed = JSON.parse(value) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }
    const rows = parsed
      .filter(item => item && typeof item === "object")
      .map(item => toDraftRow(item as MicroflowOutputMapping));
    return rows;
  } catch {
    return [];
  }
}

function hasRowInput(row: OutputMappingDraft): boolean {
  return Boolean(row.key.trim() || row.variableName.trim() || row.expression.trim() || row.constantValueText.trim());
}

function hasPersistedMappings(value: string): boolean {
  if (!value.trim()) {
    return false;
  }
  try {
    const parsed = JSON.parse(value) as unknown;
    return Array.isArray(parsed) && parsed.length > 0;
  } catch {
    return false;
  }
}

function isCompleteRow(row: OutputMappingDraft): boolean {
  if (!row.key.trim()) {
    return false;
  }
  if (row.source === "variable") {
    return Boolean(row.variableName.trim());
  }
  if (row.source === "expression") {
    return Boolean(row.expression.trim());
  }
  return Boolean(row.constantValueText.trim());
}

function toMappings(rows: OutputMappingDraft[]): MicroflowOutputMapping[] {
  return rows
    .filter(isCompleteRow)
    .map(row => {
      if (row.source === "variable") {
        return {
          key: row.key.trim(),
          source: "variable" as const,
          variableName: row.variableName.trim(),
        };
      }
      if (row.source === "expression") {
        return {
          key: row.key.trim(),
          source: "expression" as const,
          expression: row.expression,
        };
      }
      return {
        key: row.key.trim(),
        source: "constant" as const,
        constantValue: parseConstantText(row.constantValueText),
      };
    });
}

export function InlineOutputMappingsEditor(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
  const [rows, setRows] = useState<OutputMappingDraft[]>(() => parseRows(props.value));
  const keyErrors = useMemo(() => {
    const map = new Map<number, string>();
    const keys = rows.map(item => item.key.trim());
    keys.forEach((key, index) => {
      if (!hasRowInput(rows[index] ?? blankRow())) {
        return;
      }
      if (!key) {
        map.set(index, "key 不能为空");
        return;
      }
      if (keys.filter(candidate => candidate === key).length > 1) {
        map.set(index, "key 不能重复");
      }
    });
    return map;
  }, [rows]);

  useEffect(() => {
    setRows(parseRows(props.value));
  }, [props.value]);

  const commitRows = (nextRows: OutputMappingDraft[]) => {
    setRows(nextRows);
    const mappings = toMappings(nextRows);
    if (mappings.length > 0 || hasPersistedMappings(props.value)) {
      props.onCommit?.(JSON.stringify(mappings));
    }
  };

  const updateRow = (index: number, patch: Partial<OutputMappingDraft>) => {
    const next = rows.map((row, rowIndex) => rowIndex === index ? { ...row, ...patch } : row);
    commitRows(next);
  };

  const addRow = () => {
    setRows([...rows, blankRow()]);
  };

  const removeRow = (index: number) => {
    const next = rows.filter((_, rowIndex) => rowIndex !== index);
    setRows(next);
    const mappings = toMappings(next);
    if (mappings.length > 0 || hasPersistedMappings(props.value)) {
      props.onCommit?.(JSON.stringify(mappings));
    }
  };

  return (
    <div style={{ display: "grid", gap: 8 }}>
      {rows.map((row, index) => (
        <div key={`output-row-${index}`} style={{ display: "grid", gap: 6, padding: 8, border: "1px solid var(--semi-color-border)", borderRadius: 8 }}>
          <div style={{ display: "grid", gap: 6, gridTemplateColumns: "1.2fr 0.9fr auto", alignItems: "center" }}>
            <Input
              size="small"
              value={row.key}
              disabled={props.readonly}
              placeholder={props.placeholder ?? "输出字段名，例如: result"}
              onChange={value => updateRow(index, { key: value })}
            />
            <Select
              size="small"
              value={row.source}
              disabled={props.readonly}
              optionList={[
                { label: "变量", value: "variable" },
                { label: "常量", value: "constant" },
                { label: "表达式", value: "expression" },
              ]}
              onChange={value => {
                const source = String(value ?? "variable") as MicroflowOutputMappingSource;
                updateRow(index, { source });
              }}
            />
            <Button
              size="small"
              theme="borderless"
              type="danger"
              icon={<IconMinusCircleStroked />}
              disabled={props.readonly}
              onClick={() => removeRow(index)}
            />
          </div>
          {row.source === "variable" ? (
            <InlineVariableField
              value={row.variableName}
              readonly={props.readonly}
              options={props.options}
              placeholder="选择输出变量"
              onCommit={value => updateRow(index, { variableName: value })}
            />
          ) : null}
          {row.source === "constant" ? (
            <Input
              size="small"
              value={row.constantValueText}
              disabled={props.readonly}
              placeholder="写死值（string/number/bool/json）"
              onChange={value => updateRow(index, { constantValueText: value })}
            />
          ) : null}
          {row.source === "expression" ? (
            <InlineExpressionField
              value={row.expression}
              readonly={props.readonly}
              options={props.options}
              placeholder="$result"
              onCommit={value => updateRow(index, { expression: value })}
            />
          ) : null}
          {keyErrors.get(index) ? <Text type="danger" size="small">{keyErrors.get(index)}</Text> : null}
        </div>
      ))}
      <Space>
        <Button
          size="small"
          theme="light"
          icon={<IconPlusCircleStroked />}
          disabled={props.readonly}
          onClick={addRow}
        >
          新增输出
        </Button>
      </Space>
    </div>
  );
}
