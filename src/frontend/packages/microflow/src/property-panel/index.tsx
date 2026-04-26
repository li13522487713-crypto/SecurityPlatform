import type { ReactNode } from "react";
import { Empty, Input, InputNumber, Select, Space, Switch, Tag, TextArea, Typography, Button } from "@douyinfe/semi-ui";
import { IconCopy, IconDelete, IconClose } from "@douyinfe/semi-icons";
import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowDataType,
  MicroflowExpression,
  MicroflowFlow,
  MicroflowObject,
  MicroflowVariableSymbol,
  MicroflowSequenceFlow
} from "../schema";
import type { MicroflowEdgePatch, MicroflowNodePatch, MicroflowPropertyPanelProps } from "./types";

export * from "./controls";
export * from "./node-forms";
export * from "./sections";
export * from "./types";

export function buildVariablesForPropertyPanel(schema: { variables: Record<string, Record<string, MicroflowVariableSymbol>> }): MicroflowVariableSymbol[] {
  return Object.values(schema.variables).flatMap(group => Object.values(group));
}

const { Text, Title } = Typography;

function expression(raw = "", inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: []
  };
}

function objectTitle(object: MicroflowObject): string {
  if (object.kind === "actionActivity") {
    return `${object.caption} (${object.action.kind})`;
  }
  return object.caption ?? object.kind;
}

function actionPatch(action: MicroflowAction, patch: Partial<MicroflowAction>): MicroflowAction {
  return { ...action, ...patch } as MicroflowAction;
}

function updateAction(activity: MicroflowActionActivity, patch: Partial<MicroflowAction>): MicroflowActionActivity {
  return { ...activity, action: actionPatch(activity.action, patch) };
}

function issuesFor(props: MicroflowPropertyPanelProps, objectId?: string, flowId?: string, actionId?: string) {
  return props.validationIssues.filter(issue =>
    (objectId && issue.objectId === objectId) ||
    (flowId && issue.flowId === flowId) ||
    (actionId && issue.actionId === actionId)
  );
}

