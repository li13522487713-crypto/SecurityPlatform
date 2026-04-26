import { useEffect, useState, useRef } from "react";
import { Button, Empty, Spin, Switch, Table, Tag, Toast, Typography, Form, SideSheet, Space, Card, TextArea } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { WorkspaceSettingsLayout } from "../layouts/workspace-settings-layout";
import {
  getModelConfigsPaged,
  updateModelConfig,
  createModelConfig,
  testModelConfigConnection,
  createModelConfigPromptTestStream,
  type ModelConfigCreateRequest,
  type ModelConfigDto,
  type ModelConfigUpdateRequest
} from "../../services/api-model-config";

type ModelConfigFormValues = {
  name: string;
  providerType: string;
  baseUrl: string;
  defaultModel: string;
  apiKey: string;
};

function stringValue(value: unknown): string {
  return typeof value === "string" ? value : "";
}

function readModelConfigForm(values: Record<string, unknown>): ModelConfigFormValues {
  return {
    name: stringValue(values.name).trim(),
    providerType: stringValue(values.providerType).trim(),
    baseUrl: stringValue(values.baseUrl).trim(),
    defaultModel: stringValue(values.defaultModel).trim(),
    apiKey: stringValue(values.apiKey)
  };
}

export function WorkspaceSettingsModelsPage() {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [items, setItems] = useState<ModelConfigDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [isSideSheetVisible, setIsSideSheetVisible] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [editingModel, setEditingModel] = useState<ModelConfigDto | null>(null);

  const formApi = useRef<{ getValues: () => Record<string, unknown>; submitForm: () => void }>();
  const [testingConnection, setTestingConnection] = useState(false);
  const [connectionResult, setConnectionResult] = useState<{ success: boolean; latencyMs?: number; error?: string } | null>(null);

  const [prompt, setPrompt] = useState("你好，请做个自我介绍。");
  const [testingPrompt, setTestingPrompt] = useState(false);
  const [promptResult, setPromptResult] = useState("");

  const refresh = () => {
    setLoading(true);
    getModelConfigsPaged({ pageIndex: 1, pageSize: 50 }, { workspaceId: workspace.id })
      .then(result => setItems(result.items))
      .catch(() => setItems([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    refresh();
  }, [workspace.id]);

  const handleToggle = async (item: ModelConfigDto, nextEnabled: boolean) => {
    try {
      await updateModelConfig(item.id, {
        name: item.name,
        apiKey: "",
        baseUrl: item.baseUrl,
        defaultModel: item.defaultModel,
        isEnabled: nextEnabled,
        supportsEmbedding: item.supportsEmbedding,
        modelId: item.modelId,
        systemPrompt: item.systemPrompt,
        enableStreaming: item.enableStreaming,
        enableReasoning: item.enableReasoning,
        enableTools: item.enableTools,
        enableVision: item.enableVision,
        enableJsonMode: item.enableJsonMode,
        temperature: item.temperature,
        maxTokens: item.maxTokens,
        topP: item.topP,
        frequencyPenalty: item.frequencyPenalty,
        presencePenalty: item.presencePenalty,
        workspaceId: workspace.id
      });
      Toast.success(t("cozeCreateSuccess"));
      refresh();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    }
  };

  const handleOpenCreate = () => {
    setEditingModel(null);
    setConnectionResult(null);
    setPromptResult("");
    setIsSideSheetVisible(true);
  };

  const handleOpenEdit = (model: ModelConfigDto) => {
    setEditingModel(model);
    setConnectionResult(null);
    setPromptResult("");
    setIsSideSheetVisible(true);
  };

  const handleTestConnection = async () => {
    if (!formApi.current) return;
    const values = readModelConfigForm(formApi.current.getValues());
    if (!values.providerType || !values.baseUrl || !values.defaultModel) {
      Toast.warning("请先填写完整提供商、BaseURL和模型标识");
      return;
    }
    setTestingConnection(true);
    setConnectionResult(null);
    try {
      const result = await testModelConfigConnection({
        modelConfigId: editingModel?.id,
        providerType: values.providerType,
        baseUrl: values.baseUrl,
        apiKey: values.apiKey,
        model: values.defaultModel
      });
      setConnectionResult({ success: result.success, latencyMs: result.latencyMs, error: result.errorMessage });
      if (result.success) {
        Toast.success(`测试连通性成功 (延迟: ${result.latencyMs}ms)`);
      } else {
        Toast.error(result.errorMessage || "测试连通性失败");
      }
    } catch (err) {
      const error = err as Error;
      setConnectionResult({ success: false, error: error.message });
      Toast.error(error.message || "测试连通性失败");
    } finally {
      setTestingConnection(false);
    }
  };

  const handleTestPrompt = async () => {
    if (!formApi.current) return;
    const values = readModelConfigForm(formApi.current.getValues());
    if (!values.providerType || !values.baseUrl || !values.defaultModel) {
      Toast.warning("请先填写完整配置");
      return;
    }
    if (!prompt) {
      Toast.warning("请输入测试内容");
      return;
    }
    
    setTestingPrompt(true);
    setPromptResult("");
    try {
      const { fetchPromise } = createModelConfigPromptTestStream({
        modelConfigId: editingModel?.id,
        providerType: values.providerType,
        baseUrl: values.baseUrl,
        apiKey: values.apiKey,
        model: values.defaultModel,
        prompt,
        enableReasoning: false,
        enableTools: false,
        enableStreaming: true
      });
      
      const response = await fetchPromise;
      if (!response.ok || !response.body) {
        throw new Error("Prompt test failed");
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      let fullText = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || "";
        
        let currentEvent = "message";
        for (const line of lines) {
          if (line.startsWith("event: ")) {
            currentEvent = line.substring(7).trim();
          } else if (line.startsWith("data: ")) {
            const data = line.substring(6);
            if (data === "[DONE]") continue;
            if (currentEvent === "error") {
               Toast.error(data);
               continue;
            }
            if (currentEvent === "thought" || currentEvent === "final") {
               fullText += data;
               setPromptResult(fullText);
            }
          }
        }
      }
    } catch (err) {
      Toast.error((err as Error).message || "对话测试失败");
    } finally {
      setTestingPrompt(false);
    }
  };

  const handleSubmit = async (rawValues: Record<string, unknown>) => {
    const values = readModelConfigForm(rawValues);
    setSubmitting(true);
    try {
      if (editingModel) {
        const request: ModelConfigUpdateRequest = {
          name: values.name,
          apiKey: values.apiKey,
          baseUrl: values.baseUrl,
          defaultModel: values.defaultModel,
          isEnabled: editingModel.isEnabled,
          supportsEmbedding: editingModel.supportsEmbedding,
          modelId: values.defaultModel,
          workspaceId: workspace.id
        };
        await updateModelConfig(editingModel.id, request);
        Toast.success("模型配置已更新");
      } else {
        const request: ModelConfigCreateRequest = {
          name: values.name,
          providerType: values.providerType,
          apiKey: values.apiKey,
          baseUrl: values.baseUrl,
          defaultModel: values.defaultModel,
          modelId: values.defaultModel,
          workspaceId: workspace.id,
          supportsEmbedding: false,
          enableStreaming: true,
          enableReasoning: false,
          enableTools: true,
          enableVision: false,
          enableJsonMode: false,
          temperature: 0.7,
          maxTokens: 2048,
          topP: 1.0,
          frequencyPenalty: 0.0,
          presencePenalty: 0.0
        };
        await createModelConfig(request);
        Toast.success(t("cozeCreateSuccess"));
      }
      setIsSideSheetVisible(false);
      refresh();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  const columns: ColumnProps<ModelConfigDto>[] = [
    { title: t("cozeSettingsPublishColumnName"), dataIndex: "name" },
    { title: t("cozeSettingsPublishColumnType"), dataIndex: "providerType", render: (value: string) => <Tag color="blue">{value}</Tag> },
    { title: "Model", dataIndex: "defaultModel" },
    {
      title: t("cozeSettingsPublishColumnStatus"),
      dataIndex: "isEnabled",
      render: (value: boolean, record) => (
        <Switch checked={value} disabled={!record} onChange={next => record && void handleToggle(record, next)} />
      )
    },
    { title: t("cozeSettingsPublishColumnUpdatedAt"), dataIndex: "createdAt" },
    {
      title: t("cozeCommonAction"),
      dataIndex: "action",
      render: (_, record) => (
        <Button type="tertiary" size="small" disabled={!record} onClick={() => record && handleOpenEdit(record)}>
          编辑
        </Button>
      )
    }
  ];

  return (
    <WorkspaceSettingsLayout activeTab="models">
      <section className="coze-page__toolbar" style={{ display: "flex", gap: "8px" }}>
        <Button onClick={refresh}>{t("cozeCommonRefresh")}</Button>
        <Button theme="solid" onClick={handleOpenCreate}>添加模型配置</Button>
      </section>

      <section className="coze-page__body">
        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : items.length === 0 ? (
          <Empty description={t("cozeSettingsModelsEmpty")} />
        ) : (
          <Table columns={columns} dataSource={items} rowKey="id" pagination={false} />
        )}
      </section>

      <footer className="coze-page__footer">
        <Typography.Text type="tertiary">
          Workspace: {workspace.name || workspace.appKey}
        </Typography.Text>
      </footer>

      <SideSheet
        title={editingModel ? `编辑模型配置 · ${editingModel.name}` : "添加模型配置"}
        visible={isSideSheetVisible}
        onCancel={() => setIsSideSheetVisible(false)}
        size="large"
        footer={
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Button onClick={() => setIsSideSheetVisible(false)}>取消</Button>
            <Button type="primary" theme="solid" htmlType="submit" loading={submitting} onClick={() => formApi.current?.submitForm()}>保存</Button>
          </div>
        }
      >
        <Form 
          getFormApi={api => formApi.current = api} 
          onSubmit={handleSubmit} 
          layout="vertical"
          initValues={editingModel ? {
            name: editingModel.name,
            providerType: editingModel.providerType,
            baseUrl: editingModel.baseUrl,
            defaultModel: editingModel.defaultModel,
            apiKey: "" // Always blank for security, user can fill to overwrite
          } : {}}
        >
          <Form.Input field="name" label="配置名称" rules={[{ required: true }]} />
          <Form.Select field="providerType" label="提供商类型" rules={[{ required: true }]} style={{ width: "100%" }}>
            <Form.Select.Option value="OpenAI">OpenAI</Form.Select.Option>
            <Form.Select.Option value="AzureOpenAI">Azure OpenAI</Form.Select.Option>
            <Form.Select.Option value="Anthropic">Anthropic</Form.Select.Option>
            <Form.Select.Option value="Gemini">Gemini</Form.Select.Option>
            <Form.Select.Option value="Ollama">Ollama</Form.Select.Option>
            <Form.Select.Option value="Volcengine">火山引擎 (Volcengine)</Form.Select.Option>
            <Form.Select.Option value="DeepSeek">DeepSeek</Form.Select.Option>
            <Form.Select.Option value="Custom">自定义 (Custom)</Form.Select.Option>
          </Form.Select>
          <Form.Input field="baseUrl" label="API Base URL" rules={[{ required: true }]} />
          <Form.Input field="defaultModel" label="模型标识 (例如 gpt-4o 或 deepseek-chat)" rules={[{ required: true }]} />
          <Form.Input 
            field="apiKey" 
            label="API Key" 
            type="password" 
            rules={editingModel ? [] : [{ required: true }]} 
            placeholder={editingModel ? "留空表示不修改现有 Key" : "输入你的 API Key"} 
          />
          
          <Space style={{ marginTop: 24, marginBottom: 24 }}>
            <Button onClick={handleTestConnection} loading={testingConnection}>
              测试连通性
            </Button>
            {connectionResult && (
               <Tag color={connectionResult.success ? "green" : "red"}>
                 {connectionResult.success ? `测试通过 (${connectionResult.latencyMs}ms)` : "测试失败"}
               </Tag>
            )}
          </Space>
        </Form>

        <Card title="模型对话测试" headerExtraContent={
           <Button type="primary" onClick={handleTestPrompt} loading={testingPrompt}>发送测试</Button>
        }>
           <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
             <TextArea 
               value={prompt} 
               onChange={v => setPrompt(v)} 
               placeholder="输入测试提示词" 
               rows={3} 
             />
             <div style={{ 
               minHeight: 120, 
               background: "var(--semi-color-fill-0)", 
               padding: 12, 
               borderRadius: 6,
               whiteSpace: "pre-wrap",
               wordBreak: "break-word",
               border: "1px solid var(--semi-color-border)",
               maxHeight: 400,
               overflowY: "auto"
             }}>
               {promptResult || <Typography.Text type="tertiary">测试结果将显示在这里...</Typography.Text>}
             </div>
           </div>
        </Card>
      </SideSheet>
    </WorkspaceSettingsLayout>
  );
}
