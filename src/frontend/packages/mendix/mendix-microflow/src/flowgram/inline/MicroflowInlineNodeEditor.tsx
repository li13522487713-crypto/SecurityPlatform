import { useCallback, type ReactElement } from "react";

import { Button, Input, TextArea, Typography } from "@douyinfe/semi-ui";

import type { MicroflowActionActivity, MicroflowAnnotation, MicroflowAuthoringSchema, MicroflowEndEvent, MicroflowLoopedActivity, MicroflowSchema } from "../../schema";
import { VariableSelector } from "../../property-panel/selectors/VariableSelector";
import {
  updateAnnotationObjectConfig,
  updateEndEventConfig,
  updateObject,
} from "../../property-panel/utils/schema-patch";
import type { FlowGramMicroflowNodeData } from "../FlowGramMicroflowTypes";
import { useInlineEditorDraft, type InlineEditorDraft } from "./useInlineEditorDraft";
import { useFlowGramMicroflowContext } from "./useFlowGramMicroflowContext";

const { Text } = Typography;

export interface MicroflowInlineNodeEditorProps {
  data: FlowGramMicroflowNodeData;
  objectId: string;
  schema: MicroflowSchema;
  onSchemaChange: (next: MicroflowSchema, reason: string) => void;
  onCollapse: () => void;
  registerDraftValidator: ((fn: (() => { valid: boolean; summary: string }) | null) => void) | undefined;
}

function FieldRow({ label, children }: { label: string; children: ReactElement }) {
  return (
    <div className="microflow-flowgram-node__inline-editor-field" draggable={false}>
      <label className="microflow-flowgram-node__inline-editor-label">{label}</label>
      {children}
    </div>
  );
}

function InlineEndEventSection({
  objectId,
  schema,
  draft,
  updateField,
  readonly,
}: {
  objectId: string;
  schema: MicroflowAuthoringSchema;
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  readonly: boolean;
}) {
  return (
    <FieldRow label="返回变量">
      <VariableSelector
        schema={schema}
        objectId={objectId}
        value={draft.returnVariableName as string | undefined}
        onChange={name => updateField("returnVariableName", name)}
        placeholder="选择返回变量"
        disabled={readonly}
      />
    </FieldRow>
  );
}

function InlineCreateVariableSection({
  objectId,
  schema,
  draft,
  updateField,
  fieldErrors,
  readonly,
}: {
  objectId: string;
  schema: MicroflowAuthoringSchema;
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  fieldErrors: Record<string, string>;
  readonly: boolean;
}) {
  return (
    <>
      <FieldRow label="变量名">
        <div>
          <Input
            size="small"
            value={String(draft.variableName ?? "")}
            onChange={v => updateField("variableName", v)}
            validateStatus={fieldErrors.variableName ? "error" : undefined}
            placeholder="输入变量名"
            disabled={readonly}
          />
          {fieldErrors.variableName ? (
            <Text type="danger" size="small">{fieldErrors.variableName}</Text>
          ) : null}
        </div>
      </FieldRow>
      <FieldRow label="初始值（变量）">
        <VariableSelector
          schema={schema}
          objectId={objectId}
          value={draft.initialValue as string | undefined}
          onChange={name => updateField("initialValue", name)}
          placeholder="选择初始值变量（可选）"
          disabled={readonly}
        />
      </FieldRow>
    </>
  );
}

function InlineChangeVariableSection({
  objectId,
  schema,
  draft,
  updateField,
  readonly,
}: {
  objectId: string;
  schema: MicroflowAuthoringSchema;
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  readonly: boolean;
}) {
  return (
    <>
      <FieldRow label="目标变量">
        <VariableSelector
          schema={schema}
          objectId={objectId}
          value={draft.changeVariableName as string | undefined}
          onChange={name => updateField("changeVariableName", name)}
          placeholder="选择要修改的变量"
          writableOnly
          disabled={readonly}
        />
      </FieldRow>
      <FieldRow label="新值（变量）">
        <VariableSelector
          schema={schema}
          objectId={objectId}
          value={draft.newValue as string | undefined}
          onChange={name => updateField("newValue", name)}
          placeholder="选择新值变量（可选）"
          disabled={readonly}
        />
      </FieldRow>
    </>
  );
}

