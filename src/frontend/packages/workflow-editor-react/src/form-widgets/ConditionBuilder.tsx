import { Button, Input, Select, Space } from "antd";
import {
  addSelectorCondition,
  normalizeSelectorConditions,
  removeSelectorCondition,
  reorderSelectorCondition,
  type SelectorCondition
} from "../editor/selector-branches";

interface ConditionBuilderProps {
  value: unknown;
  onChange: (next: SelectorCondition[]) => void;
}

const OPERATOR_OPTIONS = [
  { value: "eq", label: "=" },
  { value: "ne", label: "!=" },
  { value: "contain", label: "contain" },
  { value: "not_contain", label: "not contain" },
  { value: "gt", label: ">" },
  { value: "gte", label: ">=" },
  { value: "lt", label: "<" },
  { value: "lte", label: "<=" },
  { value: "is_empty", label: "is empty" },
  { value: "is_not_empty", label: "is not empty" }
];

export function ConditionBuilder(props: ConditionBuilderProps) {
  const conditions = normalizeSelectorConditions(props.value);

  return (
    <Space direction="vertical" style={{ width: "100%" }} size={8}>
      {conditions.map((item, index) => (
        <Space key={`condition-${index}`} style={{ width: "100%" }} align="start">
          <Input
            size="small"
            placeholder="left"
            value={item.left}
            onChange={(event) => {
              const nextConditions = [...conditions];
              nextConditions[index] = { ...nextConditions[index], left: event.target.value };
              props.onChange(nextConditions);
            }}
          />
          <Select
            style={{ width: 150 }}
            value={item.op}
            options={OPERATOR_OPTIONS}
            onChange={(op) => {
              const nextConditions = [...conditions];
              nextConditions[index] = { ...nextConditions[index], op };
              props.onChange(nextConditions);
            }}
          />
          <Input
            size="small"
            placeholder="right"
            value={item.right}
            onChange={(event) => {
              const nextConditions = [...conditions];
              nextConditions[index] = { ...nextConditions[index], right: event.target.value };
              props.onChange(nextConditions);
            }}
          />
          <Select
            style={{ width: 120 }}
            value={item.logic ?? "and"}
            options={[
              { value: "and", label: "AND" },
              { value: "or", label: "OR" }
            ]}
            onChange={(logic) => {
              const nextConditions = [...conditions];
              nextConditions[index] = { ...nextConditions[index], logic };
              props.onChange(nextConditions);
            }}
          />
          <Button
            size="small"
            onClick={() => {
              props.onChange(reorderSelectorCondition(conditions, index, index - 1));
            }}
            disabled={index === 0}
          >
            上移
          </Button>
          <Button
            size="small"
            onClick={() => {
              props.onChange(reorderSelectorCondition(conditions, index, index + 1));
            }}
            disabled={index === conditions.length - 1}
          >
            下移
          </Button>
          <Button
            size="small"
            onClick={() => {
              props.onChange(removeSelectorCondition(conditions, index));
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
          props.onChange(addSelectorCondition(conditions));
        }}
      >
        添加条件
      </Button>
    </Space>
  );
}

