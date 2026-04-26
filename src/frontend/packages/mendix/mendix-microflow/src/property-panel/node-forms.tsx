import { Button, Checkbox, Input, Select, Space, Switch, TextArea, Typography } from "@douyinfe/semi-ui";
import { IconPlus } from "@douyinfe/semi-icons";
import type {
  LegacyMicroflowActivityConfig,
  LegacyMicroflowAnnotationNode,
  LegacyMicroflowDecisionNode,
  LegacyMicroflowEventNode,
  LegacyMicroflowLoopNode,
  LegacyMicroflowMergeNode,
  LegacyMicroflowParameterNode,
  MicroflowTypeRef
} from "../schema";
import {
  AssociationSelector,
  AttributeSelector,
  EntitySelector,
  ExpressionEditor,
  FieldRow,
  KeyValueEditor,
  VariableSelector,
  createExpression,
  primitiveType
} from "./controls";
import { MicroflowBasicSection } from "./sections";
import type { MicroflowActivityFormProps, MicroflowNodeFormProps, MicroflowNodeFormRegistry, MicroflowNodeFormRegistryItem } from "./types";

const { Text } = Typography;

function typeRef(name: string): MicroflowTypeRef {
  if (["Void", "Object", "List"].includes(name)) {
    return { kind: name === "Void" ? "void" : name.toLowerCase() as "object" | "list", name };
  }
  return primitiveType(name);
}

function updateEventConfig(props: MicroflowNodeFormProps<LegacyMicroflowEventNode>, patch: Partial<LegacyMicroflowEventNode["config"]>) {
  props.onPatch({ config: patch });
}

function updateActivityConfig(props: MicroflowActivityFormProps, patch: Partial<LegacyMicroflowActivityConfig>) {
  props.onPatch({ config: patch });
}

function assignmentEditor(props: MicroflowActivityFormProps, entity?: string) {
  const assignments = props.node.config.assignments ?? [];
  return (
    <FieldRow label="Attribute assignments">
      <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
        {assignments.map((assignment, index) => (
          <div key={assignment.id} style={{ display: "grid", gridTemplateColumns: "120px minmax(0, 1fr) auto", gap: 6, width: "100%" }}>
            <AttributeSelector
              entity={entity}
              value={assignment.attribute}
              readonly={props.readonly}
              onChange={attribute => updateActivityConfig(props, { assignments: assignments.map((item, itemIndex) => itemIndex === index ? { ...item, attribute } : item) })}
            />
            <Input
              readonly={props.readonly}
              value={assignment.expression.text}
              placeholder="Expression or value"
              onChange={text => updateActivityConfig(props, { assignments: assignments.map((item, itemIndex) => itemIndex === index ? { ...item, expression: { ...item.expression, text } } : item) })}
            />
            <Button disabled={props.readonly} type="danger" theme="borderless" onClick={() => updateActivityConfig(props, { assignments: assignments.filter((_, itemIndex) => itemIndex !== index) })}>Delete</Button>
          </div>
        ))}
        <Button
          disabled={props.readonly}
          icon={<IconPlus />}
          onClick={() => updateActivityConfig(props, {
            assignments: [...assignments, { id: `assignment-${Date.now()}`, attribute: "", expression: createExpression() }]
          })}
        >
          Add assignment
        </Button>
      </Space>
    </FieldRow>
  );
}