function Header({ props, title, subtitle, onDelete, onDuplicate }: {
  props: MicroflowPropertyPanelProps;
  title: string;
  subtitle: string;
  onDelete?: () => void;
  onDuplicate?: () => void;
}) {
  return (
    <div style={{ padding: 14, borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
      <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
        <div style={{ minWidth: 0 }}>
          <Title heading={6} style={{ margin: 0 }}>{title}</Title>
          <Text size="small" type="tertiary">{subtitle}</Text>
        </div>
        <Space>
          {onDuplicate ? <Button icon={<IconCopy />} theme="borderless" onClick={onDuplicate} disabled={props.readonly} /> : null}
          {onDelete ? <Button icon={<IconDelete />} theme="borderless" type="danger" onClick={onDelete} disabled={props.readonly} /> : null}
          <Button icon={<IconClose />} theme="borderless" onClick={props.onClose} />
        </Space>
      </Space>
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label style={{ display: "grid", gap: 6 }}>
      <Text size="small" strong>{label}</Text>
      {children}
    </label>
  );
}

function ActionActivityFields({
  object,
  readonly,
  onPatch
}: {
  object: MicroflowActionActivity;
  readonly?: boolean;
  onPatch: (patch: MicroflowNodePatch) => void;
}) {
  const action = object.action;
  const patchObject = (next: MicroflowActionActivity) => onPatch({ object: next });
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Field label="Caption">
        <Input value={object.caption} disabled={readonly} onChange={caption => patchObject({ ...object, caption })} />
      </Field>
      <Field label="Auto Generate Caption">
        <Switch checked={object.autoGenerateCaption} disabled={readonly} onChange={autoGenerateCaption => patchObject({ ...object, autoGenerateCaption })} />
      </Field>
      <Field label="Background Color">
        <Select
          value={object.backgroundColor}
          disabled={readonly}
          style={{ width: "100%" }}
          onChange={value => patchObject({ ...object, backgroundColor: String(value) as MicroflowActionActivity["backgroundColor"] })}
          optionList={["default", "blue", "green", "orange", "red", "purple", "gray"].map(value => ({ label: value, value }))}
        />
      </Field>
      <Field label="Error Handling">
        <Select
          value={action.errorHandlingType}
          disabled={readonly || action.kind === "rollback"}
          style={{ width: "100%" }}
          onChange={value => patchObject(updateAction(object, { errorHandlingType: String(value) as MicroflowAction["errorHandlingType"] }))}
          optionList={["rollback", "customWithRollback", "customWithoutRollback", "continue"].map(value => ({ label: value, value }))}
        />
      </Field>

      {action.kind === "retrieve" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Retrieve Source</Title>
          <Field label="Output Variable">
            <Input value={action.outputVariableName} disabled={readonly} onChange={outputVariableName => patchObject(updateAction(object, { outputVariableName }))} />
          </Field>
          <Field label="Source Type">
            <Select
              value={action.retrieveSource.kind}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={value => {
                const source = String(value) === "association"
                  ? { kind: "association" as const, officialType: "Microflows$AssociationRetrieveSource" as const, associationQualifiedName: null, startVariableName: "" }
                  : {
                      kind: "database" as const,
                      officialType: "Microflows$DatabaseRetrieveSource" as const,
                      entityQualifiedName: null,
                      xPathConstraint: null,
                      sortItemList: { items: [] },
                      range: { kind: "all" as const, officialType: "Microflows$ConstantRange" as const, value: "all" as const }
                    };
                patchObject(updateAction(object, { retrieveSource: source }));
              }}
              optionList={[{ label: "From Database", value: "database" }, { label: "By Association", value: "association" }]}
            />
          </Field>
          {action.retrieveSource.kind === "database" ? (
            <>
              <Field label="Entity">
                <Input
                  value={action.retrieveSource.entityQualifiedName ?? ""}
                  disabled={readonly}
                  onChange={entityQualifiedName => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, entityQualifiedName } }))}
                />
              </Field>
              <Field label="XPath Constraint">
                <TextArea
                  value={action.retrieveSource.xPathConstraint?.raw ?? ""}
                  disabled={readonly}
                  autosize
                  onChange={raw => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, xPathConstraint: expression(raw, { kind: "boolean" }) } }))}
                />
              </Field>
              <Field label="Range">
                <Select
                  value={action.retrieveSource.range.kind}
                  disabled={readonly}
                  style={{ width: "100%" }}
                  onChange={value => {
                    const kind = String(value);
                    const range = kind === "custom"
                      ? { kind: "custom" as const, officialType: "Microflows$CustomRange" as const, limitExpression: expression("10", { kind: "integer" }), offsetExpression: expression("0", { kind: "integer" }) }
                      : { kind: kind as "all" | "first", officialType: "Microflows$ConstantRange" as const, value: kind as "all" | "first" };
                    patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, range } }));
                  }}
                  optionList={[{ label: "All", value: "all" }, { label: "First", value: "first" }, { label: "Custom", value: "custom" }]}
                />
              </Field>
              <Field label="Sort Items (one per line: Entity.Attribute asc)">
                <TextArea
                  autosize
                  disabled={readonly}
                  value={action.retrieveSource.sortItemList.items.map(item => `${item.attributeQualifiedName} ${item.direction}`).join("\n")}
                  onChange={value => patchObject(updateAction(object, {
                    retrieveSource: {
                      ...action.retrieveSource,
                      sortItemList: {
                        items: value.split("\n").map(line => line.trim()).filter(Boolean).map(line => {
                          const [attributeQualifiedName, direction] = line.split(/\s+/);
                          return { attributeQualifiedName, direction: direction === "desc" ? "desc" as const : "asc" as const };
                        })
                      }
                    }
                  }))}
                />
              </Field>
            </>
          ) : (
            <>
              <Field label="Start Variable">
                <Input
                  value={action.retrieveSource.startVariableName}
                  disabled={readonly}
                  onChange={startVariableName => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, startVariableName } }))}
                />
              </Field>
              <Field label="Association">
                <Input
                  value={action.retrieveSource.associationQualifiedName ?? ""}
                  disabled={readonly}
                  onChange={associationQualifiedName => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, associationQualifiedName } }))}
                />
              </Field>
            </>
          )}
        </>
      ) : null}

      {action.kind === "commit" || action.kind === "delete" || action.kind === "rollback" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>{action.kind}</Title>
          <Field label="Object/List Variable">
            <Input
              value={action.objectOrListVariableName}
              disabled={readonly}
              onChange={objectOrListVariableName => patchObject(updateAction(object, { objectOrListVariableName }))}
            />
          </Field>
          {"withEvents" in action ? (
            <Field label="With Events">
              <Switch checked={action.withEvents} disabled={readonly} onChange={withEvents => patchObject(updateAction(object, { withEvents }))} />
            </Field>
          ) : null}
          {"refreshInClient" in action ? (
            <Field label="Refresh In Client">
              <Switch checked={action.refreshInClient} disabled={readonly} onChange={refreshInClient => patchObject(updateAction(object, { refreshInClient }))} />
            </Field>
          ) : null}
        </>
      ) : null}

      {action.kind === "restCall" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>REST Request</Title>
          <Field label="Method">
            <Select
              value={action.request.method}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={method => patchObject(updateAction(object, { request: { ...action.request, method: String(method) as typeof action.request.method } }))}
              optionList={["GET", "POST", "PUT", "PATCH", "DELETE"].map(value => ({ label: value, value }))}
            />
          </Field>
          <Field label="URL Expression">
            <Input value={action.request.urlExpression.raw} disabled={readonly} onChange={raw => patchObject(updateAction(object, { request: { ...action.request, urlExpression: expression(raw, { kind: "string" }) } }))} />
          </Field>
          <Field label="Timeout Seconds">
            <InputNumber value={action.timeoutSeconds} disabled={readonly} onChange={timeoutSeconds => patchObject(updateAction(object, { timeoutSeconds: Number(timeoutSeconds) }))} />
          </Field>
          <Field label="Response Handling">
            <Select
              value={action.response.handling.kind}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={kind => patchObject(updateAction(object, {
                response: {
                  ...action.response,
                  handling: String(kind) === "ignore"
                    ? { kind: "ignore" }
                    : { kind: String(kind) as "string" | "json", outputVariableName: action.response.handling.kind === "ignore" ? "Response" : action.response.handling.outputVariableName }
                }
              }))}
              optionList={[{ label: "Ignore", value: "ignore" }, { label: "String", value: "string" }, { label: "JSON", value: "json" }]}
            />
          </Field>
        </>
      ) : null}

      {action.kind === "logMessage" ? (
        <>
          <Field label="Level">
            <Select
              value={action.level}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={level => patchObject(updateAction(object, { level: String(level) as typeof action.level }))}
              optionList={["trace", "debug", "info", "warning", "error", "critical"].map(value => ({ label: value, value }))}
            />
          </Field>
          <Field label="Template">
            <TextArea value={action.template.text} autosize disabled={readonly} onChange={text => patchObject(updateAction(object, { template: { ...action.template, text } }))} />
          </Field>
        </>
      ) : null}
    </Space>
  );
}

