import type { Dispatch, SetStateAction } from "react";
import {
  Button,
  Descriptions,
  Input,
  InputNumber,
  Select,
  Space,
  Switch,
  Tag,
  Typography
} from "@douyinfe/semi-ui";
import { AGENT_PROMPT_SECTION_MAP, normalizeAllowlistText, stringifyAllowlist } from "./agent-ide-helpers";
import type { AgentPromptSections } from "./agent-ide-helpers";
import type { AgentConfigNavKey } from "./agent-workbench-types";
import type {
  AgentDetail,
  AgentDatabaseBindingInput,
  AgentKnowledgeBindingInput,
  AgentPluginBindingInput,
  AgentVariableBindingInput,
  ModelConfigItem,
  WorkflowListItem
} from "../types";

const MODEL_TAB_PROMPT_SECTIONS = AGENT_PROMPT_SECTION_MAP.filter(
  section =>
    section.key === "goals" ||
    section.key === "skills" ||
    section.key === "outputFormat" ||
    section.key === "constraints"
);

export interface AgentWorkbenchConfigTabProps {
  activeNavKey: AgentConfigNavKey;
  detail: AgentDetail | null;
  name: string;
  setName: (value: string) => void;
  description: string;
  setDescription: (value: string) => void;
  avatarUrl: string;
  setAvatarUrl: (value: string) => void;
  openingMessage: string;
  setOpeningMessage: (value: string) => void;
  presetQuestionsInput: string;
  setPresetQuestionsInput: (value: string) => void;
  promptSections: AgentPromptSections;
  updatePromptSection: (key: keyof AgentPromptSections, value: string) => void;
  saving: boolean;
  resourceReady: boolean;
  modelConfigId: string | undefined;
  setModelConfigId: (value: string | undefined) => void;
  modelConfigs: ModelConfigItem[];
  workflowId: string | undefined;
  setWorkflowId: (value: string | undefined) => void;
  workflowOptions: WorkflowListItem[];
  handleBindWorkflow: () => void | Promise<void>;
  pluginOptions: Array<{ id: number; name: string; category?: string; status: number }>;
  selectedPluginIds: number[];
  handlePluginSelectionChange: (nextPluginIds: number[]) => void | Promise<void>;
  knowledgeBaseOptions: Array<{ id: number; name: string; type: number }>;
  selectedKnowledgeBaseIds: number[];
  handleKnowledgeSelectionChange: (nextKnowledgeBaseIds: number[]) => void;
  knowledgeBindings: AgentKnowledgeBindingInput[];
  setKnowledgeBindings: Dispatch<SetStateAction<AgentKnowledgeBindingInput[]>>;
  pluginBindings: AgentPluginBindingInput[];
  setPluginBindings: Dispatch<SetStateAction<AgentPluginBindingInput[]>>;
  pluginDetailMap: Record<number, { id: number; name: string; category?: string; apis: Array<{ id: number; name: string; requestSchemaJson: string; timeoutSeconds: number; isEnabled: boolean }> }>;
  variableOptions: Array<{ id: number; key: string; scopeId?: number }>;
  databaseOptions: Array<{ id: number; name: string; botId?: number }>;
  selectedDatabaseIds: number[];
  handleDatabaseSelectionChange: (nextDatabaseIds: number[]) => void;
  databaseBindings: AgentDatabaseBindingInput[];
  setDatabaseBindings: Dispatch<SetStateAction<AgentDatabaseBindingInput[]>>;
  persistedDatabaseIds: number[];
  databaseBindingActionId: number | null;
  handleBindDatabase: (binding: AgentDatabaseBindingInput) => void | Promise<void>;
  handleUnbindDatabase: (databaseId: number) => void | Promise<void>;
  selectedVariableIds: number[];
  handleVariableSelectionChange: (nextVariableIds: number[]) => void;
  variableBindings: AgentVariableBindingInput[];
  setVariableBindings: Dispatch<SetStateAction<AgentVariableBindingInput[]>>;
  enableMemory: boolean;
  setEnableMemory: (value: boolean) => void;
  enableShortTermMemory: boolean;
  setEnableShortTermMemory: (value: boolean) => void;
  enableLongTermMemory: boolean;
  setEnableLongTermMemory: (value: boolean) => void;
  longTermMemoryTopK: number | undefined;
  setLongTermMemoryTopK: (value: number | undefined) => void;
  selectedModel: ModelConfigItem | undefined;
  selectedWorkflow: WorkflowListItem | undefined;
}