export function MicroflowStartEventForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowNodeFormProps<LegacyMicroflowEventNode>;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Start event type">
        <Select
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={props.node.config.startTrigger ?? "manual"}
          optionList={[
            { label: "Manual", value: "manual" },
            { label: "Page event", value: "pageEvent" },
            { label: "Form submit", value: "formSubmit" },
            { label: "Workflow call", value: "workflowCall" },
            { label: "API call", value: "apiCall" }
          ]}
          onChange={startTrigger => updateEventConfig(props, { startTrigger: String(startTrigger) as LegacyMicroflowEventNode["config"]["startTrigger"] })}
        />
      </FieldRow>
      <FieldRow label="Allow external call">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.allowExternalCall)} onChange={allowExternalCall => updateEventConfig(props, { allowExternalCall })} />
      </FieldRow>
      <FieldRow label="Record execution log">
        <Switch disabled={props.readonly} checked={props.node.config.logExecution ?? true} onChange={logExecution => updateEventConfig(props, { logExecution })} />
      </FieldRow>
      <FieldRow label="Input parameter preview">
        <Space vertical align="start" spacing={4}>
          {props.schema.parameters.length === 0 ? <Text type="tertiary">No input parameters.</Text> : props.schema.parameters.map(parameter => (
            <Text key={parameter.id} type="tertiary">{parameter.name}: {parameter.type.name}</Text>
          ))}
        </Space>
      </FieldRow>
    </Space>
  );
}

export function MicroflowEndEventForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowNodeFormProps<LegacyMicroflowEventNode>;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="End type">
        <Select
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={props.node.config.endType ?? "normal"}
          optionList={[
            { label: "Normal end", value: "normal" },
            { label: "Return result", value: "returnValue" },
            { label: "Throw error", value: "throwError" }
          ]}
          onChange={endType => updateEventConfig(props, { endType: String(endType) as LegacyMicroflowEventNode["config"]["endType"] })}
        />
      </FieldRow>
      <FieldRow label="Return value type">
        <Select
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={props.node.config.returnType?.name ?? "Void"}
          optionList={["Void", "Boolean", "String", "Integer", "Decimal", "DateTime", "Object", "List"].map(name => ({ label: name, value: name }))}
          onChange={name => updateEventConfig(props, { returnType: typeRef(String(name)) })}
        />
      </FieldRow>
      <FieldRow label="Return expression">
        <ExpressionEditor
          readonly={props.readonly}
          value={props.node.config.returnValue}
          variables={props.variables}
          onChange={returnValue => updateEventConfig(props, { returnValue })}
        />
      </FieldRow>
      <FieldRow label="Return variable">
        <VariableSelector value={props.node.config.returnVariableName} variables={props.variables} readonly={props.readonly} onChange={returnVariableName => updateEventConfig(props, { returnVariableName })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowDecisionForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowNodeFormProps<LegacyMicroflowDecisionNode>;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Condition expression" required>
        <ExpressionEditor
          required
          readonly={props.readonly}
          value={props.node.config.expression}
          variables={props.variables}
          onChange={expression => props.onPatch({ config: { expression } })}
        />
      </FieldRow>
      <FieldRow label="True branch label">
        <Input readonly={props.readonly} value={String((props.node.config as unknown as Record<string, unknown>).trueLabel ?? "Yes")} onChange={trueLabel => props.onPatch({ config: { trueLabel } })} />
      </FieldRow>
      <FieldRow label="False branch label">
        <Input readonly={props.readonly} value={String((props.node.config as unknown as Record<string, unknown>).falseLabel ?? "No")} onChange={falseLabel => props.onPatch({ config: { falseLabel } })} />
      </FieldRow>
      <Text type="tertiary">Expression should return Boolean. Multi-branch decisions are reserved.</Text>
    </Space>
  );
}

export function MicroflowMergeForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowNodeFormProps<LegacyMicroflowMergeNode>;
  const incoming = props.edges.filter(edge => edge.targetNodeId === props.node.id).length;
  const outgoing = props.edges.filter(edge => edge.sourceNodeId === props.node.id).length;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Merge strategy">
        <Select
          disabled={props.readonly}
          style={{ width: "100%" }}
          value={props.node.config.strategy}
          optionList={[
            { label: "Any branch arrives", value: "firstAvailable" },
            { label: "All branches arrive", value: "all" }
          ]}
          onChange={strategy => props.onPatch({ config: { strategy: String(strategy) } })}
        />
      </FieldRow>
      <Input readonly prefix="Incoming edges" value={String(incoming)} />
      <Input readonly prefix="Outgoing edges" value={String(outgoing)} />
    </Space>
  );
}