function ObjectPanel(props: MicroflowPropertyPanelProps) {
  const object = props.selectedObject;
  if (!object) {
    return null;
  }
  const issues = issuesFor(props, object.id, undefined, object.kind === "actionActivity" ? object.action.id : undefined);
  const patch = (next: MicroflowObject) => props.onObjectChange(object.id, { object: next });
  return (
    <>
      <Header
        props={props}
        title={objectTitle(object)}
        subtitle={object.officialType}
        onDelete={() => props.onDeleteObject?.(object.id)}
        onDuplicate={() => props.onDuplicateObject?.(object.id)}
      />
      <div style={{ padding: 14, display: "grid", gap: 12 }}>
        {issues.length > 0 ? <Space wrap>{issues.map(issue => <Tag key={issue.id} color={issue.severity === "error" ? "red" : "orange"}>{issue.code}</Tag>)}</Space> : null}
        <Field label="Kind">
          <Input value={object.kind} disabled />
        </Field>
        <Field label="Caption">
          <Input value={object.caption ?? ""} disabled={props.readonly} onChange={caption => patch({ ...object, caption })} />
        </Field>
        <Field label="Documentation">
          <TextArea value={object.documentation ?? ""} autosize disabled={props.readonly} onChange={documentation => patch({ ...object, documentation })} />
        </Field>
        <Field label="Disabled">
          <Switch checked={Boolean(object.disabled)} disabled={props.readonly} onChange={disabled => patch({ ...object, disabled })} />
        </Field>
        {object.kind === "startEvent" ? (
          <Field label="Trigger">
            <Select
              value={object.trigger.type}
              disabled={props.readonly}
              style={{ width: "100%" }}
              onChange={type => patch({ ...object, trigger: { type: String(type) as typeof object.trigger.type } })}
              optionList={["manual", "pageEvent", "formSubmit", "workflowCall", "apiCall", "scheduled", "system"].map(value => ({ label: value, value }))}
            />
          </Field>
        ) : null}
        {object.kind === "endEvent" ? (
          <Field label="Return Value">
            <Input value={object.returnValue?.raw ?? ""} disabled={props.readonly} onChange={raw => patch({ ...object, returnValue: raw ? expression(raw) : undefined })} />
          </Field>
        ) : null}
        {object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression" ? (
          <Field label="Split Expression">
            <Input value={object.splitCondition.expression.raw} disabled={props.readonly} onChange={raw => patch({ ...object, splitCondition: { ...object.splitCondition, expression: expression(raw, { kind: object.splitCondition.resultType }) } })} />
          </Field>
        ) : null}
        {object.kind === "inheritanceSplit" ? (
          <>
            <Field label="Input Object Variable">
              <Input value={object.inputObjectVariableName} disabled={props.readonly} onChange={inputObjectVariableName => patch({ ...object, inputObjectVariableName })} />
            </Field>
            <Field label="Allowed Specializations">
              <TextArea value={object.entity.allowedSpecializations.join("\n")} autosize disabled={props.readonly} onChange={value => patch({ ...object, entity: { ...object.entity, allowedSpecializations: value.split("\n").map(item => item.trim()).filter(Boolean) } })} />
            </Field>
          </>
        ) : null}
        {object.kind === "loopedActivity" ? (
          <>
            <Field label="Loop Source">
              <Select
                value={object.loopSource.kind}
                disabled={props.readonly}
                style={{ width: "100%" }}
                onChange={kind => patch({
                  ...object,
                  loopSource: String(kind) === "whileCondition"
                    ? { kind: "whileCondition", officialType: "Microflows$WhileLoopCondition", expression: expression("true", { kind: "boolean" }) }
                    : { kind: "iterableList", officialType: "Microflows$IterableList", listVariableName: "Items", iteratorVariableName: "Item", currentIndexVariableName: "$currentIndex" }
                })}
                optionList={[{ label: "Iterable List", value: "iterableList" }, { label: "While Condition", value: "whileCondition" }]}
              />
            </Field>
            {object.loopSource.kind === "iterableList" ? (
              <>
                <Field label="List Variable">
                  <Input value={object.loopSource.listVariableName} disabled={props.readonly} onChange={listVariableName => patch({ ...object, loopSource: { ...object.loopSource, listVariableName } })} />
                </Field>
                <Field label="Iterator Variable">
                  <Input value={object.loopSource.iteratorVariableName} disabled={props.readonly} onChange={iteratorVariableName => patch({ ...object, loopSource: { ...object.loopSource, iteratorVariableName } })} />
                </Field>
              </>
            ) : (
              <Field label="While Expression">
                <Input value={object.loopSource.expression.raw} disabled={props.readonly} onChange={raw => patch({ ...object, loopSource: { ...object.loopSource, expression: expression(raw, { kind: "boolean" }) } })} />
              </Field>
            )}
          </>
        ) : null}
        {object.kind === "actionActivity" ? (
          <ActionActivityFields object={object} readonly={props.readonly} onPatch={payload => props.onObjectChange(object.id, payload)} />
        ) : null}
      </div>
    </>
  );
}