function InlineRestCallSection({
  objectId,
  schema,
  draft,
  updateField,
  data,
  readonly,
}: {
  objectId: string;
  schema: MicroflowAuthoringSchema;
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  data: FlowGramMicroflowNodeData;
  readonly: boolean;
}) {
  return (
    <>
      {data.subtitle ? (
        <div className="microflow-flowgram-node__inline-editor-field">
          <Text size="small" type="tertiary">{data.subtitle}</Text>
        </div>
      ) : null}
      <FieldRow label="响应变量">
        <VariableSelector
          schema={schema}
          objectId={objectId}
          value={draft.responseVariableName as string | undefined}
          onChange={name => updateField("responseVariableName", name)}
          placeholder="选择响应输出变量"
          disabled={readonly}
        />
      </FieldRow>
    </>
  );
}

function InlineCallMicroflowSection({
  objectId,
  schema,
  draft,
  updateField,
  data,
  readonly,
}: {
  objectId: string;
  schema: MicroflowAuthoringSchema;
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  data: FlowGramMicroflowNodeData;
  readonly: boolean;
}) {
  return (
    <>
      {data.subtitle ? (
        <div className="microflow-flowgram-node__inline-editor-field">
          <Text size="small" type="tertiary">{data.subtitle}</Text>
        </div>
      ) : null}
      <FieldRow label="返回变量">
        <VariableSelector
          schema={schema}
          objectId={objectId}
          value={draft.returnVariableName as string | undefined}
          onChange={name => updateField("returnVariableName", name)}
          placeholder="选择返回值变量（可选）"
          disabled={readonly}
        />
      </FieldRow>
    </>
  );
}

function InlineLoopSection({
  objectId,
  schema,
  draft,
  updateField,
  fieldErrors,
  readonly,
}: {
  objectId: string;
  schema: MicroflowAuthoringSchema;
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  fieldErrors: Record<string, string>;
  readonly: boolean;
}) {
  return (
    <>
      <FieldRow label="列表变量">
        <VariableSelector
          schema={schema}
          objectId={objectId}
          value={draft.listVariableName as string | undefined}
          onChange={name => updateField("listVariableName", name)}
          allowedTypeKinds={["list"]}
          placeholder="选择列表变量"
          disabled={readonly}
        />
      </FieldRow>
      <FieldRow label="迭代器变量名">
        <div>
          <Input
            size="small"
            value={String(draft.iteratorVariableName ?? "")}
            onChange={v => updateField("iteratorVariableName", v)}
            validateStatus={fieldErrors.iteratorVariableName ? "error" : undefined}
            placeholder="输入迭代器变量名"
            disabled={readonly}
          />
          {fieldErrors.iteratorVariableName ? (
            <Text type="danger" size="small">{fieldErrors.iteratorVariableName}</Text>
          ) : null}
        </div>
      </FieldRow>
    </>
  );
}

function InlineAnnotationSection({
  draft,
  updateField,
  readonly,
}: {
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  readonly: boolean;
}) {
  return (
    <FieldRow label="注释文本">
      <TextArea
        rows={3}
        size="small"
        value={String(draft.caption ?? "")}
        onChange={v => updateField("caption", v)}
        placeholder="输入注释内容"
        data-flow-editor-selectable="false"
        disabled={readonly}
      />
    </FieldRow>
  );
}

function InlineStartEventSection({ schema }: { schema: MicroflowAuthoringSchema }) {
  const paramCount = schema.parameters?.length ?? 0;
  return (
    <div className="microflow-flowgram-node__inline-editor-field">
      <Text size="small" type="tertiary">
        {paramCount === 0 ? "无入参（在属性面板配置）" : `${paramCount} 个入参（在属性面板配置）`}
      </Text>
    </div>
  );
}

