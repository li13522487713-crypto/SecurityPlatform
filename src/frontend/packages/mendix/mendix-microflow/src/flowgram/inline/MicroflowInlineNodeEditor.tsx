import { useCallback, type ReactElement } from "react";

import { Button, Input, Select, TextArea, Typography } from "@douyinfe/semi-ui";

import type { MicroflowActionActivity, MicroflowAnnotation, MicroflowAuthoringSchema, MicroflowErrorHandler, MicroflowLoopedActivity, MicroflowTryCatch } from "../../schema";
import type { MicroflowDesignSchema } from "../../schema/types";
import { renameActionOutputVariable, renameLoopIteratorVariable } from "../../schema/utils";
import { renameMicroflowVariable } from "../../variables";
import { VariableSelector } from "../../property-panel/selectors/VariableSelector";
import { updateCallMicroflowReturnBinding } from "../../property-panel/utils/call-microflow-config";
import {
  updateAnnotationObjectConfig,
  updateObject,
} from "../../property-panel/utils/schema-patch";
import type { FlowGramMicroflowNodeData } from "../FlowGramMicroflowTypes";
import { applyEndEventDraft, buildEndEventDraft } from "./end-event-inline";
import { useInlineEditorDraft, type InlineEditorDraft } from "./useInlineEditorDraft";
import { useFlowGramMicroflowContext } from "./useFlowGramMicroflowContext";

const { Text } = Typography;

