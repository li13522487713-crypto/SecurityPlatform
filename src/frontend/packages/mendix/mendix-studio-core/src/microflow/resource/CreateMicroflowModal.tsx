import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Checkbox, Form, Input, Modal, Select, Space, Toast, Typography } from "@douyinfe/semi-ui";
import type { MicroflowDataType, MicroflowParameter } from "@atlas/microflow";

import type { MicroflowApiFieldError } from "../contracts/api/api-envelope";
import { getMicroflowApiError, getMicroflowErrorActionHint } from "../adapter/http/microflow-api-error";
import type { MicroflowCreateInput, MicroflowResource } from "./resource-types";

type ExistingMicroflowName = Pick<MicroflowResource, "name">;
const { Text } = Typography;

interface CreateMicroflowModalProps {
  visible: boolean;
  existingResources?: ExistingMicroflowName[];
  defaultModuleId?: string;
  moduleOptions?: Array<{ value: string; label: string }>;
  initialModuleId?: string;
  initialModuleName?: string;
  moduleLocked?: boolean;
  onClose: () => void;
  onSubmit: (input: MicroflowCreateInput) => Promise<MicroflowResource>;
  onCreated?: (resource: MicroflowResource) => void;
}

type DataTypeValue = "void" | "boolean" | "integer" | "long" | "decimal" | "string" | "dateTime" | "json";

type SubmitErrorState = {
  status?: number;
  code?: string;
  message: string;
  traceId?: string;
  fieldErrors: MicroflowApiFieldError[];
  retryHint?: string;
};

const dataTypeOptions = [
  { value: "void", label: "Void" },
  { value: "boolean", label: "Boolean" },
  { value: "integer", label: "Integer" },
  { value: "long", label: "Long" },
  { value: "decimal", label: "Decimal" },
  { value: "string", label: "String" },
  { value: "dateTime", label: "DateTime" },
  { value: "json", label: "Json" }
];

const templateOptions = [
  { value: "blank", label: "Blank" },
  { value: "orderProcessing", label: "Order Processing" },
  { value: "approval", label: "Approval" },
  { value: "restErrorHandling", label: "REST Error Handling" },
  { value: "loopProcessing", label: "Loop Processing" },
  { value: "objectTypeDecision", label: "Object Type Decision" },
  { value: "listProcessing", label: "List Processing" }
];

function toDataType(value: DataTypeValue): MicroflowDataType {
  return value === "void" ? { kind: "void" } : { kind: value };
}

function makeParameter(name: string, dataType: DataTypeValue): MicroflowParameter {
  const id = `param-${name || Date.now()}`;
  return {
    id,
    stableId: id,
    name,
    dataType: toDataType(dataType),
    required: true
  };
}

function mergeFieldErrors(input: MicroflowApiFieldError[] | undefined): Record<string, string> {
  const result: Record<string, string> = {};
  for (const item of input ?? []) {
    const normalizedPath = item.fieldPath.replace(/^input\./u, "");
    if (!result[normalizedPath]) {
      result[normalizedPath] = item.message;
    }
  }
  return result;
}

function resolveReadableErrorMessage(status: number | undefined, code: string | undefined, fallbackMessage: string): string {
  if (code === "MICROFLOW_NETWORK_ERROR" || code === "MICROFLOW_SERVICE_UNAVAILABLE") {
    return "微流服务不可用，请检查网络或后端服务。";
  }
  if (status === 401 || code === "MICROFLOW_UNAUTHORIZED") {
    return "登录已失效，请重新登录。";
  }
  if (status === 403 || code === "MICROFLOW_PERMISSION_DENIED") {
    return "当前账号无权限创建微流。";
  }
  if (status === 409 || code === "MICROFLOW_NAME_DUPLICATED") {
    return "同名微流已存在。";
  }
  if (status === 422 || code === "MICROFLOW_VALIDATION_FAILED") {
    return fallbackMessage || "微流校验失败，请检查输入字段。";
  }
  if (status === 500) {
    return "微流服务异常，请联系管理员。";
  }
  return fallbackMessage || "微流服务异常。";
}