export function MicroflowLoopForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowNodeFormProps<LegacyMicroflowLoopNode>;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Loop type">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={props.node.config.loopType ?? "list"} optionList={[{ label: "For each list", value: "list" }, { label: "Conditional loop", value: "condition" }]} onChange={loopType => props.onPatch({ config: { loopType } })} />
      </FieldRow>
      <FieldRow label="List variable" required error={!props.node.config.iterableVariableName ? "List variable is required." : undefined}>
        <VariableSelector value={props.node.config.iterableVariableName} variables={props.variables} readonly={props.readonly} onChange={iterableVariableName => props.onPatch({ config: { iterableVariableName } })} />
      </FieldRow>
      <FieldRow label="Current item variable" required error={!props.node.config.itemVariableName ? "Current item variable is required." : undefined}>
        <Input readonly={props.readonly} value={props.node.config.itemVariableName} onChange={itemVariableName => props.onPatch({ config: { itemVariableName } })} />
      </FieldRow>
      <FieldRow label="Index variable">
        <Input readonly={props.readonly} value={props.node.config.indexVariableName ?? ""} onChange={indexVariableName => props.onPatch({ config: { indexVariableName } })} />
      </FieldRow>
      <FieldRow label="Skip when list is empty">
        <Checkbox disabled={props.readonly} checked={Boolean(props.node.config.skipWhenEmpty)} onChange={event => props.onPatch({ config: { skipWhenEmpty: Boolean(event.target.checked) } })} />
      </FieldRow>
      <FieldRow label="Note">
        <TextArea autosize readonly={props.readonly} value={props.node.config.note ?? ""} onChange={note => props.onPatch({ config: { note } })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowParameterForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowNodeFormProps<LegacyMicroflowParameterNode>;
  const parameter = props.node.config.parameter;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Parameter name" required error={!parameter.name ? "Parameter name is required." : undefined}>
        <Input readonly={props.readonly} value={parameter.name} onChange={name => props.onPatch({ config: { parameter: { ...parameter, name } } })} />
      </FieldRow>
      <FieldRow label="Parameter type" required>
        <Select disabled={props.readonly} style={{ width: "100%" }} value={parameter.type.name} optionList={["String", "Boolean", "Integer", "Decimal", "DateTime", "Object", "List"].map(name => ({ label: name, value: name }))} onChange={name => props.onPatch({ config: { parameter: { ...parameter, type: typeRef(String(name)) } } })} />
      </FieldRow>
      <FieldRow label="Required">
        <Switch disabled={props.readonly} checked={parameter.required} onChange={required => props.onPatch({ config: { parameter: { ...parameter, required } } })} />
      </FieldRow>
      <FieldRow label="Default value">
        <ExpressionEditor readonly={props.readonly} value={props.node.config.defaultValue} variables={props.variables} onChange={defaultValue => props.onPatch({ config: { defaultValue } })} />
      </FieldRow>
      <FieldRow label="Example value">
        <Input readonly={props.readonly} value={props.node.config.exampleValue ?? ""} onChange={exampleValue => props.onPatch({ config: { exampleValue } })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowAnnotationForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowNodeFormProps<LegacyMicroflowAnnotationNode>;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Title">
        <Input readonly={props.readonly} value={props.node.config.title ?? ""} onChange={title => props.onPatch({ config: { title } })} />
      </FieldRow>
      <FieldRow label="Annotation content">
        <TextArea autosize readonly={props.readonly} value={props.node.config.text} onChange={text => props.onPatch({ config: { text } })} />
      </FieldRow>
      <FieldRow label="Color">
        <Input readonly={props.readonly} value={props.node.config.color ?? ""} placeholder="#fff7e0" onChange={color => props.onPatch({ config: { color } })} />
      </FieldRow>
      <FieldRow label="Pinned">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.pinned)} onChange={pinned => props.onPatch({ config: { pinned } })} />
      </FieldRow>
      <FieldRow label="Export to documentation">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.exportToDocumentation)} onChange={exportToDocumentation => props.onPatch({ config: { exportToDocumentation } })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowObjectRetrieveForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  const config = props.node.config;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Retrieve mode">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={config.retrieveMode ?? "database"} optionList={[{ label: "By association", value: "association" }, { label: "From database", value: "database" }]} onChange={retrieveMode => updateActivityConfig(props, { retrieveMode: String(retrieveMode) as LegacyMicroflowActivityConfig["retrieveMode"] })} />
      </FieldRow>
      <FieldRow label="Entity" required error={!config.entity ? "Entity is required." : undefined}>
        <EntitySelector value={config.entity} readonly={props.readonly} onChange={entity => updateActivityConfig(props, { entity })} />
      </FieldRow>
      {config.retrieveMode === "association" ? (
        <FieldRow label="Association" required error={!config.association ? "Association is required." : undefined}>
          <AssociationSelector startEntityQualifiedName={config.entity} value={config.association} readonly={props.readonly} onChange={association => updateActivityConfig(props, { association })} />
        </FieldRow>
      ) : null}
      <FieldRow label="Condition XPath / expression">
        <ExpressionEditor readonly={props.readonly} value={config.valueExpression} variables={props.variables} onChange={valueExpression => updateActivityConfig(props, { valueExpression })} />
      </FieldRow>
      <FieldRow label="Sort">
        <Input readonly={props.readonly} value={config.sort ?? ""} placeholder="CreatedDate desc" onChange={sort => updateActivityConfig(props, { sort })} />
      </FieldRow>
      <FieldRow label="Range">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={config.range ?? "all"} optionList={[{ label: "All", value: "all" }, { label: "First", value: "first" }, { label: "Limit", value: "limit" }]} onChange={range => updateActivityConfig(props, { range: String(range) as LegacyMicroflowActivityConfig["range"] })} />
      </FieldRow>
      <FieldRow label="Limit">
        <Input readonly={props.readonly} value={String(config.limit ?? "")} onChange={limit => updateActivityConfig(props, { limit: Number(limit) || undefined })} />
      </FieldRow>
      <FieldRow label="Output variable">
        <Input readonly={props.readonly} value={config.listVariableName ?? config.resultVariableName ?? ""} onChange={listVariableName => updateActivityConfig(props, { listVariableName })} />
      </FieldRow>
      <FieldRow label="Not found strategy">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={config.notFoundStrategy ?? "empty"} optionList={[{ label: "Return empty", value: "empty" }, { label: "Throw error", value: "throw" }, { label: "Use error handling", value: "errorFlow" }]} onChange={notFoundStrategy => updateActivityConfig(props, { notFoundStrategy: String(notFoundStrategy) as LegacyMicroflowActivityConfig["notFoundStrategy"] })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowObjectCreateForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Entity" required error={!props.node.config.entity ? "Entity is required." : undefined}>
        <EntitySelector value={props.node.config.entity} readonly={props.readonly} onChange={entity => updateActivityConfig(props, { entity })} />
      </FieldRow>
      <FieldRow label="Output variable" required error={!props.node.config.objectVariableName ? "Output variable is required." : undefined}>
        <Input readonly={props.readonly} value={props.node.config.objectVariableName ?? ""} onChange={objectVariableName => updateActivityConfig(props, { objectVariableName })} />
      </FieldRow>
      {assignmentEditor(props, props.node.config.entity)}
      <FieldRow label="Commit immediately">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.commitImmediately)} onChange={commitImmediately => updateActivityConfig(props, { commitImmediately })} />
      </FieldRow>
      <FieldRow label="Refresh client">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.refreshClient)} onChange={refreshClient => updateActivityConfig(props, { refreshClient })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowObjectChangeForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Target object variable" required error={!props.node.config.objectVariableName ? "Target object is required." : undefined}>
        <VariableSelector value={props.node.config.objectVariableName} variables={props.variables} readonly={props.readonly} onChange={objectVariableName => updateActivityConfig(props, { objectVariableName })} />
      </FieldRow>
      {assignmentEditor(props, props.node.config.entity)}
      <FieldRow label="Commit">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.commitImmediately)} onChange={commitImmediately => updateActivityConfig(props, { commitImmediately })} />
      </FieldRow>
      <FieldRow label="With events">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.withEvents)} onChange={withEvents => updateActivityConfig(props, { withEvents })} />
      </FieldRow>
      <FieldRow label="Refresh client">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.refreshClient)} onChange={refreshClient => updateActivityConfig(props, { refreshClient })} />
      </FieldRow>
      <FieldRow label="Validate object">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.validateObject)} onChange={validateObject => updateActivityConfig(props, { validateObject })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowObjectCommitForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Target object variable" required>
        <VariableSelector value={props.node.config.objectVariableName} variables={props.variables} readonly={props.readonly} onChange={objectVariableName => updateActivityConfig(props, { objectVariableName })} />
      </FieldRow>
      <FieldRow label="With events">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.withEvents)} onChange={withEvents => updateActivityConfig(props, { withEvents })} />
      </FieldRow>
      <FieldRow label="Refresh client">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.refreshClient)} onChange={refreshClient => updateActivityConfig(props, { refreshClient })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowObjectDeleteForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Target object variable" required>
        <VariableSelector value={props.node.config.objectVariableName} variables={props.variables} readonly={props.readonly} onChange={objectVariableName => updateActivityConfig(props, { objectVariableName })} />
      </FieldRow>
      <FieldRow label="Delete list">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.deleteList)} onChange={deleteList => updateActivityConfig(props, { deleteList })} />
      </FieldRow>
      <FieldRow label="With events">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.withEvents)} onChange={withEvents => updateActivityConfig(props, { withEvents })} />
      </FieldRow>
      <FieldRow label="Delete confirmation">
        <TextArea autosize readonly={props.readonly} value={props.node.config.deleteConfirmation ?? ""} onChange={deleteConfirmation => updateActivityConfig(props, { deleteConfirmation })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowObjectRollbackForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Target object variable" required>
        <VariableSelector value={props.node.config.objectVariableName} variables={props.variables} readonly={props.readonly} onChange={objectVariableName => updateActivityConfig(props, { objectVariableName })} />
      </FieldRow>
      <FieldRow label="Rollback scope">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={props.node.config.rollbackScope ?? "object"} optionList={[{ label: "Current object", value: "object" }, { label: "Object list", value: "list" }, { label: "Current transaction", value: "transaction" }]} onChange={rollbackScope => updateActivityConfig(props, { rollbackScope: String(rollbackScope) as LegacyMicroflowActivityConfig["rollbackScope"] })} />
      </FieldRow>
      <Text type="tertiary">Rollback restores object state according to the selected scope.</Text>
    </Space>
  );
}