function flowPatch(flow: MicroflowFlow, patch: MicroflowEdgePatch): MicroflowFlow {
  return { ...flow, ...patch } as MicroflowFlow;
}

function FlowPanel(props: MicroflowPropertyPanelProps) {
  const flow = props.selectedFlow;
  if (!flow) {
    return null;
  }
  const issues = issuesFor(props, undefined, flow.id);
  const patch = (next: MicroflowFlow) => props.onFlowChange?.(flow.id, next);
  return (
    <>
      <Header
        props={props}
        title={flow.kind === "sequence" ? flow.editor.label ?? "Sequence Flow" : flow.editor.label ?? "Annotation Flow"}
        subtitle={flow.officialType}
        onDelete={() => props.onDeleteFlow?.(flow.id)}
      />
      <div style={{ padding: 14, display: "grid", gap: 12 }}>
        {issues.length > 0 ? <Space wrap>{issues.map(issue => <Tag key={issue.id} color={issue.severity === "error" ? "red" : "orange"}>{issue.code}</Tag>)}</Space> : null}
        <Field label="Origin Object">
          <Input value={flow.originObjectId} disabled />
        </Field>
        <Field label="Destination Object">
          <Input value={flow.destinationObjectId} disabled />
        </Field>
        <Field label="Origin Connection Index">
          <InputNumber value={flow.originConnectionIndex ?? 0} disabled={props.readonly} onChange={originConnectionIndex => patch(flowPatch(flow, { originConnectionIndex: Number(originConnectionIndex) }))} />
        </Field>
        <Field label="Destination Connection Index">
          <InputNumber value={flow.destinationConnectionIndex ?? 0} disabled={props.readonly} onChange={destinationConnectionIndex => patch(flowPatch(flow, { destinationConnectionIndex: Number(destinationConnectionIndex) }))} />
        </Field>
        <Field label="Line Routing">
          <Select
            value={flow.line.kind}
            disabled={props.readonly}
            style={{ width: "100%" }}
            onChange={kind => patch(flowPatch(flow, { line: { ...flow.line, kind: String(kind) as typeof flow.line.kind } }))}
            optionList={["orthogonal", "polyline", "bezier"].map(value => ({ label: value, value }))}
          />
        </Field>
        {flow.kind === "sequence" ? (
          <>
            <Field label="Editor Edge Kind">
              <Select
                value={flow.editor.edgeKind}
                disabled={props.readonly}
                style={{ width: "100%" }}
                onChange={edgeKind => patch(flowPatch(flow, { editor: { ...flow.editor, edgeKind: String(edgeKind) as MicroflowSequenceFlow["editor"]["edgeKind"] } }))}
                optionList={["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"].map(value => ({ label: value, value }))}
              />
            </Field>
            <Field label="Error Handler">
              <Switch checked={flow.isErrorHandler} disabled={props.readonly} onChange={isErrorHandler => patch(flowPatch(flow, { isErrorHandler, editor: { ...flow.editor, edgeKind: isErrorHandler ? "errorHandler" : flow.editor.edgeKind } }))} />
            </Field>
            <Field label="Case Values">
              <TextArea
                autosize
                disabled={props.readonly}
                value={flow.caseValues.map(item => item.kind === "boolean" ? item.persistedValue : item.kind === "enumeration" ? item.value : item.kind === "inheritance" ? item.entityQualifiedName : item.kind).join("\n")}
                onChange={value => patch(flowPatch(flow, {
                  caseValues: value.split("\n").map(item => item.trim()).filter(Boolean).map(item => item === "true" || item === "false"
                    ? { kind: "boolean" as const, officialType: "Microflows$EnumerationCase" as const, value: item === "true", persistedValue: item as "true" | "false" }
                    : { kind: "enumeration" as const, officialType: "Microflows$EnumerationCase" as const, enumerationQualifiedName: "", value: item })
                }))}
              />
            </Field>
            <Field label="Label">
              <Input value={flow.editor.label ?? ""} disabled={props.readonly} onChange={label => patch(flowPatch(flow, { editor: { ...flow.editor, label } }))} />
            </Field>
          </>
        ) : (
          <>
            <Field label="Show In Export">
              <Switch checked={flow.editor.showInExport} disabled={props.readonly} onChange={showInExport => patch(flowPatch(flow, { editor: { ...flow.editor, showInExport } }))} />
            </Field>
            <Field label="Label">
              <Input value={flow.editor.label ?? ""} disabled={props.readonly} onChange={label => patch(flowPatch(flow, { editor: { ...flow.editor, label } }))} />
            </Field>
            <Field label="Description">
              <TextArea value={flow.editor.description ?? ""} autosize disabled={props.readonly} onChange={description => patch(flowPatch(flow, { editor: { ...flow.editor, description } }))} />
            </Field>
          </>
        )}
      </div>
    </>
  );
}

export function MicroflowPropertyPanel(props: MicroflowPropertyPanelProps) {
  if (!props.selectedObject && !props.selectedFlow) {
    return (
      <div style={{ height: "100%", display: "grid", placeItems: "center", padding: 24 }}>
        <Empty title="未选择对象" description="选择画布对象或连线后编辑 AuthoringSchema 字段。" />
      </div>
    );
  }
  return (
    <div style={{ height: "100%", minHeight: 0, overflow: "auto", background: "var(--semi-color-bg-1, #fff)" }}>
      {props.selectedFlow ? <FlowPanel {...props} /> : <ObjectPanel {...props} />}
    </div>
  );
}