export function CreateMicroflowModal({
  visible,
  existingResources = [],
  defaultModuleId,
  moduleOptions = [],
  initialModuleId,
  initialModuleName,
  moduleLocked = false,
  onClose,
  onSubmit,
  onCreated
}: CreateMicroflowModalProps) {
  const [name, setName] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [description, setDescription] = useState("");
  const [moduleId, setModuleId] = useState("");
  const [tags, setTags] = useState("microflow");
  const [returnType, setReturnType] = useState<DataTypeValue>("void");
  const [returnVariableName, setReturnVariableName] = useState("");
  const [parameters, setParameters] = useState<MicroflowParameter[]>([]);
  const [applyEntityAccess, setApplyEntityAccess] = useState(true);
  const [allowedRoles, setAllowedRoles] = useState("BusinessUser");
  const [allowConcurrentExecution, setAllowConcurrentExecution] = useState(true);
  const [concurrencyErrorMessage, setConcurrencyErrorMessage] = useState("");
  const [exportLevel, setExportLevel] = useState<"hidden" | "module" | "public">("module");
  const [markAsUsed, setMarkAsUsed] = useState(true);
  const [asMicroflowAction, setAsMicroflowAction] = useState(false);
  const [asWorkflowAction, setAsWorkflowAction] = useState(false);
  const [urlEnabled, setUrlEnabled] = useState(false);
  const [urlPath, setUrlPath] = useState("");
  const [template, setTemplate] = useState<MicroflowCreateInput["template"]>("blank");
  const [submitting, setSubmitting] = useState(false);
  const submittingRef = useRef(false);
  const [submitError, setSubmitError] = useState<SubmitErrorState>();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const resolvedDefaultModuleId = defaultModuleId ?? initialModuleId ?? "";

  const existingNames = useMemo(
    () => new Set(existingResources.map(item => item.name.toLowerCase())),
    [existingResources]
  );

  useEffect(() => {
    if (!visible) {
      return;
    }
    setName("");
    setDisplayName("");
    setDescription("");
    setModuleId(resolvedDefaultModuleId);
    setTags("microflow");
    setReturnType("void");
    setReturnVariableName("");
    setParameters([]);
    setTemplate("blank");
    setSubmitError(undefined);
    setFieldErrors({});
    setSubmitting(false);
    submittingRef.current = false;
  }, [resolvedDefaultModuleId, visible]);

  function validate(): boolean {
    const trimmedName = name.trim();
    const nextFieldErrors: Record<string, string> = {};
    setSubmitError(undefined);
    if (!trimmedName) {
      nextFieldErrors.name = "name 不能为空。";
    }
    if (trimmedName && !/^[A-Za-z][A-Za-z0-9_]*$/u.test(trimmedName)) {
      nextFieldErrors.name = "name 必须以字母开头，且只能包含字母、数字和下划线。";
    }
    if (existingNames.has(trimmedName.toLowerCase())) {
      nextFieldErrors.name = "同名微流已存在。";
    }
    const trimmedModuleId = moduleId.trim();
    if (!trimmedModuleId) {
      nextFieldErrors.moduleId = resolvedDefaultModuleId ? "moduleId 不能为空。" : "缺少模块上下文，无法创建微流。";
    }
    const parameterNames = parameters.map(parameter => parameter.name.trim()).filter(Boolean);
    if (parameterNames.length !== parameters.length || parameterNames.some(value => !/^[A-Za-z_][A-Za-z0-9_]*$/u.test(value))) {
      Toast.warning("参数名格式不合法");
      return false;
    }
    if (new Set(parameterNames.map(value => value.toLowerCase())).size !== parameterNames.length) {
      Toast.warning("参数名不能重复");
      return false;
    }
    if (urlEnabled && urlPath && !urlPath.startsWith("/")) {
      Toast.warning("URL 路径必须以 / 开头");
      return false;
    }
    setFieldErrors(nextFieldErrors);
    if (Object.keys(nextFieldErrors).length > 0) {
      Toast.warning(nextFieldErrors.name ?? nextFieldErrors.moduleId ?? "请先修正表单错误。");
      return false;
    }
    return true;
  }

  async function handleSubmit() {
    if (submittingRef.current || submitting) {
      return;
    }
    if (!validate()) {
      return;
    }
    submittingRef.current = true;
    setSubmitting(true);
    try {
      const created = await onSubmit({
        name: name.trim(),
        displayName: displayName.trim() || name.trim(),
        description: description.trim(),
        moduleId: moduleId.trim(),
        moduleName: initialModuleName?.trim() || moduleId.trim(),
        tags: tags.split(",").map(tag => tag.trim()).filter(Boolean),
        parameters,
        returnType: toDataType(returnType),
        returnVariableName: returnVariableName.trim() || undefined,
        security: {
          applyEntityAccess,
          allowedModuleRoleIds: allowedRoles.split(",").map(role => role.trim()).filter(Boolean),
          allowedRoleNames: allowedRoles.split(",").map(role => role.trim()).filter(Boolean)
        },
        concurrency: {
          allowConcurrentExecution,
          errorMessage: concurrencyErrorMessage.trim() || undefined,
          errorMicroflowId: null
        },
        exposure: {
          exportLevel,
          markAsUsed,
          asMicroflowAction: { enabled: asMicroflowAction },
          asWorkflowAction: { enabled: asWorkflowAction },
          url: { enabled: urlEnabled, path: urlPath.trim() || undefined }
        },
        template
      });
      setSubmitError(undefined);
      setFieldErrors({});
      Toast.success("微流创建成功");
      onCreated?.(created);
      onClose();
    } catch (caught) {
      const apiError = getMicroflowApiError(caught);
      const status = apiError.httpStatus;
      const code = apiError.code;
      const nextFieldErrors = mergeFieldErrors(apiError.fieldErrors);
      if (status === 409 || code === "MICROFLOW_NAME_DUPLICATED") {
        nextFieldErrors.name = nextFieldErrors.name ?? "同名微流已存在。";
      }
      const readableMessage = resolveReadableErrorMessage(status, code, apiError.message);
      setFieldErrors(nextFieldErrors);
      setSubmitError({
        status,
        code,
        message: readableMessage,
        traceId: apiError.traceId,
        fieldErrors: apiError.fieldErrors ?? [],
        retryHint: getMicroflowErrorActionHint(apiError) || (apiError.retryable ? "该错误可重试，请稍后再试。" : undefined),
      });
      Toast.error(readableMessage);
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={visible} title="新建微流" onCancel={onClose} onOk={() => void handleSubmit()} confirmLoading={submitting} width={760} okText="创建">
      <Form labelPosition="top" style={{ maxHeight: "70vh", overflow: "auto", paddingRight: 8 }}>
        <Form.Section text="基本信息">
          <Form.Input field="name" label="Name" value={name} onChange={value => setName(String(value))} placeholder="OrderProcessing" />
          {fieldErrors.name ? <Text type="danger" size="small">{fieldErrors.name}</Text> : null}
          <Form.Input field="displayName" label="显示名称" value={displayName} onChange={value => setDisplayName(String(value))} placeholder="订单处理微流" />
          <Form.TextArea field="description" label="描述" value={description} onChange={value => setDescription(String(value))} autosize />
          {moduleLocked ? (
            <Form.Input
              field="moduleIdDisplay"
              label="模块"
              value={moduleOptions.find(item => item.value === moduleId)?.label ?? initialModuleName ?? moduleId}
              disabled
              placeholder="缺少模块上下文"
            />
          ) : moduleOptions.length > 0 ? (
            <Form.Select
              field="moduleId"
              label="模块"
              value={moduleId}
              onChange={value => setModuleId(String(value ?? ""))}
              disabled={moduleLocked}
              optionList={moduleOptions}
              placeholder="请选择模块"
            />
          ) : (
            <Form.Input field="moduleId" label="模块" value={moduleId} onChange={value => setModuleId(String(value))} disabled={moduleLocked} placeholder="请输入模块 ID" />
          )}
          {fieldErrors.moduleId ? <Text type="danger" size="small">{fieldErrors.moduleId}</Text> : null}
          <Form.Input field="tags" label="标签（逗号分隔）" value={tags} onChange={value => setTags(String(value))} />
          <Form.Select field="template" label="模板" value={template} onChange={value => setTemplate(value as MicroflowCreateInput["template"])} optionList={templateOptions} />
        </Form.Section>
        <Form.Section text="输入参数">
          <Space vertical align="start" style={{ width: "100%" }}>
            {parameters.map((parameter, index) => (
              <Space key={parameter.id} wrap>
                <Input value={parameter.name} placeholder="parameterName" onChange={value => setParameters(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, name: value } : item))} />
                <Select value={parameter.dataType.kind} optionList={dataTypeOptions.filter(item => item.value !== "void")} onChange={value => setParameters(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, dataType: toDataType(value as DataTypeValue) } : item))} style={{ width: 160 }} />
                <Checkbox checked={parameter.required} onChange={event => setParameters(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, required: Boolean(event.target.checked) } : item))}>必填</Checkbox>
                <Input value={parameter.documentation ?? ""} placeholder="说明" onChange={value => setParameters(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, documentation: value } : item))} />
                <Button type="danger" theme="borderless" onClick={() => setParameters(current => current.filter((_, itemIndex) => itemIndex !== index))}>删除</Button>
              </Space>
            ))}
            <Button onClick={() => setParameters(current => [...current, makeParameter(`parameter${current.length + 1}`, "string")])}>添加参数</Button>
          </Space>
        </Form.Section>
        <Form.Section text="返回值">
          <Form.Select field="returnType" label="返回类型" value={returnType} onChange={value => setReturnType(value as DataTypeValue)} optionList={dataTypeOptions} />
          <Form.Input field="returnVariableName" label="返回变量名" value={returnVariableName} onChange={value => setReturnVariableName(String(value))} />
        </Form.Section>
        <Form.Section text="权限 / 运行策略 / 暴露设置">
          <Checkbox checked={applyEntityAccess} onChange={event => setApplyEntityAccess(Boolean(event.target.checked))}>应用实体权限</Checkbox>
          <Form.Input field="allowedRoles" label="允许角色（逗号分隔）" value={allowedRoles} onChange={value => setAllowedRoles(String(value))} />
          <Checkbox checked={allowConcurrentExecution} onChange={event => setAllowConcurrentExecution(Boolean(event.target.checked))}>允许并发执行</Checkbox>
          <Form.Input field="concurrencyErrorMessage" label="并发错误提示" value={concurrencyErrorMessage} onChange={value => setConcurrencyErrorMessage(String(value))} />
          <Form.Select field="exportLevel" label="导出级别" value={exportLevel} onChange={value => setExportLevel(value as "hidden" | "module" | "public")} optionList={[{ value: "hidden", label: "Hidden" }, { value: "module", label: "Module" }, { value: "public", label: "Public" }]} />
          <Space wrap>
            <Checkbox checked={markAsUsed} onChange={event => setMarkAsUsed(Boolean(event.target.checked))}>标记为使用中</Checkbox>
            <Checkbox checked={asMicroflowAction} onChange={event => setAsMicroflowAction(Boolean(event.target.checked))}>作为微流动作</Checkbox>
            <Checkbox checked={asWorkflowAction} onChange={event => setAsWorkflowAction(Boolean(event.target.checked))}>作为工作流动作</Checkbox>
            <Checkbox checked={urlEnabled} onChange={event => setUrlEnabled(Boolean(event.target.checked))}>启用 URL</Checkbox>
          </Space>
          <Form.Input field="urlPath" label="URL path" value={urlPath} onChange={value => setUrlPath(String(value))} placeholder="/orders/process" />
        </Form.Section>
        {submitError ? (
          <Form.Section text="创建失败">
            <Space vertical align="start" spacing={4}>
              <Text type="danger">{submitError.message}</Text>
              <Text size="small">status: {submitError.status ?? "-"}</Text>
              <Text size="small">code: {submitError.code ?? "-"}</Text>
              <Text size="small">traceId: {submitError.traceId ?? "-"}</Text>
              {submitError.retryHint ? <Text size="small">{submitError.retryHint}</Text> : null}
              {submitError.fieldErrors.length > 0 ? (
                <Space vertical align="start" spacing={2}>
                  {submitError.fieldErrors.map(item => (
                    <Text size="small" type="danger" key={`${item.fieldPath}:${item.code}`}>{item.fieldPath}: {item.message} ({item.code})</Text>
                  ))}
                </Space>
              ) : null}
            </Space>
          </Form.Section>
        ) : null}
      </Form>
    </Modal>
  );
}
