import { Select, Typography } from "@douyinfe/semi-ui";
import { useMemo } from "react";
import type { MicroflowDataType } from "../../schema";

const { Text } = Typography;

export interface ContextVariableCandidate {
  name: string;
  type?: MicroflowDataType | { kind: string };
  source?: string;
  sourceNode?: string;
  scope?: string;
  readonly?: boolean;
  maybe?: boolean;
  unknown?: boolean;
  preview?: string;
  refCount?: number;
}

const SOURCE_GROUP_LABEL: Record<string, string> = {
  input: "微流输入参数",
  context: "当前上下文变量",
  "upstream-direct": "直接上游节点输出",
  "upstream-indirect": "间接上游节点输出",
  upstream: "上游节点输出",
  loop: "循环变量",
  system: "系统变量",
  error: "错误变量",
  runtime: "运行时变量快照",
};

function normalizeGroup(source?: string): string {
  if (!source) {
    return "context";
  }
  const key = source.toLowerCase();
  if (key.includes("input")) {
    return "input";
  }
  if (key.includes("upstream") || key.includes("output")) {
    if (key.includes("indirect")) {
      return "upstream-indirect";
    }
    if (key.includes("direct")) {
      return "upstream-direct";
    }
    return "upstream";
  }
  if (key.includes("loop")) {
    return "loop";
  }
  if (key.includes("system")) {
    return "system";
  }
  if (key.includes("error")) {
    return "error";
  }
  if (key.includes("runtime")) {
    return "runtime";
  }
  return "context";
}

export function ContextVariablePicker(props: {
  value?: string;
  disabled?: boolean;
  placeholder?: string;
  variables: ContextVariableCandidate[];
  insertionMode?: "replace" | "append" | "jsonPath";
  onChange: (value?: string) => void;
}) {
  const options = useMemo(() => props.variables.map(variable => ({
    label: `[${SOURCE_GROUP_LABEL[normalizeGroup(variable.source)]}] ${variable.name}`,
    value: variable.name,
    searchKeywords: [
      variable.name,
      (variable.type as { kind?: string } | undefined)?.kind ?? "",
      variable.source ?? "",
      variable.sourceNode ?? "",
      variable.scope ?? "",
      typeof variable.refCount === "number" ? `ref:${variable.refCount}` : "",
      variable.readonly ? "readonly" : "",
      variable.maybe ? "maybe" : "",
      variable.unknown ? "unknown" : "",
    ].join(" ").toLowerCase(),
    render: () => (
      <div style={{ display: "grid", gap: 2 }}>
        <Text>
          {variable.name}
          {variable.readonly ? " · readonly" : ""}
          {variable.maybe ? " · maybe" : ""}
          {variable.unknown ? " · unknown" : ""}
          {typeof variable.refCount === "number" ? ` · refs:${variable.refCount}` : ""}
        </Text>
        <Text type="tertiary" size="small">
          {(variable.type as { kind?: string } | undefined)?.kind ?? "unknown"}
          {variable.source ? ` · ${variable.source}` : ""}
          {variable.sourceNode ? ` · ${variable.sourceNode}` : ""}
          {variable.scope ? ` · ${variable.scope}` : ""}
        </Text>
        {variable.preview ? <Text type="tertiary" size="small">{variable.preview}</Text> : null}
      </div>
    ),
  })), [props.variables]);

  return (
    <Select
      filter={(input, option) => {
        const keyword = String(input ?? "").trim().toLowerCase();
        if (!keyword) {
          return true;
        }
        const typed = option as { label?: string; searchKeywords?: string } | undefined;
        const haystack = `${typed?.label ?? ""} ${typed?.searchKeywords ?? ""}`.toLowerCase();
        return haystack.includes(keyword);
      }}
      showClear
      value={props.value}
      disabled={props.disabled}
      style={{ width: "100%" }}
      placeholder={props.placeholder ?? "选择变量"}
      optionList={options}
      onClear={() => props.onChange(undefined)}
      onChange={value => {
        const selected = value ? String(value) : undefined;
        if (!selected) {
          props.onChange(undefined);
          return;
        }
        if (props.insertionMode === "jsonPath") {
          const normalized = selected.startsWith("$.")
            ? selected
            : selected.startsWith("$")
              ? `$.${selected.slice(1)}`
              : `$.${selected}`;
          props.onChange(normalized);
          return;
        }
        if (props.insertionMode === "append") {
          const base = String(props.value ?? "").trim();
          props.onChange(base ? `${base} ${selected}`.trim() : selected);
          return;
        }
        props.onChange(selected);
      }}
    />
  );
}