/** 中间配置区：按左侧导航键只渲染对应表单区块。 */
export function AgentWorkbenchConfigTab(props: AgentWorkbenchConfigTabProps) {
  const {
    activeNavKey,
    detail,
    name,
    setName,
    description,
    setDescription,
    avatarUrl,
    setAvatarUrl,
    openingMessage,
    setOpeningMessage,
    presetQuestionsInput,
    setPresetQuestionsInput,
    promptSections,
    updatePromptSection,
    saving,
    resourceReady,
    modelConfigId,
    setModelConfigId,
    modelConfigs,
    workflowId,
    setWorkflowId,
    workflowOptions,
    handleBindWorkflow,
    pluginOptions,
    selectedPluginIds,
    handlePluginSelectionChange,
    knowledgeBaseOptions,
    selectedKnowledgeBaseIds,
    handleKnowledgeSelectionChange,
    knowledgeBindings,
    setKnowledgeBindings,
    pluginBindings,
    setPluginBindings,
    pluginDetailMap,
    variableOptions,
    databaseOptions,
    selectedDatabaseIds,
    handleDatabaseSelectionChange,
    databaseBindings,
    setDatabaseBindings,
    persistedDatabaseIds,
    databaseBindingActionId,
    handleBindDatabase,
    handleUnbindDatabase,
    selectedVariableIds,
    handleVariableSelectionChange,
    variableBindings,
    setVariableBindings,
    enableMemory,
    setEnableMemory,
    enableShortTermMemory,
    setEnableShortTermMemory,
    enableLongTermMemory,
    setEnableLongTermMemory,
    longTermMemoryTopK,
    setLongTermMemoryTopK,
    selectedModel,
    selectedWorkflow
  } = props;

  switch (activeNavKey) {
    case "basic":
      return (
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__section-head">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>智能体</Typography.Title>
              <Typography.Text type="tertiary">名称与展示信息将用于列表与对话页。</Typography.Text>
            </div>
            <Tag color="cyan">{detail?.status || "draft"}</Tag>
          </div>
          <div className="module-studio__stack">
            <Input value={name} onChange={setName} placeholder="角色名称" />
            <Input value={description} onChange={setDescription} placeholder="角色概述" />
            <Input value={avatarUrl} onChange={setAvatarUrl} placeholder="头像地址（可选）" data-testid="app-bot-ide-avatar-url" />
          </div>
        </section>
      );

    case "persona":
      return (
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__field">
            <span>角色（Persona）</span>
            <textarea
              value={promptSections.persona}
              onChange={event => updatePromptSection("persona", event.target.value)}
              rows={10}
              className="module-studio__textarea"
              disabled={saving}
            />
          </div>
        </section>
      );

    case "model":
      return (
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__coze-inspector-list">
            <div className="module-studio__coze-inspector-card">
              <span>模型</span>
              <Select
                value={modelConfigId}
                placeholder="选择模型配置"
                optionList={modelConfigs.map(item => ({
                  label: `${item.name} / ${item.defaultModel}`,
                  value: String(item.id)
                }))}
                onChange={value => setModelConfigId(typeof value === "string" ? value : undefined)}
                disabled={!resourceReady || saving}
                data-testid="app-bot-ide-model-config"
              />
            </div>

            <div className="module-studio__coze-inspector-card">
              <span>记忆开关</span>
              <div className="module-studio__model-switches">
                <div className="module-studio__switch-item">
                  <span>启用记忆</span>
                  <Switch checked={enableMemory} onChange={setEnableMemory} />
                </div>
                <div className="module-studio__switch-item">
                  <span>短期记忆</span>
                  <Switch checked={enableShortTermMemory} onChange={setEnableShortTermMemory} disabled={!enableMemory} />
                </div>
                <div className="module-studio__switch-item">
                  <span>长期记忆</span>
                  <Switch checked={enableLongTermMemory} onChange={setEnableLongTermMemory} disabled={!enableMemory} />
                </div>
              </div>
            </div>

            <div className="module-studio__coze-inspector-card">
              <span>长期记忆召回数量</span>
              <InputNumber value={longTermMemoryTopK} min={1} max={20} onNumberChange={value => setLongTermMemoryTopK(value ?? 3)} disabled={!enableMemory || !enableLongTermMemory} />
            </div>

            {MODEL_TAB_PROMPT_SECTIONS.map(section => (
              <div key={section.key} className="module-studio__field">
                <span>{section.title}</span>
                <textarea
                  value={promptSections[section.key]}
                  onChange={event => updatePromptSection(section.key, event.target.value)}
                  rows={4}
                  className="module-studio__textarea"
                  disabled={saving}
                />
              </div>
            ))}

            <div className="module-studio__coze-inspector-card">
              <span>当前绑定</span>
              <Descriptions
                data={[
                  { key: "agent", value: detail?.name || "-" },
                  { key: "model", value: selectedModel ? `${selectedModel.providerType} / ${selectedModel.defaultModel}` : "尚未绑定模型" },
                  { key: "workflow", value: selectedWorkflow?.name || detail?.defaultWorkflowName || "尚未绑定工作流" },
                  { key: "memory", value: enableMemory ? `${enableLongTermMemory ? "长期" : ""}${enableShortTermMemory ? "短期" : ""}` || "已启用" : "未启用" },
                  { key: "knowledge", value: `${knowledgeBindings.filter(item => item.isEnabled).length} 个知识库` },
                  { key: "tools", value: `${pluginBindings.flatMap(item => item.toolBindings ?? []).filter(item => item.isEnabled).length} 个工具` },
                  { key: "database", value: databaseBindings.find(item => item.isDefault)?.alias || databaseOptions.find(item => item.id === databaseBindings.find(candidate => candidate.isDefault)?.databaseId)?.name || "未设置默认库" },
                  { key: "variables", value: `${variableBindings.length} 个变量` }
                ]}
                size="small"
                align="left"
              />
            </div>
          </div>
        </section>
      );

    case "workflow":
      return (
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__coze-inspector-list">
            <div className="module-studio__coze-inspector-card">
              <span>工作流</span>
              <Select
                value={workflowId}
                placeholder="绑定默认工作流"
                optionList={workflowOptions.map(item => ({
                  label: `${item.name}${item.status === 1 ? " / 已发布" : " / 草稿"}`,
                  value: item.id
                }))}
                onChange={value => setWorkflowId(typeof value === "string" ? value : undefined)}
                disabled={!resourceReady || saving}
                data-testid="app-bot-ide-workflow-select"
              />
            </div>
            <Space wrap>
              <Button onClick={() => void handleBindWorkflow()} disabled={!resourceReady || !workflowId} data-testid="app-bot-ide-bind-workflow">
                绑定工作流
              </Button>
            </Space>
            <div className="module-studio__field">
              <span>工作流说明（Prompt 段落）</span>
              <textarea
                value={promptSections.workflow}
                onChange={event => updatePromptSection("workflow", event.target.value)}
                rows={8}
                className="module-studio__textarea"
                disabled={saving}
              />
            </div>
          </div>
        </section>
      );

    case "opening":
      return (
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__stack">
            <div className="module-studio__field">
              <span>开场白</span>
              <textarea
                value={openingMessage}
                onChange={event => {
                  setOpeningMessage(event.target.value);
                  updatePromptSection("opening", event.target.value);
                }}
                rows={4}
                className="module-studio__textarea"
                disabled={saving}
                data-testid="app-bot-ide-opening-message"
              />
            </div>
            <div className="module-studio__field">
              <span>预置问题（每行一条，最多 6 条）</span>
              <textarea
                value={presetQuestionsInput}
                onChange={event => setPresetQuestionsInput(event.target.value)}
                rows={5}
                className="module-studio__textarea"
                disabled={saving}
                data-testid="app-bot-ide-preset-questions"
              />
            </div>
          </div>
        </section>
      );

    case "plugins":
      return (
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__coze-inspector-list">
            <div className="module-studio__coze-inspector-card">
              <span>插件</span>
              <Select
                multiple
                value={selectedPluginIds}
                placeholder="选择插件能力"
                optionList={pluginOptions.map(item => ({
                  label: `${item.name}${item.category ? ` / ${item.category}` : ""}`,
                  value: item.id
                }))}
                onChange={value => void handlePluginSelectionChange(Array.isArray(value) ? value.map(item => Number(item)) : [])}
                disabled={!resourceReady || saving}
                data-testid="app-bot-ide-plugins"
              />
            </div>

            {pluginBindings.map(binding => {
              const pluginOption = pluginOptions.find(item => item.id === binding.pluginId);
              const pluginDetail = pluginDetailMap[binding.pluginId];
              return (
                <div key={`plugin-${binding.pluginId}`} className="module-studio__coze-inspector-card">
                  <div className="module-studio__card-head">
                    <strong>{pluginOption?.name || `Plugin ${binding.pluginId}`}</strong>
                    <Tag color={binding.isEnabled ? "green" : "grey"}>{binding.isEnabled ? "启用" : "停用"}</Tag>
                  </div>
                  {!pluginDetail ? <Typography.Text type="tertiary">插件详情加载中…</Typography.Text> : (
                    <div className="module-studio__stack">
                      {binding.toolBindings?.map(toolBinding => {
                        const apiDetail = pluginDetail.apis.find(apiItem => apiItem.id === toolBinding.apiId);
                        return (
                          <div key={`tool-${binding.pluginId}-${toolBinding.apiId}`} className="module-studio__coze-inspector-card">
                            <div className="module-studio__card-head">
                              <div>
                                <strong>{apiDetail?.name || `API ${toolBinding.apiId}`}</strong>
                                <Typography.Text type="tertiary">{pluginOption?.category || "Plugin API"}</Typography.Text>
                              </div>
                              <Switch
                                checked={toolBinding.isEnabled}
                                onChange={checked => setPluginBindings(current => current.map(item => item.pluginId === binding.pluginId ? {
                                  ...item,
                                  toolBindings: item.toolBindings?.map(tool => tool.apiId === toolBinding.apiId ? { ...tool, isEnabled: checked } : tool)
                                } : item))}
                              />
                            </div>
                            <div className="module-studio__form-grid">
                              <div className="module-studio__field">
                                <span>超时（秒）</span>
                                <InputNumber value={toolBinding.timeoutSeconds} min={1} max={300} onNumberChange={value => setPluginBindings(current => current.map(item => item.pluginId === binding.pluginId ? {
                                  ...item,
                                  toolBindings: item.toolBindings?.map(tool => tool.apiId === toolBinding.apiId ? { ...tool, timeoutSeconds: value ?? 30 } : tool)
                                } : item))} />
                              </div>
                              <div className="module-studio__field">
                                <span>失败策略</span>
                                <Select
                                  value={toolBinding.failurePolicy}
                                  optionList={[
                                    { label: "失败即终止", value: "fail" },
                                    { label: "跳过并继续", value: "skip" }
                                  ]}
                                  onChange={value => setPluginBindings(current => current.map(item => item.pluginId === binding.pluginId ? {
                                    ...item,
                                    toolBindings: item.toolBindings?.map(tool => tool.apiId === toolBinding.apiId ? { ...tool, failurePolicy: String(value) as "skip" | "fail" } : tool)
                                  } : item))}
                                />
                              </div>
                            </div>
                            {(toolBinding.parameterBindings ?? []).length > 0 ? (
                              <div className="module-studio__stack">
                                {(toolBinding.parameterBindings ?? []).map((parameter, index) => (
                                  <div key={`param-${toolBinding.apiId}-${parameter.parameterName}-${index}`} className="module-studio__form-grid">
                                    <div className="module-studio__field">
                                      <span>{parameter.parameterName}</span>
                                      <Select
                                        value={parameter.valueSource}
                                        optionList={[
                                          { label: "字面量", value: "literal" },
                                          { label: "变量", value: "variable" }
                                        ]}
                                        onChange={value => setPluginBindings(current => current.map(item => item.pluginId === binding.pluginId ? {
                                          ...item,
                                          toolBindings: item.toolBindings?.map(tool => tool.apiId === toolBinding.apiId ? {
                                            ...tool,
                                            parameterBindings: tool.parameterBindings.map(currentParameter => currentParameter.parameterName === parameter.parameterName ? {
                                              ...currentParameter,
                                              valueSource: String(value) as "literal" | "variable"
                                            } : currentParameter)
                                          } : tool)
                                        } : item))}
                                      />
                                    </div>
                                    {parameter.valueSource === "literal" ? (
                                      <div className="module-studio__field">
                                        <span>字面量值</span>
                                        <Input
                                          value={parameter.literalValue ?? ""}
                                          onChange={value => setPluginBindings(current => current.map(item => item.pluginId === binding.pluginId ? {
                                            ...item,
                                            toolBindings: item.toolBindings?.map(tool => tool.apiId === toolBinding.apiId ? {
                                              ...tool,
                                              parameterBindings: tool.parameterBindings.map(currentParameter => currentParameter.parameterName === parameter.parameterName ? {
                                                ...currentParameter,
                                                literalValue: value || undefined
                                              } : currentParameter)
                                            } : tool)
                                          } : item))}
                                        />
                                      </div>
                                    ) : (
                                      <div className="module-studio__field">
                                        <span>变量</span>
                                        <Select
                                          value={parameter.variableKey}
                                          optionList={variableOptions.map(item => ({ label: item.key, value: item.key }))}
                                          onChange={value => setPluginBindings(current => current.map(item => item.pluginId === binding.pluginId ? {
                                            ...item,
                                            toolBindings: item.toolBindings?.map(tool => tool.apiId === toolBinding.apiId ? {
                                              ...tool,
                                              parameterBindings: tool.parameterBindings.map(currentParameter => currentParameter.parameterName === parameter.parameterName ? {
                                                ...currentParameter,
                                                variableKey: String(value) || undefined
                                              } : currentParameter)
                                            } : tool)
                                          } : item))}
                                        />
                                      </div>
                                    )}
                                  </div>
                                ))}
                              </div>
                            ) : null}
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            })}

            <div className="module-studio__coze-inspector-card">
              <span>数据库</span>
              <Select
                multiple
                value={selectedDatabaseIds}
                placeholder="选择数据库"
                optionList={databaseOptions.map(item => ({
                  label: `${item.name}${item.botId ? " / 已绑定" : ""}`,
                  value: item.id
                }))}
                onChange={value => handleDatabaseSelectionChange(Array.isArray(value) ? value.map(item => Number(item)) : [])}
                disabled={!resourceReady || saving}
                data-testid="app-bot-ide-databases"
              />
            </div>

            {databaseBindings.map(binding => {
              const databaseOption = databaseOptions.find(item => item.id === binding.databaseId);
              const isPersisted = persistedDatabaseIds.includes(binding.databaseId);
              const isBindingBusy = databaseBindingActionId === binding.databaseId;
              return (
                <div key={`database-${binding.databaseId}`} className="module-studio__coze-inspector-card">
                  <div className="module-studio__card-head">
                    <strong>{databaseOption?.name || `DB ${binding.databaseId}`}</strong>
                    <div className="module-studio__inline-actions">
                      <Tag color={binding.isDefault ? "blue" : "grey"}>{binding.isDefault ? "默认库" : isPersisted ? "已绑定" : "未落库"}</Tag>
                      {binding.accessMode === "readwrite" ? <Tag color="orange">读写</Tag> : <Tag color="green">只读</Tag>}
                      {isPersisted ? (
                        <Button
                          size="small"
                          theme="borderless"
                          type="danger"
                          loading={isBindingBusy}
                          disabled={saving || !resourceReady}
                          onClick={() => void handleUnbindDatabase(binding.databaseId)}
                        >
                          解绑
                        </Button>
                      ) : (
                        <Button
                          size="small"
                          theme="borderless"
                          loading={isBindingBusy}
                          disabled={saving || !resourceReady}
                          onClick={() => void handleBindDatabase(binding)}
                        >
                          立即绑定
                        </Button>
                      )}
                    </div>
                  </div>
                  <div className="module-studio__form-grid">
                    <div className="module-studio__field">
                      <span>别名</span>
                      <Input
                        value={binding.alias ?? ""}
                        onChange={value => setDatabaseBindings(current => current.map(item => item.databaseId === binding.databaseId ? { ...item, alias: value || undefined } : item))}
                      />
                    </div>
                    <div className="module-studio__field">
                      <span>访问模式</span>
                      <Select
                        value={binding.accessMode}
                        optionList={[
                          { label: "只读", value: "readonly" },
                          { label: "读写", value: "readwrite" }
                        ]}
                        onChange={value => setDatabaseBindings(current => current.map(item => item.databaseId === binding.databaseId ? { ...item, accessMode: String(value) as "readonly" | "readwrite" } : item))}
                      />
                    </div>
                    <div className="module-studio__field">
                      <span>默认数据库</span>
                      <Switch
                        checked={binding.isDefault}
                        onChange={checked => setDatabaseBindings(current => current.map(item => ({
                          ...item,
                          isDefault: checked ? item.databaseId === binding.databaseId : item.databaseId === current.find(candidate => candidate.databaseId !== binding.databaseId)?.databaseId
                        })))}
                      />
                    </div>
                    <div className="module-studio__field module-studio__field--full">
                      <span>表白名单（逗号分隔）</span>
                      <Input
                        value={stringifyAllowlist(binding.tableAllowlist)}
                        onChange={value => setDatabaseBindings(current => current.map(item => item.databaseId === binding.databaseId ? {
                          ...item,
                          tableAllowlist: normalizeAllowlistText(value)
                        } : item))}
                        placeholder="例如：alerts, incidents, assets"
                      />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      );

    case "knowledge":
      return (
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__coze-inspector-list">
            <div className="module-studio__coze-inspector-card">
              <span>知识库</span>
              <Select
                multiple
                value={selectedKnowledgeBaseIds}
                placeholder="选择知识库"
                optionList={knowledgeBaseOptions.map(item => ({
                  label: `${item.name} / type:${item.type}`,
                  value: item.id
                }))}
                onChange={value => handleKnowledgeSelectionChange(Array.isArray(value) ? value.map(item => Number(item)) : [])}
                disabled={!resourceReady || saving}
                data-testid="app-bot-ide-knowledge-bases"
              />
            </div>

            {knowledgeBindings.map(binding => {
              const knowledgeOption = knowledgeBaseOptions.find(item => item.id === binding.knowledgeBaseId);
              return (
                <div key={`knowledge-${binding.knowledgeBaseId}`} className="module-studio__coze-inspector-card">
                  <div className="module-studio__card-head">
                    <strong>{knowledgeOption?.name || `KB ${binding.knowledgeBaseId}`}</strong>
                    <Switch
                      checked={binding.isEnabled}
                      onChange={checked => setKnowledgeBindings(current => current.map(item => item.knowledgeBaseId === binding.knowledgeBaseId ? { ...item, isEnabled: checked } : item))}
                    />
                  </div>
                  <div className="module-studio__form-grid">
                    <div className="module-studio__field">
                      <span>调用方式</span>
                      <Select
                        value={binding.invokeMode}
                        optionList={[
                          { label: "自动", value: "auto" },
                          { label: "手动", value: "manual" }
                        ]}
                        onChange={value => setKnowledgeBindings(current => current.map(item => item.knowledgeBaseId === binding.knowledgeBaseId ? { ...item, invokeMode: String(value) as "auto" | "manual" } : item))}
                      />
                    </div>
                    <div className="module-studio__field">
                      <span>TopK</span>
                      <InputNumber value={binding.topK} min={1} max={20} onNumberChange={value => setKnowledgeBindings(current => current.map(item => item.knowledgeBaseId === binding.knowledgeBaseId ? { ...item, topK: value ?? 5 } : item))} />
                    </div>
                    <div className="module-studio__field">
                      <span>阈值</span>
                      <InputNumber value={binding.scoreThreshold} min={0} max={1} step={0.1} onNumberChange={value => setKnowledgeBindings(current => current.map(item => item.knowledgeBaseId === binding.knowledgeBaseId ? { ...item, scoreThreshold: value ?? 0 } : item))} />
                    </div>
                    <div className="module-studio__field module-studio__field--full">
                      <span>内容类型</span>
                      <Space wrap>
                        {(["text", "table", "image"] as const).map(type => {
                          const active = binding.enabledContentTypes.includes(type);
                          return (
                            <Button
                              key={`${binding.knowledgeBaseId}-${type}`}
                              theme={active ? "solid" : "light"}
                              type={active ? "primary" : "tertiary"}
                              onClick={() => setKnowledgeBindings(current => current.map(item => {
                                if (item.knowledgeBaseId !== binding.knowledgeBaseId) {
                                  return item;
                                }
                                const nextTypes = active
                                  ? item.enabledContentTypes.filter(currentType => currentType !== type)
                                  : [...item.enabledContentTypes, type];
                                return { ...item, enabledContentTypes: nextTypes };
                              }))}
                            >
                              {type}
                            </Button>
                          );
                        })}
                      </Space>
                    </div>
                    <div className="module-studio__field module-studio__field--full">
                      <span>查询改写模板</span>
                      <textarea
                        rows={3}
                        className="module-studio__textarea"
                        value={binding.rewriteQueryTemplate ?? ""}
                        onChange={event => setKnowledgeBindings(current => current.map(item => item.knowledgeBaseId === binding.knowledgeBaseId ? { ...item, rewriteQueryTemplate: event.target.value || undefined } : item))}
                      />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      );

    case "variables":
      return (
        <section className="module-studio__coze-agent-panel">
          <div className="module-studio__coze-inspector-list">
            <div className="module-studio__coze-inspector-card">
              <span>变量</span>
              <Select
                multiple
                value={selectedVariableIds}
                placeholder="选择 Bot 变量"
                optionList={variableOptions.map(item => ({
                  label: item.key,
                  value: item.id
                }))}
                onChange={value => handleVariableSelectionChange(Array.isArray(value) ? value.map(item => Number(item)) : [])}
                disabled={!resourceReady || saving}
                data-testid="app-bot-ide-variables"
              />
            </div>

            {selectedVariableIds.length === 0 ? (
              <div className="module-studio__coze-inspector-card">
                <Typography.Text type="tertiary">当前 Bot 作用域还没有变量绑定，可先在工作流侧创建 Bot 变量后回来配置。</Typography.Text>
              </div>
            ) : null}

            {variableBindings.map(binding => {
              const variableOption = variableOptions.find(item => item.id === binding.variableId);
              return (
                <div key={`variable-${binding.variableId}`} className="module-studio__coze-inspector-card">
                  <div className="module-studio__card-head">
                    <strong>{variableOption?.key || `Var ${binding.variableId}`}</strong>
                    <Tag color={binding.isRequired ? "red" : "grey"}>{binding.isRequired ? "必填" : "可选"}</Tag>
                  </div>
                  <div className="module-studio__form-grid">
                    <div className="module-studio__field">
                      <span>别名</span>
                      <Input
                        value={binding.alias ?? ""}
                        onChange={value => setVariableBindings(current => current.map(item => item.variableId === binding.variableId ? { ...item, alias: value || undefined } : item))}
                      />
                    </div>
                    <div className="module-studio__field">
                      <span>必填</span>
                      <Switch
                        checked={binding.isRequired}
                        onChange={checked => setVariableBindings(current => current.map(item => item.variableId === binding.variableId ? { ...item, isRequired: checked } : item))}
                      />
                    </div>
                    <div className="module-studio__field module-studio__field--full">
                      <span>默认值覆盖</span>
                      <textarea
                        rows={3}
                        className="module-studio__textarea"
                        value={binding.defaultValueOverride ?? ""}
                        onChange={event => setVariableBindings(current => current.map(item => item.variableId === binding.variableId ? {
                          ...item,
                          defaultValueOverride: event.target.value || undefined
                        } : item))}
                      />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      );

    default: {
      const _never: never = activeNavKey;
      return _never;
    }
  }
}
