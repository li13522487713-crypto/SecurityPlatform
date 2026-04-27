import { useMemo, useState } from "react";
import { Button, Input, InputNumber, Select, Space, Switch, Tag, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowAction, MicroflowActionActivity, MicroflowDataType, MicroflowDatabaseRetrieveSource, MicroflowExpression, MicroflowSortItem } from "../../schema";
import { EMPTY_MICROFLOW_METADATA_CATALOG, getAssociationByQualifiedName, getAttributeByQualifiedName, getMicroflowById, getTargetEntityByAssociation, useMetadataStatus, useMicroflowMetadataCatalog, type MicroflowMetadataCatalog } from "../../metadata";
import { buildVariableIndex, getObjectEntityQualifiedName, getVariableNameConflicts, getVariableReferences, resolveVariableReferenceFromIndex } from "../../variables";
import { ErrorHandlingEditor, FieldError, FieldRow, OutputVariableEditor, VariableNameInput } from "../common";
import { ExpressionEditor } from "../expression";
import { AssociationSelector, AttributeSelector, DataTypeSelector, EntitySelector, MicroflowSelector, VariableSelector } from "../selectors";
import type { MicroflowNodePatch, MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { dataTypeFromKey, dataTypeLabel, expression, Field, updateAction } from "../panel-shared";
import { GenericActionFields } from "./generic-action-fields-form";
import {
  isVoidMicroflowReturn,
  updateCallMicroflowParameterMapping,
  updateCallMicroflowReturnBinding,
  updateCallMicroflowTarget,
} from "../utils/call-microflow-config";

const { Text, Title } = Typography;

function RequiredConfigWarning({ visible, children }: { visible: boolean; children: string }) {
  return visible ? <Text type="warning" size="small">{children}</Text> : null;
}

function retrieveOutputDataType(action: Extract<MicroflowAction, { kind: "retrieve" }>, catalog: MicroflowMetadataCatalog): MicroflowDataType {
  if (action.retrieveSource.kind === "database") {
    const itemType: MicroflowDataType = action.retrieveSource.entityQualifiedName
      ? { kind: "object", entityQualifiedName: action.retrieveSource.entityQualifiedName }
      : { kind: "unknown", reason: "Retrieve entity missing" };
    return action.retrieveSource.range.kind === "first" ? itemType : { kind: "list", itemType };
  }
  const association = getAssociationByQualifiedName(catalog, action.retrieveSource.associationQualifiedName ?? undefined);
  const target = getTargetEntityByAssociation(catalog, action.retrieveSource.associationQualifiedName ?? undefined);
  const itemType: MicroflowDataType = target
    ? { kind: "object", entityQualifiedName: target.qualifiedName }
    : { kind: "unknown", reason: "Retrieve association target missing" };
  return association?.multiplicity === "oneToMany" || association?.multiplicity === "manyToMany" ? { kind: "list", itemType } : itemType;
}

export function ActionActivityForm({
  schema,
  object,
  issues,
  readonly,
  onPatch
}: {
  schema: MicroflowPropertyPanelProps["schema"];
  object: MicroflowActionActivity;
  issues: ReturnType<typeof getIssuesForObject>;
  readonly?: boolean;
  onPatch: (patch: MicroflowNodePatch) => void;
}) {
  const action = object.action;
  const catalog = useMicroflowMetadataCatalog();
  const { version: metadataVersion } = useMetadataStatus();
  const effectiveCatalog = catalog ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const variableIndex = useMemo(() => buildVariableIndex(schema, effectiveCatalog), [schema, effectiveCatalog, metadataVersion]);
  const variableEntity = (variableName?: string) => variableName
    ? getObjectEntityQualifiedName(resolveVariableReferenceFromIndex(schema, variableIndex, { objectId: object.id }, variableName)?.dataType)
    : undefined;
  const associationSource = action.kind === "retrieve" && action.retrieveSource.kind === "association"
    ? variableEntity(action.retrieveSource.startVariableName) ?? getAssociationByQualifiedName(effectiveCatalog, action.retrieveSource.associationQualifiedName ?? undefined)?.sourceEntityQualifiedName
    : undefined;
  const [associationStartEntity, setAssociationStartEntity] = useState<string | undefined>(associationSource);
  const firstMemberEntity = action.kind === "changeMembers" && action.memberChanges[0]?.memberQualifiedName
    ? action.memberChanges[0].memberQualifiedName.split(".").slice(0, -1).join(".")
    : undefined;
  const inferredMemberEntity = action.kind === "changeMembers" ? variableEntity(action.changeVariableName) : undefined;
  const [memberEntity, setMemberEntity] = useState<string | undefined>(inferredMemberEntity ?? firstMemberEntity);
  const selectedMicroflow = action.kind === "callMicroflow" ? getMicroflowById(effectiveCatalog, action.targetMicroflowId) : undefined;
  const restFormBody = action.kind === "restCall" && action.request.body.kind === "form" ? action.request.body : undefined;
  const createVariableConflicts = action.kind === "createVariable" ? getVariableNameConflicts(schema, action.variableName, action.id) : [];
  const createVariableReferences = action.kind === "createVariable" ? getVariableReferences(schema, action.id).filter(reference => reference.objectId !== object.id || reference.fieldPath !== "action.initialValue") : [];
  const patchObject = (next: MicroflowActionActivity) => onPatch({ object: next });
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Field label="Caption">
        <Input
          value={object.caption}
          disabled={readonly}
          onChange={caption => patchObject({ ...object, caption: caption.trim() ? caption : object.caption || action.kind })}
        />
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
      <FieldRow label="Error Handling" fieldPath="action.errorHandlingType" issues={getIssuesForField(issues, "action.errorHandlingType")}>
        <ErrorHandlingEditor
          value={action.errorHandlingType}
          readonly={readonly || action.kind === "rollback"}
          actionKind={action.kind}
          fieldPath="action.errorHandlingType"
          issues={getIssuesForField(issues, "action.errorHandlingType")}
          supportedTypes={action.kind === "logMessage" ? ["rollback"] : action.kind === "callMicroflow" ? ["rollback", "customWithRollback", "customWithoutRollback", "continue"] : ["rollback", "customWithRollback", "customWithoutRollback"]}
          onChange={errorHandlingType => patchObject(updateAction(object, { errorHandlingType }))}
        />
      </FieldRow>

      {action.kind === "retrieve" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Retrieve Source</Title>
          <FieldRow label="Output Variable" fieldPath="action.outputVariableName" required issues={getIssuesForField(issues, "action.outputVariableName")}>
            <OutputVariableEditor
              value={action.outputVariableName}
              schema={schema}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.outputVariableName"
              suggestedBaseName="RetrievedObjects"
              dataType={retrieveOutputDataType(action, effectiveCatalog)}
              readonly={readonly}
              required
              issues={getIssuesForField(issues, "action.outputVariableName")}
              onChange={outputVariableName => patchObject(updateAction(object, { outputVariableName: outputVariableName ?? "" }))}
            />
          </FieldRow>
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
                <EntitySelector
                  value={action.retrieveSource.entityQualifiedName ?? ""}
                  disabled={readonly}
                  onlyPersistable
                  onChange={entityQualifiedName => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, entityQualifiedName: entityQualifiedName ?? null } }))}
                />
                <FieldError issues={getIssuesForField(issues, "action.retrieveSource.entityQualifiedName")} />
                <RequiredConfigWarning visible={!String(action.retrieveSource.entityQualifiedName ?? "").trim()}>Entity 未配置；不会自动填入示例 Domain Model。</RequiredConfigWarning>
              </Field>
              <Field label="XPath Constraint">
                <ExpressionEditor
                  value={action.retrieveSource.xPathConstraint ?? undefined}
                  schema={schema}
                  metadata={effectiveCatalog}
                  variableIndex={variableIndex}
                  objectId={object.id}
                  actionId={action.id}
                  fieldPath="action.retrieveSource.xPathConstraint"
                  expectedType={{ kind: "boolean" }}
                  readonly={readonly}
                  onChange={xPathConstraint => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, xPathConstraint } }))}
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
              {action.retrieveSource.range.kind === "custom" ? (
                <>
                  <FieldRow label="Limit Expression" fieldPath="action.retrieveSource.range.limitExpression" required issues={getIssuesForField(issues, "action.retrieveSource.range.limitExpression")}>
                    <ExpressionEditor
                      value={action.retrieveSource.range.limitExpression}
                      schema={schema}
                      metadata={effectiveCatalog}
                      variableIndex={variableIndex}
                      objectId={object.id}
                      actionId={action.id}
                      fieldPath="action.retrieveSource.range.limitExpression"
                      expectedType={{ kind: "integer" }}
                      required
                      readonly={readonly}
                      onChange={limitExpression => {
                        const source = action.retrieveSource;
                        if (source.kind !== "database" || source.range.kind !== "custom") {
                          return;
                        }
                        patchObject(updateAction(object, { retrieveSource: { ...source, range: { ...source.range, limitExpression } } }));
                      }}
                    />
                  </FieldRow>
                  <FieldRow label="Offset Expression" fieldPath="action.retrieveSource.range.offsetExpression" issues={getIssuesForField(issues, "action.retrieveSource.range.offsetExpression")}>
                    <ExpressionEditor
                      value={action.retrieveSource.range.offsetExpression}
                      schema={schema}
                      metadata={effectiveCatalog}
                      variableIndex={variableIndex}
                      objectId={object.id}
                      actionId={action.id}
                      fieldPath="action.retrieveSource.range.offsetExpression"
                      expectedType={{ kind: "integer" }}
                      readonly={readonly}
                      onChange={offsetExpression => {
                        const source = action.retrieveSource;
                        if (source.kind !== "database" || source.range.kind !== "custom") {
                          return;
                        }
                        patchObject(updateAction(object, { retrieveSource: { ...source, range: { ...source.range, offsetExpression } } }));
                      }}
                    />
                  </FieldRow>
                </>
              ) : null}
              <Field label="Sort Items (one per line: Entity.Attribute asc)">
                <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                  {(action.retrieveSource as MicroflowDatabaseRetrieveSource).sortItemList.items.map((item, index) => (
                    <div key={`${item.attributeQualifiedName}-${index}`} style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 92px auto", gap: 6, width: "100%" }}>
                      <AttributeSelector
                        entityQualifiedName={action.retrieveSource.kind === "database" ? action.retrieveSource.entityQualifiedName ?? undefined : undefined}
                        value={item.attributeQualifiedName}
                        disabled={readonly}
                        onChange={attributeQualifiedName => patchObject(updateAction(object, {
                          retrieveSource: {
                            ...action.retrieveSource,
                            sortItemList: {
                              items: (action.retrieveSource as MicroflowDatabaseRetrieveSource).sortItemList.items.map((row: MicroflowSortItem, rowIndex: number) => rowIndex === index ? { ...row, attributeQualifiedName: attributeQualifiedName ?? "" } : row),
                            },
                          },
                        }))}
                      />
                      <Select
                        value={item.direction}
                        disabled={readonly}
                        optionList={[{ label: "asc", value: "asc" }, { label: "desc", value: "desc" }]}
                        onChange={direction => patchObject(updateAction(object, {
                          retrieveSource: {
                            ...action.retrieveSource,
                            sortItemList: {
                              items: (action.retrieveSource as MicroflowDatabaseRetrieveSource).sortItemList.items.map((row: MicroflowSortItem, rowIndex: number) => rowIndex === index ? { ...row, direction: String(direction) === "desc" ? "desc" : "asc" } : row),
                            },
                          },
                        }))}
                      />
                      <Button disabled={readonly} type="danger" theme="borderless" onClick={() => patchObject(updateAction(object, {
                        retrieveSource: {
                          ...action.retrieveSource,
                          sortItemList: { items: (action.retrieveSource as MicroflowDatabaseRetrieveSource).sortItemList.items.filter((_: unknown, rowIndex: number) => rowIndex !== index) },
                        },
                      }))}>Delete</Button>
                    </div>
                  ))}
                  <Button disabled={readonly || !action.retrieveSource.entityQualifiedName} onClick={() => patchObject(updateAction(object, {
                    retrieveSource: {
                      ...action.retrieveSource,
                      sortItemList: { items: [...(action.retrieveSource as MicroflowDatabaseRetrieveSource).sortItemList.items, { attributeQualifiedName: "", direction: "asc" as const }] },
                    },
                  }))}>Add sort item</Button>
                </Space>
              </Field>
            </>
          ) : (
            <>
              <Field label="Start Variable">
                <VariableSelector
                  schema={schema}
                  objectId={object.id}
                  fieldPath="action.retrieveSource.startVariableName"
                  allowedTypeKinds={["object"]}
                  value={action.retrieveSource.startVariableName}
                  disabled={readonly}
                  onChange={startVariableName => {
                    const nextStartVariableName = startVariableName ?? "";
                    setAssociationStartEntity(variableEntity(nextStartVariableName));
                    patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, startVariableName: nextStartVariableName, associationQualifiedName: null } }));
                  }}
                />
                <FieldError issues={getIssuesForField(issues, "action.retrieveSource.startVariableName")} />
              </Field>
              <Field label="Association Start Entity">
                <EntitySelector
                  value={associationStartEntity}
                  disabled={readonly}
                  onChange={entityQualifiedName => {
                    setAssociationStartEntity(entityQualifiedName);
                    patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, associationQualifiedName: null } }));
                  }}
                />
              </Field>
              <Field label="Association">
                <AssociationSelector
                  startEntityQualifiedName={associationStartEntity}
                  value={action.retrieveSource.associationQualifiedName ?? ""}
                  disabled={readonly}
                  onChange={associationQualifiedName => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, associationQualifiedName: associationQualifiedName ?? null } }))}
                />
                <FieldError issues={getIssuesForField(issues, "action.retrieveSource.associationQualifiedName")} />
              </Field>
            </>
          )}
        </>
      ) : null}

      {action.kind === "commit" || action.kind === "delete" || action.kind === "rollback" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>{action.kind}</Title>
          <Field label="Object/List Variable">
            <VariableSelector
              schema={schema}
              objectId={object.id}
              fieldPath="action.objectOrListVariableName"
              allowedTypeKinds={["object", "list"]}
              value={action.objectOrListVariableName}
              disabled={readonly}
              onChange={objectOrListVariableName => patchObject(updateAction(object, { objectOrListVariableName: objectOrListVariableName ?? "" }))}
            />
            <FieldError issues={getIssuesForField(issues, "action.objectOrListVariableName")} />
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
          {action.kind === "delete" ? (
            <FieldRow label="Delete Behavior" fieldPath="action.deleteBehavior" tooltip="Deleting objects may invalidate downstream references.">
              <Select
                value={action.deleteBehavior}
                disabled={readonly}
                style={{ width: "100%" }}
                optionList={[{ label: "deleteOnly", value: "deleteOnly" }, { label: "deleteAndRefreshClient", value: "deleteAndRefreshClient" }]}
                onChange={deleteBehavior => patchObject(updateAction(object, { deleteBehavior: String(deleteBehavior) as typeof action.deleteBehavior }))}
              />
            </FieldRow>
          ) : null}
          {action.kind === "rollback" ? (
            <Tag color="orange">Rollback Object only reverts the selected object/list, not the whole error-handling transaction.</Tag>
          ) : null}
        </>
      ) : null}

      {action.kind === "createObject" || action.kind === "changeMembers" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>{action.kind === "createObject" ? "Create Object" : "Change Members"}</Title>
          {action.kind === "createObject" ? (
            <>
              <Field label="Entity">
                <EntitySelector value={action.entityQualifiedName} disabled={readonly} onChange={entityQualifiedName => patchObject(updateAction(object, { entityQualifiedName: entityQualifiedName ?? "" }))} />
                <FieldError issues={getIssuesForField(issues, "action.entityQualifiedName")} />
                <RequiredConfigWarning visible={!action.entityQualifiedName.trim()}>Entity 未配置；真实 Domain Model 绑定留到后续阶段。</RequiredConfigWarning>
              </Field>
              <FieldRow label="Output Variable" fieldPath="action.outputVariableName" required issues={getIssuesForField(issues, "action.outputVariableName")}>
                <OutputVariableEditor
                  value={action.outputVariableName}
                  schema={schema}
                  objectId={object.id}
                  actionId={action.id}
                  fieldPath="action.outputVariableName"
                  suggestedBaseName="NewObject"
                  dataType={action.entityQualifiedName ? { kind: "object", entityQualifiedName: action.entityQualifiedName } : { kind: "unknown", reason: "CreateObject entity missing" }}
                  readonly={readonly}
                  required
                  issues={getIssuesForField(issues, "action.outputVariableName")}
                  onChange={outputVariableName => patchObject(updateAction(object, { outputVariableName: outputVariableName ?? "" }))}
                />
              </FieldRow>
            </>
          ) : (
            <Field label="Change Variable">
              <VariableSelector
                schema={schema}
                objectId={object.id}
                fieldPath="action.changeVariableName"
                allowedTypeKinds={["object"]}
                value={action.changeVariableName}
                disabled={readonly}
                onChange={changeVariableName => {
                  const nextChangeVariableName = changeVariableName ?? "";
                  setMemberEntity(variableEntity(nextChangeVariableName));
                  patchObject(updateAction(object, { changeVariableName: nextChangeVariableName }));
                }}
              />
              <FieldError issues={getIssuesForField(issues, "action.changeVariableName")} />
            </Field>
          )}
          <Field label="Member Changes (member|assignment|expression)">
            <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
              <FieldError issues={getIssuesForField(issues, "action.memberChanges")} />
              {action.kind === "changeMembers" ? (
                <EntitySelector value={memberEntity} disabled={readonly} onChange={setMemberEntity} placeholder="Select changed object entity" />
              ) : null}
              {action.memberChanges.map((change, index) => (
                <div key={change.id} style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 90px minmax(0, 1fr) auto", gap: 6, width: "100%" }}>
                  <AttributeSelector
                    entityQualifiedName={action.kind === "createObject" ? action.entityQualifiedName : memberEntity}
                    value={change.memberQualifiedName}
                    disabled={readonly}
                    writableOnly
                    onChange={memberQualifiedName => patchObject(updateAction(object, {
                      memberChanges: action.memberChanges.map((row, rowIndex) => rowIndex === index ? { ...row, memberQualifiedName: memberQualifiedName ?? "" } : row),
                    }))}
                  />
                  <Select
                    value={change.assignmentKind}
                    disabled={readonly}
                    optionList={["set", "add", "remove", "clear"].map(value => ({ label: value, value }))}
                    onChange={assignmentKind => patchObject(updateAction(object, {
                      memberChanges: action.memberChanges.map((row, rowIndex) => rowIndex === index ? { ...row, assignmentKind: String(assignmentKind) as typeof row.assignmentKind } : row),
                    }))}
                  />
                  <ExpressionEditor
                    value={change.valueExpression ?? expression("")}
                    schema={schema}
                    metadata={effectiveCatalog}
                    variableIndex={variableIndex}
                    objectId={object.id}
                    actionId={action.id}
                    fieldPath={`action.memberChanges.${index}.valueExpression`}
                    expectedType={getAttributeByQualifiedName(effectiveCatalog, change.memberQualifiedName)?.type}
                    required={change.assignmentKind !== "clear"}
                    readonly={readonly || change.assignmentKind === "clear"}
                    placeholder="Expression"
                    onChange={valueExpression => patchObject(updateAction(object, {
                      memberChanges: action.memberChanges.map((row, rowIndex) => rowIndex === index ? { ...row, valueExpression } : row),
                    }))}
                  />
                  <Button disabled={readonly} type="danger" theme="borderless" onClick={() => patchObject(updateAction(object, {
                    memberChanges: action.memberChanges.filter((_, rowIndex) => rowIndex !== index),
                  }))}>Delete</Button>
                </div>
              ))}
              <Button disabled={readonly || (action.kind === "createObject" ? !action.entityQualifiedName : !memberEntity)} onClick={() => patchObject(updateAction(object, {
                memberChanges: [...action.memberChanges, {
                  id: `member-change-${Date.now()}`,
                  memberQualifiedName: "",
                  memberKind: "attribute",
                  assignmentKind: "set",
                  valueExpression: expression(""),
                }],
              }))}>Add member change</Button>
            </Space>
          </Field>
          <Field label="Commit Enabled">
            <Switch checked={action.commit.enabled} disabled={readonly} onChange={enabled => patchObject(updateAction(object, { commit: { ...action.commit, enabled } }))} />
          </Field>
          <Field label="With Events">
            <Switch checked={action.commit.withEvents} disabled={readonly} onChange={withEvents => patchObject(updateAction(object, { commit: { ...action.commit, withEvents } }))} />
          </Field>
          <Field label="Refresh In Client">
            <Switch checked={action.commit.refreshInClient} disabled={readonly} onChange={refreshInClient => patchObject(updateAction(object, { commit: { ...action.commit, refreshInClient } }))} />
          </Field>
          {action.kind === "changeMembers" ? (
            <Field label="Validate Object">
              <Switch checked={action.validateObject} disabled={readonly} onChange={validateObject => patchObject(updateAction(object, { validateObject }))} />
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
            <ExpressionEditor
              value={action.request.urlExpression}
              schema={schema}
              metadata={effectiveCatalog}
              variableIndex={variableIndex}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.request.urlExpression"
              expectedType={{ kind: "string" }}
              required
              readonly={readonly}
              onChange={urlExpression => patchObject(updateAction(object, { request: { ...action.request, urlExpression } }))}
            />
            <RequiredConfigWarning visible={!action.request.urlExpression.raw.trim()}>REST URL 为空；保存为待配置状态，不会写入 demo URL。</RequiredConfigWarning>
          </Field>
          <Field label="Timeout Seconds" issues={getIssuesForField(issues, "action.timeoutSeconds")}>
            <InputNumber value={action.timeoutSeconds} disabled={readonly} onChange={timeoutSeconds => patchObject(updateAction(object, { timeoutSeconds: Number(timeoutSeconds) }))} />
          </Field>
          <Field label="Headers">
            <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
              {action.request.headers.map((header, index) => (
                <div key={`${header.key}-${index}`} style={{ display: "grid", gridTemplateColumns: "120px minmax(0, 1fr)", gap: 6, width: "100%" }}>
                  <Input value={header.key} disabled={readonly} placeholder="Header" onChange={key => patchObject(updateAction(object, { request: { ...action.request, headers: action.request.headers.map((row, rowIndex) => rowIndex === index ? { ...row, key } : row) } }))} />
                  <ExpressionEditor
                    value={header.valueExpression}
                    schema={schema}
                    metadata={effectiveCatalog}
                    variableIndex={variableIndex}
                    objectId={object.id}
                    actionId={action.id}
                    fieldPath={`action.request.headers.${index}.valueExpression`}
                    expectedType={{ kind: "string" }}
                    readonly={readonly}
                    onChange={valueExpression => patchObject(updateAction(object, { request: { ...action.request, headers: action.request.headers.map((row, rowIndex) => rowIndex === index ? { ...row, valueExpression } : row) } }))}
                  />
                </div>
              ))}
              <Button
                disabled={readonly}
                onClick={() => patchObject(updateAction(object, {
                  request: {
                    ...action.request,
                    headers: [...action.request.headers, { id: `hdr-${Date.now()}`, key: "", valueExpression: expression("", { kind: "string" }) }],
                  },
                }))}
              >Add header</Button>
            </Space>
          </Field>
          <Field label="Query Parameters">
            <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
              {action.request.queryParameters.map((parameter, index) => (
                <div key={`${parameter.key}-${index}`} style={{ display: "grid", gridTemplateColumns: "120px minmax(0, 1fr)", gap: 6, width: "100%" }}>
                  <Input value={parameter.key} disabled={readonly} placeholder="Query" onChange={key => patchObject(updateAction(object, { request: { ...action.request, queryParameters: action.request.queryParameters.map((row, rowIndex) => rowIndex === index ? { ...row, key } : row) } }))} />
                  <ExpressionEditor
                    value={parameter.valueExpression}
                    schema={schema}
                    metadata={effectiveCatalog}
                    variableIndex={variableIndex}
                    objectId={object.id}
                    actionId={action.id}
                    fieldPath={`action.request.queryParameters.${index}.valueExpression`}
                    expectedType={{ kind: "string" }}
                    readonly={readonly}
                    onChange={valueExpression => patchObject(updateAction(object, { request: { ...action.request, queryParameters: action.request.queryParameters.map((row, rowIndex) => rowIndex === index ? { ...row, valueExpression } : row) } }))}
                  />
                </div>
              ))}
              <Button
                disabled={readonly}
                onClick={() => patchObject(updateAction(object, {
                  request: {
                    ...action.request,
                    queryParameters: [...action.request.queryParameters, { id: `q-${Date.now()}`, key: "", valueExpression: expression("", { kind: "string" }) }],
                  },
                }))}
              >Add query</Button>
            </Space>
          </Field>
          <Field label="Body Type">
            <Select
              value={action.request.body.kind}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={kind => {
                const bodyKind = String(kind);
                const body = bodyKind === "json" || bodyKind === "text"
                  ? { kind: bodyKind as "json" | "text", expression: expression("", { kind: "string" }) }
                  : bodyKind === "form"
                    ? { kind: "form" as const, fields: [] }
                    : bodyKind === "mapping"
                      ? { kind: "mapping" as const, exportMappingQualifiedName: "" }
                      : { kind: "none" as const };
                patchObject(updateAction(object, { request: { ...action.request, body } }));
              }}
              optionList={["none", "json", "text", "form", "mapping"].map(value => ({ label: value, value }))}
            />
          </Field>
          {action.request.body.kind === "json" || action.request.body.kind === "text" ? (
            <Field label="Body Content">
              <ExpressionEditor
                value={action.request.body.expression}
                schema={schema}
                metadata={effectiveCatalog}
                variableIndex={variableIndex}
                objectId={object.id}
                actionId={action.id}
                fieldPath="action.request.body.expression"
                expectedType={action.request.body.kind === "json" ? { kind: "json" } : { kind: "string" }}
                readonly={readonly}
                mode="multiline"
                onChange={nextExpression => patchObject(updateAction(object, { request: { ...action.request, body: { ...action.request.body, expression: nextExpression } } }))}
              />
            </Field>
          ) : null}
          {restFormBody ? (
            <Field label="Form Fields">
              <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                {restFormBody.fields.map((field, index) => (
                  <div key={field.id} style={{ display: "grid", gridTemplateColumns: "120px minmax(0, 1fr) auto", gap: 6, width: "100%" }}>
                    <Input value={field.key} disabled={readonly} placeholder="Field" onChange={key => patchObject(updateAction(object, { request: { ...action.request, body: { ...restFormBody, fields: restFormBody.fields.map((row, rowIndex) => rowIndex === index ? { ...row, key } : row) } } }))} />
                    <ExpressionEditor
                      value={field.valueExpression}
                      schema={schema}
                      metadata={effectiveCatalog}
                      variableIndex={variableIndex}
                      objectId={object.id}
                      actionId={action.id}
                      fieldPath={`action.request.body.fields.${index}.valueExpression`}
                      expectedType={{ kind: "string" }}
                      readonly={readonly}
                      onChange={valueExpression => patchObject(updateAction(object, { request: { ...action.request, body: { ...restFormBody, fields: restFormBody.fields.map((row, rowIndex) => rowIndex === index ? { ...row, valueExpression } : row) } } }))}
                    />
                    <Button disabled={readonly} type="danger" theme="borderless" onClick={() => patchObject(updateAction(object, { request: { ...action.request, body: { ...restFormBody, fields: restFormBody.fields.filter((_, rowIndex) => rowIndex !== index) } } }))}>Delete</Button>
                  </div>
                ))}
                <Button disabled={readonly} onClick={() => patchObject(updateAction(object, { request: { ...action.request, body: { ...restFormBody, fields: [...restFormBody.fields, { id: `form-${Date.now()}`, key: "", valueExpression: expression("", { kind: "string" }) }] } } }))}>Add form field</Button>
              </Space>
            </Field>
          ) : null}
          {action.request.body.kind === "mapping" ? (
            <FieldRow label="Export Mapping" fieldPath="action.request.body.exportMappingQualifiedName">
              <Input
                value={action.request.body.exportMappingQualifiedName}
                disabled={readonly}
                placeholder="Module.ExportMapping"
                onChange={exportMappingQualifiedName => patchObject(updateAction(object, { request: { ...action.request, body: { ...action.request.body, exportMappingQualifiedName } } }))}
              />
            </FieldRow>
          ) : null}
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
          {action.response.handling.kind !== "ignore" ? (
            <FieldRow label="Response Output Variable" fieldPath="action.response.handling.outputVariableName" required issues={getIssuesForField(issues, "action.response.handling.outputVariableName")}>
              <OutputVariableEditor
                value={action.response.handling.outputVariableName}
                schema={schema}
                objectId={object.id}
                actionId={action.id}
                fieldPath="action.response.handling.outputVariableName"
                suggestedBaseName="RestResponse"
                dataType={action.response.handling.kind === "string" ? { kind: "string" } : action.response.handling.kind === "json" ? { kind: "json" } : { kind: "unknown", reason: "REST import mapping" }}
                readonly={readonly}
                required
                issues={getIssuesForField(issues, "action.response.handling.outputVariableName")}
                onChange={outputVariableName => patchObject(updateAction(object, { response: { ...action.response, handling: { ...action.response.handling, outputVariableName: outputVariableName ?? "" } } }))}
              />
            </FieldRow>
          ) : null}
          <FieldRow label="Status Code Variable" fieldPath="action.response.statusCodeVariableName" issues={getIssuesForField(issues, "action.response.statusCodeVariableName")}>
            <VariableNameInput
              value={action.response.statusCodeVariableName}
              schema={schema}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.response.statusCodeVariableName"
              suggestedBaseName="StatusCode"
              dataType={{ kind: "integer" }}
              readonly={readonly}
              issues={getIssuesForField(issues, "action.response.statusCodeVariableName")}
              onChange={statusCodeVariableName => patchObject(updateAction(object, { response: { ...action.response, statusCodeVariableName } }))}
            />
          </FieldRow>
          <FieldRow label="Headers Variable" fieldPath="action.response.headersVariableName" issues={getIssuesForField(issues, "action.response.headersVariableName")}>
            <VariableNameInput
              value={action.response.headersVariableName}
              schema={schema}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.response.headersVariableName"
              suggestedBaseName="ResponseHeaders"
              dataType={{ kind: "json" }}
              readonly={readonly}
              issues={getIssuesForField(issues, "action.response.headersVariableName")}
              onChange={headersVariableName => patchObject(updateAction(object, { response: { ...action.response, headersVariableName } }))}
            />
          </FieldRow>
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
            <ExpressionEditor
              value={action.template.text}
              schema={schema}
              metadata={effectiveCatalog}
              variableIndex={variableIndex}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.template.text"
              expectedType={{ kind: "string" }}
              readonly={readonly}
              mode="multiline"
              onChange={next => patchObject(updateAction(object, { template: { ...action.template, text: next.raw } }))}
            />
          </Field>
          <FieldRow label="Arguments" fieldPath="action.template.arguments">
            <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
              {action.template.arguments.map((argument, index) => (
                <div key={`${index}-${argument.raw}`} style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) auto", gap: 6, width: "100%" }}>
                  <ExpressionEditor
                    value={argument}
                    schema={schema}
                    metadata={effectiveCatalog}
                    variableIndex={variableIndex}
                    objectId={object.id}
                    actionId={action.id}
                    fieldPath={`action.template.arguments.${index}`}
                    readonly={readonly}
                    placeholder="Argument expression"
                    onChange={nextArgument => patchObject(updateAction(object, { template: { ...action.template, arguments: action.template.arguments.map((row, rowIndex) => rowIndex === index ? nextArgument : row) } }))}
                  />
                  <Button disabled={readonly} type="danger" theme="borderless" onClick={() => patchObject(updateAction(object, { template: { ...action.template, arguments: action.template.arguments.filter((_, rowIndex) => rowIndex !== index) } }))}>Delete</Button>
                </div>
              ))}
              <Button disabled={readonly} onClick={() => patchObject(updateAction(object, { template: { ...action.template, arguments: [...action.template.arguments, expression("")] } }))}>Add argument</Button>
            </Space>
          </FieldRow>
          <FieldRow label="Log Node Name" fieldPath="action.logNodeName">
            <Input value={action.logNodeName} disabled={readonly} onChange={logNodeName => patchObject(updateAction(object, { logNodeName }))} />
          </FieldRow>
          <Field label="Include Context Variables">
            <Switch checked={action.includeContextVariables} disabled={readonly} onChange={includeContextVariables => patchObject(updateAction(object, { includeContextVariables }))} />
          </Field>
          <Field label="Include TraceId">
            <Switch checked={action.includeTraceId} disabled={readonly} onChange={includeTraceId => patchObject(updateAction(object, { includeTraceId }))} />
          </Field>
        </>
      ) : null}

      {action.kind === "callMicroflow" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Call Microflow</Title>
          <Field label="Target Microflow">
            <MicroflowSelector
              value={action.targetMicroflowId}
              currentMicroflowId={schema.id}
              disabled={readonly}
              onChange={targetMicroflowId => {
                const target = getMicroflowById(effectiveCatalog, targetMicroflowId);
                patchObject(updateAction(object, updateCallMicroflowTarget(action, target)));
              }}
            />
            <FieldError issues={getIssuesForField(issues, "action.targetMicroflowId")} />
            <RequiredConfigWarning visible={!action.targetMicroflowId.trim()}>Target Microflow 未配置；请选择真实 metadata API 返回的微流。</RequiredConfigWarning>
          </Field>
          {selectedMicroflow ? (
            <Field label="Target Signature">
              <TextArea
                disabled
                autosize
                value={`${selectedMicroflow.qualifiedName}\n${selectedMicroflow.parameters.map(parameter => `${parameter.name}: ${dataTypeLabel(parameter.type)}`).join("\n")}\nreturn: ${dataTypeLabel(selectedMicroflow.returnType)}`}
              />
            </Field>
          ) : null}
          <FieldRow label="Parameter Mappings" fieldPath="action.parameterMappings" tooltip="Rows are generated from target microflow metadata.">
            <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
              {action.parameterMappings.map((mapping, index) => (
                <div key={mapping.parameterName} style={{ display: "grid", gap: 6, width: "100%" }}>
                  <Tag color="blue">{mapping.parameterName}: {dataTypeLabel(mapping.parameterType)}</Tag>
                  <div style={{ display: "grid", gridTemplateColumns: "minmax(0, 180px) minmax(0, 1fr)", gap: 8, width: "100%" }}>
                    <VariableSelector
                      schema={schema}
                      objectId={object.id}
                      fieldPath={`action.parameterMappings.${index}.sourceVariableName`}
                      allowedTypeKinds={mapping.parameterType ? [mapping.parameterType.kind] : undefined}
                      value={mapping.sourceVariableName}
                      disabled={readonly}
                      placeholder="Source variable"
                      scopeMode="index"
                      onChange={sourceVariableName => patchObject(updateAction(object, updateCallMicroflowParameterMapping(action, index, {
                        sourceVariableName,
                        argumentExpression: expression(sourceVariableName ?? "", mapping.parameterType),
                      })))}
                    />
                    <ExpressionEditor
                      value={mapping.argumentExpression}
                      schema={schema}
                      metadata={effectiveCatalog}
                      variableIndex={variableIndex}
                      objectId={object.id}
                      actionId={action.id}
                      fieldPath={`action.parameterMappings.${index}.argumentExpression`}
                      expectedType={mapping.parameterType}
                      required
                      readonly={readonly}
                      onChange={argumentExpression => patchObject(updateAction(object, updateCallMicroflowParameterMapping(action, index, {
                        argumentExpression,
                        sourceVariableName: argumentExpression.raw.trim() === mapping.sourceVariableName ? mapping.sourceVariableName : undefined,
                      })))}
                    />
                  </div>
                  {selectedMicroflow?.parameters.find(parameter => parameter.name === mapping.parameterName)?.required && !mapping.argumentExpression.raw.trim() ? (
                    <Text type="warning" size="small">Required parameter has no mapping.</Text>
                  ) : null}
                  {!selectedMicroflow?.parameters.some(parameter => parameter.name === mapping.parameterName) ? (
                    <Text type="warning" size="small">Mapping is stale because the target parameter no longer exists.</Text>
                  ) : null}
                </div>
              ))}
              {action.parameterMappings.length === 0 ? <Tag color="grey">No parameters</Tag> : null}
            </Space>
          </FieldRow>
          <Field label="Store Result">
            <Switch checked={action.returnValue.storeResult} disabled={readonly || isVoidMicroflowReturn(selectedMicroflow?.returnType)} onChange={storeResult => patchObject(updateAction(object, { returnValue: { ...action.returnValue, storeResult, outputVariableName: storeResult ? action.returnValue.outputVariableName : undefined } }))} />
            {isVoidMicroflowReturn(selectedMicroflow?.returnType) ? <Text type="tertiary" size="small">Target microflow has no return value.</Text> : null}
          </Field>
          <Field label="Existing Return Variable">
            <VariableSelector
              schema={schema}
              objectId={object.id}
              fieldPath="action.returnValue.outputVariableName"
              allowedTypeKinds={selectedMicroflow?.returnType ? [selectedMicroflow.returnType.kind] : undefined}
              value={action.returnValue.outputVariableName}
              disabled={readonly || isVoidMicroflowReturn(selectedMicroflow?.returnType)}
              includeSystem={false}
              includeReadonly={false}
              scopeMode="index"
              onChange={outputVariableName => patchObject(updateAction(object, updateCallMicroflowReturnBinding(action, outputVariableName)))}
            />
          </Field>
          <FieldRow label="Return Variable Name" fieldPath="action.returnValue.outputVariableName" required={action.returnValue.storeResult} issues={getIssuesForField(issues, "action.returnValue.outputVariableName")}>
            <OutputVariableEditor
              value={action.returnValue.outputVariableName}
              schema={schema}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.returnValue.outputVariableName"
              suggestedBaseName="MicroflowResult"
              dataType={selectedMicroflow?.returnType ?? action.returnValue.dataType}
              readonly={readonly || !action.returnValue.storeResult || isVoidMicroflowReturn(selectedMicroflow?.returnType)}
              required={action.returnValue.storeResult}
              issues={getIssuesForField(issues, "action.returnValue.outputVariableName")}
              onChange={outputVariableName => patchObject(updateAction(object, { returnValue: { ...action.returnValue, outputVariableName } }))}
            />
          </FieldRow>
          <Field label="Call Mode">
            <Select
              value={action.callMode}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={callMode => patchObject(updateAction(object, { callMode: String(callMode) as typeof action.callMode }))}
              optionList={[{ label: "sync", value: "sync" }, { label: "asyncReserved", value: "asyncReserved" }]}
            />
          </Field>
        </>
      ) : null}

      {action.kind === "createVariable" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Create Variable</Title>
          <FieldRow label="Variable Name" fieldPath="action.variableName" required issues={getIssuesForField(issues, "action.variableName")}>
            <OutputVariableEditor
              value={action.variableName}
              schema={schema}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.variableName"
              suggestedBaseName="NewVariable"
              dataType={action.dataType}
              readonly={readonly}
              required
              issues={getIssuesForField(issues, "action.variableName")}
              onChange={variableName => patchObject(updateAction(object, { variableName: variableName ?? "" }))}
            />
            {createVariableConflicts.length ? <Text type="warning" size="small">{createVariableConflicts.join(" ")}</Text> : null}
            <Text type="tertiary" size="small">Renaming a variable does not rewrite existing expressions.</Text>
            {createVariableReferences.length ? <Text type="warning" size="small">Existing expressions or Change Variable targets may reference this variable.</Text> : null}
          </FieldRow>
          <Field label="Variable ID">
            <Input value={action.id} disabled />
          </Field>
          <Field label="Read only">
            <Switch checked={action.readonly} disabled={readonly} onChange={readOnly => patchObject(updateAction(object, { readonly: readOnly }))} />
          </Field>
          <Field label="Data Type">
            <DataTypeSelector value={action.dataType} disabled={readonly} allowVoid={false} onChange={dataType => patchObject(updateAction(object, { dataType }))} />
            <FieldError issues={getIssuesForField(issues, "action.dataType")} />
            {action.dataType.kind === "object" || action.dataType.kind === "list" ? (
              <Text type="warning" size="small">Entity metadata will be connected in Stage 19.</Text>
            ) : null}
          </Field>
          <Field label="Initial Value">
            <ExpressionEditor
              value={action.initialValue ?? expression("", action.dataType)}
              schema={schema}
              metadata={effectiveCatalog}
              variableIndex={variableIndex}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.initialValue"
              expectedType={action.dataType}
              readonly={readonly}
              onChange={initialValue => patchObject(updateAction(object, { initialValue }))}
            />
            <Text type="tertiary" size="small">Stage 13 stores the expression text only; it does not evaluate expressions.</Text>
          </Field>
          <Field label="Description">
            <TextArea value={action.documentation ?? ""} autosize disabled={readonly} onChange={documentation => patchObject(updateAction(object, { documentation }))} />
          </Field>
        </>
      ) : null}

      {action.kind === "changeVariable" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Change Variable</Title>
          <Field label="Target Variable">
            <VariableSelector
              schema={schema}
              objectId={object.id}
              fieldPath="action.targetVariableName"
              value={action.targetVariableName}
              disabled={readonly}
              includeSystem={false}
              includeReadonly={false}
              scopeMode="index"
              onChange={targetVariableName => patchObject(updateAction(object, { targetVariableName: targetVariableName ?? "" }))}
            />
            <FieldError issues={getIssuesForField(issues, "action.targetVariableName")} />
            <RequiredConfigWarning visible={!action.targetVariableName.trim()}>Target variable 未配置；保存为待配置状态。</RequiredConfigWarning>
          </Field>
          <Field label="New Value Expression">
            <ExpressionEditor
              value={action.newValueExpression}
              schema={schema}
              metadata={effectiveCatalog}
              variableIndex={variableIndex}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.newValueExpression"
              expectedType={resolveVariableReferenceFromIndex(schema, variableIndex, { objectId: object.id, actionId: action.id, fieldPath: "action.targetVariableName" }, action.targetVariableName)?.dataType}
              required
              readonly={readonly}
              onChange={newValueExpression => patchObject(updateAction(object, { newValueExpression }))}
            />
            <Text type="tertiary" size="small">Available variables are computed from the current microflow schema only.</Text>
          </Field>
        </>
      ) : null}
      <GenericActionFields schema={schema} object={object} issues={issues} readonly={readonly} onPatch={onPatch} />
    </Space>
  );
}
