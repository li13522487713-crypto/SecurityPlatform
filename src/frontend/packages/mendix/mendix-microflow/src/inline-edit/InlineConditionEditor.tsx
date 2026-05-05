import { useMemo } from "react";
import { ConditionBuilder, type ConditionBuilderValue } from "./shared/ConditionBuilder";

export function InlineConditionEditor(props: {
  value: string;
  placeholder?: string;
  readonly?: boolean;
  invalid?: boolean;
  options?: Array<{ label: string; value: string }>;
  onCommit?: (value: string) => void;
}) {
  const initial = useMemo<ConditionBuilderValue>(() => {
    const fallback: ConditionBuilderValue = {
      left: props.value,
      operator: "equals",
      right: "",
      logic: "AND",
      raw: props.value,
    };
    const match = props.value.trim().match(/^(.+?)\s*(==|!=|>=|<=|>|<)\s*(.+)$/);
    if (!match) {
      return fallback;
    }
    const operatorMap: Record<string, ConditionBuilderValue["operator"]> = {
      "==": "equals",
      "!=": "not equals",
      ">": "greater than",
      ">=": "greater or equal",
      "<": "less than",
      "<=": "less or equal",
    };
    return {
      left: match[1]?.trim() ?? "",
      operator: operatorMap[match[2] ?? "=="] ?? "equals",
      right: match[3]?.trim() ?? "",
      logic: "AND",
      raw: props.value,
    };
  }, [props.value]);

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
        const normalized = next.raw?.trim()
          ? next.raw.trim()
          : `${next.left} ${symbolMap[next.operator]} ${next.right}`.trim();
        props.onCommit?.(normalized);
      }}
      onChangeRaw={raw => props.onCommit?.(raw)}
    />
  );
}
