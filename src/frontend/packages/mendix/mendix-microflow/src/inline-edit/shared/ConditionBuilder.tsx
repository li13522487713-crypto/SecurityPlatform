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
  clauses?: Array<{
    left: string;
    operator: typeof operators[number];
    right: string;
  }>;
}

export function ConditionBuilder(props: {
  value: ConditionBuilderValue;
  readonly?: boolean;
  variables?: ContextVariableCandidate[];
  onChange: (next: ConditionBuilderValue) => void;
  onChangeRaw?: (raw: string) => void;
}) {
  const operatorOptions = useMemo(() => operators.map(item => ({ label: item, value: item })), []);
  const clauses = props.value.clauses?.length
    ? props.value.clauses
    : [{ left: props.value.left, operator: props.value.operator, right: props.value.right }];
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      {clauses.map((clause, index) => (
        <Space key={`clause-${index}`} vertical align="start" style={{ width: "100%" }}>
          <InlineExpressionField
            value={clause.left}
            readonly={props.readonly}
            placeholder="$riskScore"
            onCommit={left => {
              const nextClauses = clauses.map((item, itemIndex) => (itemIndex === index ? { ...item, left } : item));
              props.onChange({ ...props.value, left: nextClauses[0]?.left ?? "", clauses: nextClauses });
            }}
          />
          {props.variables?.length ? (
            <ContextVariablePicker
              value={clause.left}
              disabled={props.readonly}
              placeholder="从上下文插入变量"
              variables={props.variables}
              insertionMode="append"
              onChange={left => {
                const nextClauses = clauses.map((item, itemIndex) => (itemIndex === index ? { ...item, left: left ?? "" } : item));
                props.onChange({ ...props.value, left: nextClauses[0]?.left ?? "", clauses: nextClauses });
              }}
            />
          ) : null}
          <Select
            disabled={props.readonly}
            value={clause.operator}
            optionList={operatorOptions}
            style={{ width: "100%" }}
            onChange={value => {
              const operator = String(value) as ConditionBuilderValue["operator"];
              const nextClauses = clauses.map((item, itemIndex) => (itemIndex === index ? { ...item, operator } : item));
              props.onChange({ ...props.value, operator: nextClauses[0]?.operator ?? "equals", clauses: nextClauses });
            }}
          />
          <InlineExpressionField
            value={clause.right}
            readonly={props.readonly}
            placeholder="80"
            onCommit={right => {
              const nextClauses = clauses.map((item, itemIndex) => (itemIndex === index ? { ...item, right } : item));
              props.onChange({ ...props.value, right: nextClauses[0]?.right ?? "", clauses: nextClauses });
            }}
          />
          {props.variables?.length ? (
            <ContextVariablePicker
              value={clause.right}
              disabled={props.readonly}
              placeholder="右值可选变量"
              variables={props.variables}
              insertionMode="append"
              onChange={right => {
                const nextClauses = clauses.map((item, itemIndex) => (itemIndex === index ? { ...item, right: right ?? "" } : item));
                props.onChange({ ...props.value, right: nextClauses[0]?.right ?? "", clauses: nextClauses });
              }}
            />
          ) : null}
          {clauses.length > 1 ? (
            <Button
              size="small"
              disabled={props.readonly}
              onClick={() => {
                const nextClauses = clauses.filter((_, itemIndex) => itemIndex !== index);
                props.onChange({
                  ...props.value,
                  left: nextClauses[0]?.left ?? "",
                  operator: nextClauses[0]?.operator ?? "equals",
                  right: nextClauses[0]?.right ?? "",
                  clauses: nextClauses,
                });
              }}
            >
              删除条件
            </Button>
          ) : null}
        </Space>
      ))}
      <Button
        size="small"
        disabled={props.readonly}
        onClick={() => {
          const nextClauses = [...clauses, { left: "", operator: "equals" as const, right: "" }];
          props.onChange({ ...props.value, clauses: nextClauses });
        }}
      >
        添加条件
      </Button>
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
      <Button size="small" disabled={props.readonly} onClick={() => props.onChange({ ...props.value, right: "", clauses: undefined })}>清空值</Button>
    </Space>
  );
}