export function MicroflowVariableCreateForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Variable name" required>
        <Input readonly={props.readonly} value={props.node.config.variableName ?? ""} onChange={variableName => updateActivityConfig(props, { variableName })} />
      </FieldRow>
      <FieldRow label="Variable type">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={props.node.config.variableType?.name ?? "String"} optionList={["String", "Boolean", "Integer", "Decimal", "DateTime", "Object", "List"].map(name => ({ label: name, value: name }))} onChange={name => updateActivityConfig(props, { variableType: typeRef(String(name)) })} />
      </FieldRow>
      <FieldRow label="Initial value">
        <ExpressionEditor readonly={props.readonly} value={props.node.config.valueExpression} variables={props.variables} onChange={valueExpression => updateActivityConfig(props, { valueExpression })} />
      </FieldRow>
      <FieldRow label="Readonly">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.readonly)} onChange={readonly => updateActivityConfig(props, { readonly })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowVariableChangeForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Target variable" required>
        <VariableSelector value={props.node.config.variableName} variables={props.variables} readonly={props.readonly} onChange={variableName => updateActivityConfig(props, { variableName })} />
      </FieldRow>
      <FieldRow label="New value expression" required>
        <ExpressionEditor required readonly={props.readonly} value={props.node.config.valueExpression} variables={props.variables} onChange={valueExpression => updateActivityConfig(props, { valueExpression })} />
      </FieldRow>
      <Text type="tertiary">Expression type should match the target variable type.</Text>
    </Space>
  );
}

export function MicroflowCallMicroflowForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Target microflow" required>
        <Input readonly={props.readonly} value={props.node.config.targetMicroflowId ?? ""} onChange={targetMicroflowId => updateActivityConfig(props, { targetMicroflowId })} />
      </FieldRow>
      <FieldRow label="Return variable">
        <Input readonly={props.readonly} value={props.node.config.resultVariableName ?? ""} onChange={resultVariableName => updateActivityConfig(props, { resultVariableName })} />
      </FieldRow>
      <FieldRow label="Call mode">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={props.node.config.callMode ?? "sync"} optionList={[{ label: "Sync", value: "sync" }, { label: "Async", value: "async" }]} onChange={callMode => updateActivityConfig(props, { callMode: String(callMode) as LegacyMicroflowActivityConfig["callMode"] })} />
      </FieldRow>
      <Text type="tertiary">Parameter mapping editor is reserved for typed microflow contracts.</Text>
    </Space>
  );
}

export function MicroflowCallRestForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  const config = props.node.config;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Method" required>
        <Select disabled={props.readonly} style={{ width: "100%" }} value={config.method ?? "GET"} optionList={["GET", "POST", "PUT", "PATCH", "DELETE"].map(method => ({ label: method, value: method }))} onChange={method => updateActivityConfig(props, { method: String(method) as LegacyMicroflowActivityConfig["method"] })} />
      </FieldRow>
      <FieldRow label="URL" required error={!config.url ? "URL is required." : undefined}>
        <Input readonly={props.readonly} value={config.url ?? ""} onChange={url => updateActivityConfig(props, { url })} />
      </FieldRow>
      <FieldRow label="Headers">
        <KeyValueEditor readonly={props.readonly} value={config.headers ?? []} onChange={headers => updateActivityConfig(props, { headers })} />
      </FieldRow>
      <FieldRow label="Query params">
        <KeyValueEditor readonly={props.readonly} value={config.query ?? []} onChange={query => updateActivityConfig(props, { query })} />
      </FieldRow>
      <FieldRow label="Body type">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={config.bodyType ?? "none"} optionList={["none", "json", "form", "text"].map(item => ({ label: item, value: item }))} onChange={bodyType => updateActivityConfig(props, { bodyType: String(bodyType) as LegacyMicroflowActivityConfig["bodyType"] })} />
      </FieldRow>
      <FieldRow label="Body">
        <ExpressionEditor readonly={props.readonly} value={config.bodyExpression} variables={props.variables} onChange={bodyExpression => updateActivityConfig(props, { bodyExpression })} />
      </FieldRow>
      <FieldRow label="Timeout seconds">
        <Input readonly={props.readonly} value={String(config.timeoutMs ? Math.round(config.timeoutMs / 1000) : "")} onChange={seconds => updateActivityConfig(props, { timeoutMs: (Number(seconds) || 0) * 1000 || undefined })} />
      </FieldRow>
      <FieldRow label="Response mapping">
        <Input readonly={props.readonly} value={config.responseMapping ?? ""} onChange={responseMapping => updateActivityConfig(props, { responseMapping })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowLogMessageForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Log level">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={props.node.config.logLevel ?? "info"} optionList={["debug", "info", "warn", "error"].map(level => ({ label: level, value: level }))} onChange={logLevel => updateActivityConfig(props, { logLevel: String(logLevel) as LegacyMicroflowActivityConfig["logLevel"] })} />
      </FieldRow>
      <FieldRow label="Log content" required>
        <ExpressionEditor required readonly={props.readonly} value={props.node.config.messageExpression} variables={props.variables} onChange={messageExpression => updateActivityConfig(props, { messageExpression })} />
      </FieldRow>
      <FieldRow label="Record context variables">
        <Switch disabled={props.readonly} checked={Boolean(props.node.config.logContextVariables)} onChange={logContextVariables => updateActivityConfig(props, { logContextVariables })} />
      </FieldRow>
      <FieldRow label="Record traceId">
        <Switch disabled={props.readonly} checked={props.node.config.logTraceId ?? true} onChange={logTraceId => updateActivityConfig(props, { logTraceId })} />
      </FieldRow>
    </Space>
  );
}