export interface MicroflowInlineNodeEditorProps {
  data: FlowGramMicroflowNodeData;
  objectId: string;
  schema: MicroflowDesignSchema;
  onSchemaChange: (next: MicroflowDesignSchema, reason: string) => void;
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
  draft,
  updateField,
  readonly,
}: {
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  readonly: boolean;
}) {
  return (
    <FieldRow label="返回表达式">
      <TextArea
        rows={2}
        value={String(draft.returnExpression ?? "")}
        onChange={value => updateField("returnExpression", value)}
        placeholder="输入返回表达式"
        data-flow-editor-selectable="false"
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

function InlineErrorHandlerSection({
  draft,
  updateField,
  readonly,
}: {
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  readonly: boolean;
}) {
  return (
    <>
      <FieldRow label="策略">
        <Select
          value={String(draft.policy ?? "rollback")}
          style={{ width: "100%" }}
          optionList={[
            { label: "rollback", value: "rollback" },
            { label: "continue", value: "continue" },
            { label: "custom", value: "custom" },
          ]}
          onChange={value => updateField("policy", String(value))}
          disabled={readonly}
        />
      </FieldRow>
      <FieldRow label="错误变量">
        <Input
          size="small"
          value={String(draft.customHandlerVariable ?? "")}
          onChange={value => updateField("customHandlerVariable", value)}
          placeholder="输入错误变量名（可选）"
          disabled={readonly}
        />
      </FieldRow>
      <FieldRow label="继续执行">
        <Select
          value={draft.continueOnError ? "true" : "false"}
          style={{ width: "100%" }}
          optionList={[
            { label: "false", value: "false" },
            { label: "true", value: "true" },
          ]}
          onChange={value => updateField("continueOnError", String(value) === "true")}
          disabled={readonly}
        />
      </FieldRow>
    </>
  );
}

function InlineTryCatchSection({
  draft,
  updateField,
  readonly,
}: {
  draft: InlineEditorDraft;
  updateField: (key: string, value: unknown) => void;
  readonly: boolean;
}) {
  return (
    <>
      <FieldRow label="Try Branch">
        <Input
          size="small"
          value={String(draft.tryBranchKey ?? "")}
          onChange={value => updateField("tryBranchKey", value)}
          placeholder="try"
          disabled={readonly}
        />
      </FieldRow>
      <FieldRow label="Catch Branch">
        <Input
          size="small"
          value={String(draft.catchBranchKey ?? "")}
          onChange={value => updateField("catchBranchKey", value)}
          placeholder="catch"
          disabled={readonly}
        />
      </FieldRow>
      <FieldRow label="Finally Branch">
        <Input
          size="small"
          value={String(draft.finallyBranchKey ?? "")}
          onChange={value => updateField("finallyBranchKey", value)}
          placeholder="optional"
          disabled={readonly}
        />
      </FieldRow>
      <FieldRow label="Error Variable">
        <Input
          size="small"
          value={String(draft.errorVariableName ?? "")}
          onChange={value => updateField("errorVariableName", value)}
          placeholder="latestError"
          disabled={readonly}
        />
      </FieldRow>
    </>
  );
}

function simpleVariableReferenceName(raw: unknown): string {
  if (typeof raw !== "string") {
    return "";
  }
  const trimmed = raw.trim();
  const match = /^\$([A-Za-z_][A-Za-z0-9_]*)$/.exec(trimmed);
  return match?.[1] ?? "";
}

function variableReferenceExpression(variableName: string) {
  const trimmed = variableName.trim();
  return {
    raw: trimmed ? `$${trimmed}` : "",
    inferredType: { kind: "unknown" as const, reason: trimmed || "inline-variable-selector" },
    references: {
      variables: trimmed ? [`$${trimmed}`] : [],
      entities: [],
      attributes: [],
      associations: [],
      enumerations: [],
      functions: [],
    },
    diagnostics: [],
  };
}

export function buildInitialDraft(data: FlowGramMicroflowNodeData): InlineEditorDraft {
  const action = data.action as Record<string, unknown> | undefined;

  if (data.objectKind === "endEvent") {
    return buildEndEventDraft(data);
  }
  if (data.objectKind === "loopedActivity") {
    return {
      listVariableName: String(data.listVariableName ?? ""),
      iteratorVariableName: String(data.iteratorVariableName ?? ""),
    };
  }
  if (data.objectKind === "annotation") {
    // annotation 的文本内容存在 title/subtitle 或直接在 action 中
    return { caption: String(action?.text ?? data.title ?? "") };
  }
  if (data.objectKind === "tryCatch") {
    return {
      tryBranchKey: String(data.tryBranchKey ?? "try"),
      catchBranchKey: String(data.catchBranchKey ?? "catch"),
      finallyBranchKey: String(data.finallyBranchKey ?? ""),
      errorVariableName: String(data.errorVariableName ?? "latestError"),
    };
  }
  if (data.objectKind === "errorHandler") {
    return {
      policy: String(data.policy ?? "rollback"),
      customHandlerVariable: String(data.customHandlerVariable ?? ""),
      continueOnError: Boolean(data.continueOnError ?? false),
    };
  }
  if (data.objectKind === "actionActivity") {
    switch (data.actionKind) {
      case "createVariable":
        return {
          variableName: String(action?.variableName ?? ""),
          initialValue: simpleVariableReferenceName((action?.initialValue as Record<string, unknown> | undefined)?.raw),
        };
      case "changeVariable":
        return {
          changeVariableName: String(action?.targetVariableName ?? ""),
          newValue: simpleVariableReferenceName((action?.newValueExpression as Record<string, unknown> | undefined)?.raw),
        };
      case "restCall":
        {
          const response = action?.response as Record<string, unknown> | undefined;
          const handling = response?.handling as Record<string, unknown> | undefined;
          return {
            responseVariableName: String(
              handling?.outputVariableName
              ?? (response?.outputVariableName as string | undefined)
              ?? action?.outputVariableName
              ?? "",
            ),
          };
        }
      case "callMicroflow":
        {
          const returnValue = action?.returnValue as Record<string, unknown> | undefined;
          return {
            returnVariableName: String(
              returnValue?.outputVariableName
              ?? returnValue?.resultVariableName
              ?? action?.outputVariableName
              ?? "",
            ),
          };
        }
      case "errorHandler":
        return {
          policy: String(action?.policy ?? "rollback"),
          customHandlerVariable: String(action?.customHandlerVariable ?? action?.errorVariableName ?? ""),
          continueOnError: Boolean(action?.continueOnError ?? false),
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

export function applyDraft(
  schema: MicroflowAuthoringSchema,
  objectId: string,
  data: FlowGramMicroflowNodeData,
  draft: InlineEditorDraft,
): MicroflowAuthoringSchema {
  switch (data.objectKind) {
    case "endEvent":
      return applyEndEventDraft(schema, objectId, draft);

    case "annotation":
      return updateAnnotationObjectConfig(schema, objectId, { text: String(draft.caption ?? "") } as Partial<MicroflowAnnotation>);

    case "tryCatch":
      return updateObject<MicroflowTryCatch>(schema, objectId, tryCatch => ({
        ...tryCatch,
        tryBranchKey: String(draft.tryBranchKey ?? tryCatch.tryBranchKey).trim() || "try",
        catchBranchKey: String(draft.catchBranchKey ?? tryCatch.catchBranchKey).trim() || "catch",
        finallyBranchKey: String(draft.finallyBranchKey ?? "").trim() || undefined,
        errorVariableName: String(draft.errorVariableName ?? tryCatch.errorVariableName).trim() || "latestError",
      }));

    case "errorHandler":
      return updateObject<MicroflowErrorHandler>(schema, objectId, errorHandler => ({
        ...errorHandler,
        policy: String(draft.policy ?? errorHandler.policy) as MicroflowErrorHandler["policy"],
        customHandlerVariable: String(draft.customHandlerVariable ?? "").trim() || undefined,
        continueOnError: Boolean(draft.continueOnError),
      }));

    case "loopedActivity":
      return renameLoopIteratorVariable(
        updateObject<MicroflowLoopedActivity>(schema, objectId, loop => ({
          ...loop,
          loopSource: loop.loopSource?.kind === "iterableList"
            ? {
                ...loop.loopSource,
                listVariableName: String(draft.listVariableName ?? loop.loopSource.listVariableName),
              }
            : loop.loopSource,
        })),
        objectId,
        String(draft.iteratorVariableName ?? ""),
      );

    case "actionActivity":
      switch (data.actionKind) {
        case "createVariable":
          return updateObject<MicroflowActionActivity>(
            renameMicroflowVariable(schema, objectId, String(draft.variableName ?? "")),
            objectId,
            activity => {
              const caption = activity.autoGenerateCaption
                ? `Create ${String(draft.variableName ?? "").trim() || "Variable"}`
                : activity.caption;
              return {
                ...activity,
                caption,
                action: {
                  ...(activity.action as Record<string, unknown>),
                  caption,
                  initialValue: draft.initialValue
                    ? variableReferenceExpression(String(draft.initialValue))
                    : undefined,
                } as unknown as MicroflowActionActivity["action"],
              };
            },
          );

        case "changeVariable":
          return updateObject<MicroflowActionActivity>(schema, objectId, activity => ({
            ...activity,
            action: {
              ...(activity.action as Record<string, unknown>),
              targetVariableName: String(draft.changeVariableName ?? ""),
              newValueExpression: draft.newValue
                ? variableReferenceExpression(String(draft.newValue))
                : (activity.action as unknown as Record<string, unknown>).newValueExpression,
            } as unknown as MicroflowActionActivity["action"],
          }));

        case "restCall": {
          const nextName = String(draft.responseVariableName ?? "").trim();
          if (nextName) {
            return renameActionOutputVariable(schema, objectId, nextName);
          }
          return updateObject<MicroflowActionActivity>(schema, objectId, activity => activity.action.kind === "restCall" && activity.action.response.handling.kind !== "ignore"
            ? {
                ...activity,
                action: {
                  ...activity.action,
                  response: {
                    ...activity.action.response,
                    handling: {
                      ...activity.action.response.handling,
                      outputVariableName: "",
                    },
                  },
                },
              }
            : activity);
        }

        case "callMicroflow": {
          const nextName = String(draft.returnVariableName ?? "").trim();
          if (nextName) {
            const actionRecord = data.action as Record<string, unknown> | undefined;
            const currentReturnValue = actionRecord?.returnValue as Record<string, unknown> | undefined;
            const currentName = String(currentReturnValue?.outputVariableName ?? currentReturnValue?.resultVariableName ?? "");
            if (currentName) {
              return renameActionOutputVariable(schema, objectId, nextName);
            }
          }
          return updateObject<MicroflowActionActivity>(schema, objectId, activity => activity.action.kind === "callMicroflow"
            ? {
                ...activity,
                action: updateCallMicroflowReturnBinding(activity.action, nextName || undefined),
              }
            : activity);
        }

        case "errorHandler":
          return updateObject<MicroflowActionActivity>(schema, objectId, activity => ({
            ...activity,
            action: {
              ...(activity.action as Record<string, unknown>),
              policy: String(draft.policy ?? (activity.action as Record<string, unknown>).policy ?? "rollback"),
              customHandlerVariable: String(draft.customHandlerVariable ?? "").trim() || undefined,
              continueOnError: Boolean(draft.continueOnError),
            } as unknown as MicroflowActionActivity["action"],
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
  const authoringSchema = schema as unknown as MicroflowAuthoringSchema;
  const initialDraft = buildInitialDraft(data);
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
    onSchemaChange(nextSchema as unknown as MicroflowDesignSchema, "inlineEdit");
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
          draft={draft}
          updateField={updateField}
          readonly={readonly}
        />
      );
      break;
    case "annotation":
      fields = <InlineAnnotationSection draft={draft} updateField={updateField} readonly={readonly} />;
      break;
    case "tryCatch":
      fields = <InlineTryCatchSection draft={draft} updateField={updateField} readonly={readonly} />;
      break;
    case "errorHandler":
      fields = <InlineErrorHandlerSection draft={draft} updateField={updateField} readonly={readonly} />;
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
        case "errorHandler":
          fields = <InlineErrorHandlerSection draft={draft} updateField={updateField} readonly={readonly} />;
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