function buildInitialDraft(data: FlowGramMicroflowNodeData, schema: MicroflowAuthoringSchema): InlineEditorDraft {
  const action = data.action as Record<string, unknown> | undefined;
  const objectId = data.objectId;

  if (data.objectKind === "endEvent") {
    const endEvent = schema.objectCollection.objects.find(o => o.id === objectId) as MicroflowEndEvent | undefined;
    return {
      returnVariableName: (endEvent as unknown as Record<string, unknown>)?.returnVariableName ?? action?.outputVariableName ?? "",
    };
  }
  if (data.objectKind === "loopedActivity") {
    const loop = schema.objectCollection.objects.find(o => o.id === objectId) as MicroflowLoopedActivity | undefined;
    const src = loop?.loopSource as Record<string, unknown> | undefined;
    return {
      listVariableName: String(src?.sourceListVariableName ?? ""),
      iteratorVariableName: loop?.iteratorVariableName ?? "",
    };
  }
  if (data.objectKind === "annotation") {
    const obj = schema.objectCollection.objects.find(o => o.id === objectId) as MicroflowAnnotation | undefined;
    return { caption: obj?.caption ?? "" };
  }
  if (data.objectKind === "actionActivity") {
    switch (data.actionKind) {
      case "createVariable":
        return {
          variableName: String(action?.variableName ?? ""),
          initialValue: String(action?.value ?? ""),
        };
      case "changeVariable":
        return {
          changeVariableName: String(action?.variableName ?? ""),
          newValue: String(action?.value ?? ""),
        };
      case "restCall":
        return {
          responseVariableName: String(
            (action?.response as Record<string, unknown> | undefined)?.outputVariableName ?? action?.outputVariableName ?? "",
          ),
        };
      case "callMicroflow":
        return {
          returnVariableName: String(action?.outputVariableName ?? ""),
        };
      default:
        return {};
    }
  }
  return {};
}

function buildValidators(data: FlowGramMicroflowNodeData): Record<string, (v: unknown) => string | null> {
  const namePattern = /^[A-Za-z_][A-Za-z0-9_]*$/;
  if (data.objectKind === "actionActivity" && data.actionKind === "createVariable") {
    return {
      variableName: v => {
        const s = String(v ?? "").trim();
        if (!s) return "变量名不能为空";
        if (!namePattern.test(s)) return "变量名只能包含字母、数字和下划线，且不能以数字开头";
        return null;
      },
    };
  }
  if (data.objectKind === "loopedActivity") {
    return {
      iteratorVariableName: v => {
        const s = String(v ?? "").trim();
        if (!s) return "迭代器变量名不能为空";
        if (!namePattern.test(s)) return "变量名只能包含字母、数字和下划线，且不能以数字开头";
        return null;
      },
    };
  }
  return {};
}

function applyDraft(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  data: FlowGramMicroflowNodeData,
  draft: InlineEditorDraft,
): MicroflowAuthoringSchema {
  switch (data.objectKind) {
    case "endEvent":
      return updateEndEventConfig(schema, objectId, {
        returnValue: draft.returnVariableName
          ? { kind: "variable", variableName: String(draft.returnVariableName) } as unknown as MicroflowEndEvent["returnValue"]
          : undefined,
      } as Partial<MicroflowEndEvent>);

    case "annotation":
      return updateAnnotationObjectConfig(schema, objectId, { caption: String(draft.caption ?? "") });

    case "loopedActivity":
      return updateObject<MicroflowLoopedActivity>(schema, objectId, loop => ({
        ...loop,
        iteratorVariableName: String(draft.iteratorVariableName ?? loop.iteratorVariableName),
        loopSource: loop.loopSource?.kind === "iterableList"
          ? { ...loop.loopSource, sourceListVariableName: String(draft.listVariableName ?? loop.loopSource.sourceListVariableName) }
          : loop.loopSource,
      }));

    case "actionActivity":
      switch (data.actionKind) {
        case "createVariable":
          return updateObject<MicroflowActionActivity>(schema, objectId, activity => ({
            ...activity,
            action: {
              ...activity.action,
              variableName: String(draft.variableName ?? ""),
              value: draft.initialValue ? { kind: "variable", variableName: String(draft.initialValue) } : activity.action.value,
            } as MicroflowActionActivity["action"],
          }));

        case "changeVariable":
          return updateObject<MicroflowActionActivity>(schema, objectId, activity => ({
            ...activity,
            action: {
              ...activity.action,
              variableName: String(draft.changeVariableName ?? ""),
              value: draft.newValue ? { kind: "variable", variableName: String(draft.newValue) } : activity.action.value,
            } as MicroflowActionActivity["action"],
          }));

        case "restCall":
          return updateObject<MicroflowActionActivity>(schema, objectId, activity => ({
            ...activity,
            action: {
              ...activity.action,
              outputVariableName: String(draft.responseVariableName ?? ""),
            } as MicroflowActionActivity["action"],
          }));

        case "callMicroflow":
          return updateObject<MicroflowActionActivity>(schema, objectId, activity => ({
            ...activity,
            action: {
              ...activity.action,
              outputVariableName: String(draft.returnVariableName ?? ""),
            } as MicroflowActionActivity["action"],
          }));

        default:
          return schema;
      }

    default:
      return schema;
  }
}

