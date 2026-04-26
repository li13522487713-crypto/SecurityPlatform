import { Input, Select, Space, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowExpression, MicroflowNode, MicroflowVariable } from "../schema/types";

const { Text } = Typography;

export interface ExpressionEditorProps {
  value: MicroflowExpression;
  variables: MicroflowVariable[];
  issues?: string[];
  onChange: (value: MicroflowExpression) => void;
}

export function ExpressionEditor({ value, variables, issues = [], onChange }: ExpressionEditorProps) {
  return (
    <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
      <TextArea
        autosize
        value={value.text}
        placeholder="Enter expression"
        onChange={text => onChange({ ...value, text })}
      />
      <Select
        multiple
        style={{ width: "100%" }}
        placeholder="Referenced variables"
        value={value.referencedVariables ?? []}
        optionList={variables.map(variable => ({ label: `${variable.name}: ${variable.type.name}`, value: variable.name }))}
        onChange={selected => {
          const selectedValues = Array.isArray(selected) ? selected.map(String) : [];
          onChange({ ...value, referencedVariables: selectedValues });
        }}
      />
      <Text type="tertiary">Type hints are reserved for the pluggable expression engine.</Text>
      {issues.map(item => (
        <Text key={item} type="danger">
          {item}
        </Text>
      ))}
    </Space>
  );
}

export interface MicroflowPropertyFormProps {
  node?: MicroflowNode;
  variables: MicroflowVariable[];
}

function renderConfigRows(node: MicroflowNode): Array<[string, string]> {
  if (node.type === "activity") {
    return [
      ["Activity Type", node.config.activityType],
      ["Entity", node.config.entity ?? ""],
      ["Association", node.config.association ?? ""],
      ["Object Variable", node.config.objectVariableName ?? ""],
      ["List Variable", node.config.listVariableName ?? ""],
      ["Variable", node.config.variableName ?? ""],
      ["Target Microflow", node.config.targetMicroflowId ?? ""],
      ["Method", node.config.method ?? ""],
      ["URL", node.config.url ?? ""],
      ["Error Handling", node.config.errorHandling?.mode ?? ""]
    ].filter(([, value]) => value.length > 0);
  }

  if (node.type === "loop") {
    return [
      ["Iterable", node.config.iterableVariableName],
      ["Item Variable", node.config.itemVariableName]
    ];
  }

  if (node.type === "parameter") {
    return [
      ["Name", node.config.parameter.name],
      ["Type", node.config.parameter.type.name],
      ["Required", String(node.config.parameter.required)]
    ];
  }

  if (node.type === "annotation") {
    return [["Text", node.config.text]];
  }

  if (node.type === "decision") {
    return [["Expression", node.config.expression.text]];
  }

  if (node.type === "merge") {
    return [["Strategy", node.config.strategy]];
  }

  if (node.type === "endEvent" && node.config.returnValue) {
    return [["Return Value", node.config.returnValue.text]];
  }

  return [["Type", node.type]];
}

export function MicroflowPropertyForm({ node }: MicroflowPropertyFormProps) {
  if (!node) {
    return <Text type="tertiary">Select a node to inspect its properties.</Text>;
  }

  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Input value={node.title} readonly prefix="Title" />
      <Input value={node.propertyForm.formKey} readonly prefix="Form" />
      {renderConfigRows(node).map(([label, value]) => (
        <Input key={label} value={value} readonly prefix={label} />
      ))}
    </Space>
  );
}

export const objectActivityFormKey = "objectActivity";
export const listActivityFormKey = "listActivity";
export const variableActivityFormKey = "variableActivity";
export const callActivityFormKey = "callActivity";
export const integrationActivityFormKey = "integrationActivity";
export const errorHandlingFormKey = "errorHandling";
