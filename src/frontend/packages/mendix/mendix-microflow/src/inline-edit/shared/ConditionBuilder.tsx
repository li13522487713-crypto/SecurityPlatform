import { Button, Select, Space } from "@douyinfe/semi-ui";
import { useMemo } from "react";
import { InlineExpressionField } from "../InlineExpressionField";
import { ContextVariablePicker, type ContextVariableCandidate } from "./ContextVariablePicker";

const operators = [
  "equals",
  "not equals",
  "greater than",
  "greater or equal",
  "less than",
  "less or equal",
  "contains",
  "starts with",
  "ends with",
  "is empty",
  "is not empty",
  "is true",
  "is false",
  "in",
  "not in",
] as const;

export interface ConditionBuilderValue {
  left: string;
  operator: typeof operators[number];
  right: string;
  logic?: "AND" | "OR";
  raw?: string;
}

export function ConditionBuilder(props: {
  value: ConditionBuilderValue;
  readonly?: boolean;
  variables?: ContextVariableCandidate[];
  onChange: (next: ConditionBuilderValue) => void;
  onChangeRaw?: (raw: string) => void;
}) {
  const operatorOptions = useMemo(() => operators.map(item => ({ label: item, value: item })), []);
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <InlineExpressionField
        value={props.value.left}
        readonly={props.readonly}
        placeholder="$riskScore"
        onCommit={left => props.onChange({ ...props.value, left })}
      />
      {props.variables?.length ? (
        <ContextVariablePicker
          value={props.value.left}
          disabled={props.readonly}
          placeholder="从上下文插入变量"
          variables={props.variables}
          onChange={left => props.onChange({ ...props.value, left: left ?? "" })}
        />
      ) : null}
      <Select
        disabled={props.readonly}
        value={props.value.operator}
        optionList={operatorOptions}
        style={{ width: "100%" }}
        onChange={value => props.onChange({ ...props.value, operator: String(value) as ConditionBuilderValue["operator"] })}
      />
      <InlineExpressionField
        value={props.value.right}
        readonly={props.readonly}
        placeholder="80"
        onCommit={right => props.onChange({ ...props.value, right })}
      />
      {props.variables?.length ? (
        <ContextVariablePicker
          value={props.value.right}
          disabled={props.readonly}
          placeholder="右值可选变量"
          variables={props.variables}
          onChange={right => props.onChange({ ...props.value, right: right ?? "" })}
        />
      ) : null}
      <Select
        disabled={props.readonly}
        value={props.value.logic ?? "AND"}
        optionList={[{ label: "AND", value: "AND" }, { label: "OR", value: "OR" }]}
        style={{ width: "100%" }}
        onChange={value => props.onChange({ ...props.value, logic: String(value) as "AND" | "OR" })}
      />
      <InlineExpressionField
        value={props.value.raw ?? ""}
        readonly={props.readonly}
        placeholder="raw expression"
        onCommit={raw => {
          props.onChangeRaw?.(raw);
          props.onChange({ ...props.value, raw });
        }}
      />
      <Button size="small" disabled={props.readonly} onClick={() => props.onChange({ ...props.value, right: "" })}>清空值</Button>
    </Space>
  );
}