export function MicroflowInlineNodeEditor({
  data,
  objectId,
  schema,
  onSchemaChange,
  onCollapse,
  registerDraftValidator,
}: MicroflowInlineNodeEditorProps): ReactElement | null {
  const { readonly } = useFlowGramMicroflowContext();
  const authoringSchema = schema as MicroflowAuthoringSchema;
  const initialDraft = buildInitialDraft(data, authoringSchema);
  const validators = buildValidators(data);

  const { draft, fieldErrors, isDraftValid, updateField } = useInlineEditorDraft(
    initialDraft,
    validators,
    registerDraftValidator,
  );

  const handleSave = useCallback(() => {
    if (!isDraftValid()) {
      return;
    }
    const nextSchema = applyDraft(authoringSchema, objectId, data, draft);
    onSchemaChange(nextSchema, "inlineEdit");
    onCollapse();
  }, [isDraftValid, authoringSchema, objectId, data, draft, onSchemaChange, onCollapse]);

  const handleCancel = useCallback(() => {
    onCollapse();
  }, [onCollapse]);

  let fields: ReactElement | null = null;

  switch (data.objectKind) {
    case "startEvent":
      fields = <InlineStartEventSection schema={authoringSchema} />;
      break;
    case "endEvent":
      fields = (
        <InlineEndEventSection
          objectId={objectId}
          schema={authoringSchema}
          draft={draft}
          updateField={updateField}
          readonly={readonly}
        />
      );
      break;
    case "annotation":
      fields = <InlineAnnotationSection draft={draft} updateField={updateField} readonly={readonly} />;
      break;
    case "loopedActivity":
      fields = (
        <InlineLoopSection
          objectId={objectId}
          schema={authoringSchema}
          draft={draft}
          updateField={updateField}
          fieldErrors={fieldErrors}
          readonly={readonly}
        />
      );
      break;
    case "actionActivity":
      switch (data.actionKind) {
        case "createVariable":
          fields = (
            <InlineCreateVariableSection
              objectId={objectId}
              schema={authoringSchema}
              draft={draft}
              updateField={updateField}
              fieldErrors={fieldErrors}
              readonly={readonly}
            />
          );
          break;
        case "changeVariable":
          fields = (
            <InlineChangeVariableSection
              objectId={objectId}
              schema={authoringSchema}
              draft={draft}
              updateField={updateField}
              readonly={readonly}
            />
          );
          break;
        case "restCall":
          fields = (
            <InlineRestCallSection
              objectId={objectId}
              schema={authoringSchema}
              draft={draft}
              updateField={updateField}
              data={data}
              readonly={readonly}
            />
          );
          break;
        case "callMicroflow":
          fields = (
            <InlineCallMicroflowSection
              objectId={objectId}
              schema={authoringSchema}
              draft={draft}
              updateField={updateField}
              data={data}
              readonly={readonly}
            />
          );
          break;
        default:
          return null;
      }
      break;
    default:
      return null;
  }

  const isStartEvent = data.objectKind === "startEvent";

  return (
    <>
      {fields}
      {!isStartEvent && !readonly ? (
        <div className="microflow-flowgram-node__inline-editor-actions">
          <Button size="small" theme="borderless" type="tertiary" onClick={handleCancel}>
            取消
          </Button>
          <Button
            size="small"
            type="primary"
            disabled={!isDraftValid()}
            onClick={handleSave}
          >
            保存
          </Button>
        </div>
      ) : !isStartEvent && readonly ? (
        <div className="microflow-flowgram-node__inline-editor-actions">
          <Button size="small" theme="borderless" type="tertiary" onClick={handleCancel}>
            关闭
          </Button>
        </div>
      ) : null}
    </>
  );
}