export function MicroflowShowPageForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Page" required>
        <Input readonly={props.readonly} value={props.node.config.pageName ?? ""} onChange={pageName => updateActivityConfig(props, { pageName })} />
      </FieldRow>
      <FieldRow label="Open mode">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={props.node.config.pageOpenMode ?? "current"} optionList={[{ label: "Current page", value: "current" }, { label: "Modal", value: "modal" }, { label: "New window", value: "newWindow" }]} onChange={pageOpenMode => updateActivityConfig(props, { pageOpenMode: String(pageOpenMode) as LegacyMicroflowActivityConfig["pageOpenMode"] })} />
      </FieldRow>
      <FieldRow label="Title">
        <Input readonly={props.readonly} value={props.node.config.pageTitle ?? ""} onChange={pageTitle => updateActivityConfig(props, { pageTitle })} />
      </FieldRow>
      <Text type="tertiary">Page parameter mapping is reserved for page metadata integration.</Text>
    </Space>
  );
}

export function MicroflowClosePageForm(rawProps: MicroflowNodeFormProps) {
  const props = rawProps as MicroflowActivityFormProps;
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <FieldRow label="Close mode">
        <Select disabled={props.readonly} style={{ width: "100%" }} value={props.node.config.closeMode ?? "current"} optionList={[{ label: "Current page", value: "current" }, { label: "Specific modal", value: "modal" }, { label: "Back", value: "back" }]} onChange={closeMode => updateActivityConfig(props, { closeMode: String(closeMode) as LegacyMicroflowActivityConfig["closeMode"] })} />
      </FieldRow>
      <FieldRow label="Return result">
        <Input readonly={props.readonly} value={props.node.config.closeResult ?? ""} onChange={closeResult => updateActivityConfig(props, { closeResult })} />
      </FieldRow>
    </Space>
  );
}

