import { Input, Select, Space, TextArea, Typography } from "@douyinfe/semi-ui";
import type {
  MicroflowActionActivity,
  MicroflowExpression,
  MicroflowObject,
  MicroflowVariableSymbol
} from "../schema/types";

const { Text } = Typography;

export interface ExpressionEditorProps {
  value: MicroflowExpression;
  variables: MicroflowVariableSymbol[];
  issues?: string[];
  onChange: (value: MicroflowExpression) => void;
}

export function ExpressionEditor({ value, variables, issues = [], onChange }: ExpressionEditorProps) {
  return (
    <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
      <TextArea
        autosize
        value={value.raw ?? value.text}
        placeholder="Enter expression"
        onChange={raw => onChange({
          ...value,
          raw,
          text: raw,
          references: {
            ...(value.references ?? { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }),
            variables: value.references?.variables?.length ? value.references.variables : Array.from(raw.matchAll(/\$[A-Za-z_][\w]*/g)).map(match => match[0])
          }
        })}
      />
      <Select
        multiple
        style={{ width: "100%" }}
        placeholder="Referenced variables"
        value={value.references?.variables ?? value.referencedVariables ?? []}
        optionList={variables.map(variable => ({ label: `${variable.name}: ${variable.dataType.kind}`, value: variable.name }))}
        onChange={selected => {
          const selectedValues = Array.isArray(selected) ? selected.map(String) : [];
          onChange({
            ...value,
            references: { ...(value.references ?? { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }), variables: selectedValues },
            referencedVariables: selectedValues
          });
        }}
      />
      {issues.map(item => (
        <Text key={item} type="danger">
          {item}
        </Text>
      ))}
    </Space>
  );
}

export interface MicroflowPropertyFormProps {
  object?: MicroflowObject;
  variables: MicroflowVariableSymbol[];
}

function renderObjectRows(object: MicroflowObject): Array<[string, string]> {
  if (object.kind === "actionActivity") {
    const action = object.action;
    return [
      ["Action Kind", action.kind],
      ["Official Type", action.officialType],
      ["Error Handling", action.errorHandlingType],
      ["Category", action.editor.category],
      ["Availability", action.editor.availability],
      ...(action.kind === "retrieve"
        ? [
            ["Output Variable", action.outputVariableName],
            ["Retrieve Source", action.retrieveSource.kind],
            ["Entity", action.retrieveSource.kind === "database" ? (action.retrieveSource.entityQualifiedName ?? "") : ""],
            ["Association", action.retrieveSource.kind === "association" ? (action.retrieveSource.associationQualifiedName ?? "") : ""]
          ]
        : []),
      ...(action.kind === "restCall"
        ? [
            ["Method", action.request.method],
            ["URL", action.request.urlExpression.raw],
            ["Response Mode", action.response.handling.kind],
            ["Timeout", String(action.timeoutSeconds)]
          ]
        : [])
    ].filter(([, value]) => value.length > 0);
  }
  if (object.kind === "loopedActivity") {
    return [
      ["Loop Source", object.loopSource.kind],
      ["Error Handling", object.errorHandlingType],
      ["Nested Collection", object.objectCollection.id]
    ];
  }
  if (object.kind === "parameterObject") {
    return [["Parameter Id", object.parameterId]];
  }
  return [
    ["Kind", object.kind],
    ["Official Type", object.officialType]
  ];
}

export function MicroflowPropertyForm({ object }: MicroflowPropertyFormProps) {
  if (!object) {
    return <Text type="tertiary">Select an object to inspect AuthoringSchema properties.</Text>;
  }
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Input value={object.caption ?? object.id} readonly prefix="Title" />
      <Input value={object.officialType} readonly prefix="Official Type" />
      <Input value={object.kind} readonly prefix="Object Kind" />
      {renderObjectRows(object).map(([label, value]) => (
        <Input key={label} value={value} readonly prefix={label} />
      ))}
    </Space>
  );
}

export function isActionActivity(object: MicroflowObject | undefined): object is MicroflowActionActivity {
  return Boolean(object && object.kind === "actionActivity");
}

export const objectActivityFormKey = "actionActivity.object";
export const listActivityFormKey = "actionActivity.list";
export const variableActivityFormKey = "actionActivity.variable";
export const callActivityFormKey = "actionActivity.call";
export const integrationActivityFormKey = "actionActivity.integration";
export const errorHandlingFormKey = "action.errorHandling";
