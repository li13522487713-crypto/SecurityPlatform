import { useEffect, useMemo, useState } from "react";
import { Button, Checkbox, Form, Input, Modal, Select, Space, Toast } from "@douyinfe/semi-ui";
import type { MicroflowDataType, MicroflowParameter } from "@atlas/microflow";

import type { MicroflowCreateInput, MicroflowResource } from "./resource-types";

type ExistingMicroflowName = Pick<MicroflowResource, "name">;

interface CreateMicroflowModalProps {
  visible: boolean;
  existingResources?: ExistingMicroflowName[];
  initialModuleId?: string;
  initialModuleName?: string;
  moduleLocked?: boolean;
  onClose: () => void;
  onSubmit: (input: MicroflowCreateInput) => Promise<MicroflowResource>;
  onCreated?: (resource: MicroflowResource) => void;
}

type DataTypeValue = "void" | "boolean" | "integer" | "long" | "decimal" | "string" | "dateTime" | "json";

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

export function CreateMicroflowModal({
  visible,
  existingResources = [],
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
  const [moduleId, setModuleId] = useState("sales");
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
    setModuleId(initialModuleId ?? "sales");
    setTags("microflow");
    setReturnType("void");
    setReturnVariableName("");
    setParameters([]);
    setTemplate("blank");
  }, [initialModuleId, visible]);

  function validate(): boolean {
    const trimmedName = name.trim();
    if (!trimmedName) {
      Toast.warning("微流名称不能为空");
      return false;
    }
    if (!/^[A-Za-z_][A-Za-z0-9_]*$/u.test(trimmedName)) {
      Toast.warning("微流名称只能包含字母、数字和下划线，且不能以数字开头");
      return false;
    }
    if (existingNames.has(trimmedName.toLowerCase())) {
      Toast.warning("微流名称不能重复");
      return false;
    }
    if (!moduleId.trim()) {
      Toast.warning("模块不能为空");
      return false;
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
    return true;
  }

  async function handleSubmit() {
    if (!validate()) {
      return;
    }
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
      Toast.success("微流创建成功");
      onCreated?.(created);
      onClose();
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={visible} title="新建微流" onCancel={onClose} onOk={() => void handleSubmit()} confirmLoading={submitting} width={760} okText="创建">
      <Form labelPosition="top" style={{ maxHeight: "70vh", overflow: "auto", paddingRight: 8 }}>
        <Form.Section text="基本信息">
          <Form.Input field="name" label="Name" value={name} onChange={value => setName(String(value))} placeholder="OrderProcessing" />
          <Form.Input field="displayName" label="显示名称" value={displayName} onChange={value => setDisplayName(String(value))} placeholder="订单处理微流" />
          <Form.TextArea field="description" label="描述" value={description} onChange={value => setDescription(String(value))} autosize />
          <Form.Input field="moduleId" label="模块" value={moduleId} onChange={value => setModuleId(String(value))} disabled={moduleLocked} />
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
      </Form>
    </Modal>
  );
}