function unsupportedForm(props: MicroflowNodeFormProps) {
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <MicroflowBasicSection props={props} />
      <Text type="tertiary">This node type does not have a dedicated form yet.</Text>
      <TextArea readonly autosize={{ minRows: 6 }} value={JSON.stringify(props.node.config, null, 2)} />
    </Space>
  );
}

function registryItem(renderProperties: (props: MicroflowNodeFormProps) => JSX.Element, tabs: MicroflowNodeFormRegistryItem["tabs"]): MicroflowNodeFormRegistryItem {
  return { renderProperties, tabs };
}

export function getMicroflowNodeFormKey(node: MicroflowNodeFormProps["node"]): string {
  return node.type === "activity" ? `activity:${node.config.activityType}` : node.type;
}

export const microflowNodeFormRegistry: MicroflowNodeFormRegistry = {
  startEvent: registryItem(MicroflowStartEventForm, ["properties", "documentation", "output"]),
  endEvent: registryItem(MicroflowEndEventForm, ["properties", "documentation", "output"]),
  errorEvent: registryItem(MicroflowEndEventForm, ["properties", "documentation", "output"]),
  breakEvent: registryItem(MicroflowStartEventForm, ["properties", "documentation"]),
  continueEvent: registryItem(MicroflowStartEventForm, ["properties", "documentation"]),
  decision: registryItem(MicroflowDecisionForm, ["properties", "documentation", "output"]),
  objectTypeDecision: registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  merge: registryItem(MicroflowMergeForm, ["properties", "documentation", "output"]),
  loop: registryItem(MicroflowLoopForm, ["properties", "documentation", "output"]),
  parameter: registryItem(MicroflowParameterForm, ["properties", "documentation", "output"]),
  annotation: registryItem(MicroflowAnnotationForm, ["properties", "documentation"]),
  "activity:objectRetrieve": registryItem(MicroflowObjectRetrieveForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:objectCast": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:objectCreate": registryItem(MicroflowObjectCreateForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:objectChange": registryItem(MicroflowObjectChangeForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:objectCommit": registryItem(MicroflowObjectCommitForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:objectDelete": registryItem(MicroflowObjectDeleteForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:objectRollback": registryItem(MicroflowObjectRollbackForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:variableCreate": registryItem(MicroflowVariableCreateForm, ["properties", "documentation", "output"]),
  "activity:variableChange": registryItem(MicroflowVariableChangeForm, ["properties", "documentation", "output"]),
  "activity:callMicroflow": registryItem(MicroflowCallMicroflowForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:callJavaAction": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:callJavaScriptAction": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:callRest": registryItem(MicroflowCallRestForm, ["properties", "documentation", "errorHandling", "output", "advanced"]),
  "activity:callWebService": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:callExternalAction": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:importWithMapping": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:exportWithMapping": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:queryExternalDatabase": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:sendRestRequestBeta": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:logMessage": registryItem(MicroflowLogMessageForm, ["properties", "documentation"]),
  "activity:showPage": registryItem(MicroflowShowPageForm, ["properties", "documentation", "output"]),
  "activity:closePage": registryItem(MicroflowClosePageForm, ["properties", "documentation"]),
  "activity:downloadFile": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:showHomePage": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:showMessage": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:synchronizeToDevice": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:validationFeedback": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:synchronize": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:listOperation": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:listAggregate": registryItem(unsupportedForm, ["properties", "documentation", "output"]),
  "activity:listCreate": registryItem(unsupportedForm, ["properties", "documentation", "output"]),
  "activity:listChange": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:callNanoflow": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:generateDocument": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:counter": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:incrementCounter": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:gauge": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:callMlModel": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:applyJumpToOption": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling"]),
  "activity:callWorkflow": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling", "output"]),
  "activity:changeWorkflowState": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling"]),
  "activity:completeUserTask": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling"]),
  "activity:generateJumpToOptions": registryItem(unsupportedForm, ["properties", "documentation", "output"]),
  "activity:retrieveWorkflowActivityRecords": registryItem(unsupportedForm, ["properties", "documentation", "output"]),
  "activity:retrieveWorkflowContext": registryItem(unsupportedForm, ["properties", "documentation", "output"]),
  "activity:retrieveWorkflows": registryItem(unsupportedForm, ["properties", "documentation", "output"]),
  "activity:showUserTaskPage": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:showWorkflowAdminPage": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:lockWorkflow": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:unlockWorkflow": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:notifyWorkflow": registryItem(unsupportedForm, ["properties", "documentation"]),
  "activity:deleteExternalObject": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling"]),
  "activity:sendExternalObject": registryItem(unsupportedForm, ["properties", "documentation", "errorHandling"])
};
