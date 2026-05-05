import { useMemo } from "react";
import { ConditionBuilder, type ConditionBuilderValue } from "./shared/ConditionBuilder";

const operatorTokenMap: Array<{ token: string; operator: ConditionBuilderValue["operator"] }> = [
  { token: ">=", operator: "greater or equal" },
  { token: "<=", operator: "less or equal" },
  { token: "==", operator: "equals" },
  { token: "!=", operator: "not equals" },
  { token: ">", operator: "greater than" },
  { token: "<", operator: "less than" },
  { token: " contains ", operator: "contains" },
  { token: " startsWith ", operator: "starts with" },
  { token: " endsWith ", operator: "ends with" },
  { token: " isEmpty ", operator: "is empty" },
  { token: " isNotEmpty ", operator: "is not empty" },
  { token: " isTrue ", operator: "is true" },
  { token: " isFalse ", operator: "is false" },
  { token: " notIn ", operator: "not in" },
  { token: " in ", operator: "in" },
];

function parseExpressionToBuilder(rawValue: string): ConditionBuilderValue {
  const raw = rawValue.trim();
  const fallback: ConditionBuilderValue = {
    left: rawValue,
    operator: "equals",
    right: "",
    logic: "AND",
    raw: rawValue,
  };
  if (!raw) {
    return fallback;
  }
  const logic: "AND" | "OR" = /\s+OR\s+/i.test(raw) ? "OR" : "AND";
  const clauseParts = logic === "OR"
    ? raw.split(/\s+OR\s+/i)
    : raw.split(/\s+AND\s+/i);
  const clauses: NonNullable<ConditionBuilderValue["clauses"]> = [];
  for (const clause of clauseParts) {
    for (const item of operatorTokenMap) {
      const index = clause.indexOf(item.token);
      if (index <= 0) {
        continue;
      }
      const left = clause.slice(0, index).trim();
      const right = clause.slice(index + item.token.length).trim();
      clauses.push({ left, operator: item.operator, right });
      break;
    }
  }
  if (clauses.length > 0) {
    const first = clauses[0];
    return { left: first.left, operator: first.operator, right: first.right, logic, clauses, raw: rawValue };
  }
  return { ...fallback, logic };
}

export function InlineConditionEditor(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
  const initial = useMemo<ConditionBuilderValue>(() => parseExpressionToBuilder(props.value), [props.value]);

  const variableOptions = useMemo(() => (props.options ?? []).map(option => {
    const [source, nodeLabel] = option.label.includes("::")
      ? option.label.split("::", 2)
      : ["context", option.label];
    return {
      name: option.value,
      source,
      sourceNode: nodeLabel,
    };
  }), [props.options]);

  return (
    <ConditionBuilder
      value={initial}
      readonly={props.readonly}
      variables={variableOptions}
      onChange={next => {
        const symbolMap: Record<ConditionBuilderValue["operator"], string> = {
          equals: "==",
          "not equals": "!=",
          "greater than": ">",
          "greater or equal": ">=",
          "less than": "<",
          "less or equal": "<=",
          contains: "contains",
          "starts with": "startsWith",
          "ends with": "endsWith",
          "is empty": "isEmpty",
          "is not empty": "isNotEmpty",
          "is true": "isTrue",
          "is false": "isFalse",
          in: "in",
          "not in": "notIn",
        };
        const clauses = next.clauses?.length
          ? next.clauses
          : [{ left: next.left, operator: next.operator, right: next.right }];
        const logic = next.logic ?? "AND";
        const synthesized = clauses
          .map(item => `${item.left} ${symbolMap[item.operator]} ${item.right}`.trim())
          .filter(Boolean)
          .join(` ${logic} `)
          .trim();
        const normalized = next.raw?.trim()
          ? next.raw.trim()
          : synthesized;
        props.onCommit?.(normalized);
      }}
      onChangeRaw={raw => props.onCommit?.(raw)}
    />
  );
}
