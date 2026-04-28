import { useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import {
  Banner,
  Button,
  Space,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import {
  composeAgentPromptSections,
  createDefaultDatabaseBinding,
  createDefaultKnowledgeBinding,
  createDefaultPluginBinding,
  createDefaultVariableBinding,
  EMPTY_AGENT_PROMPT_SECTIONS,
  formatDate,
  formatWorkflowResultMessage,
  parseAgentPromptSections,
  parsePluginParameterNames,
  parseResourceUsageSummary,
  parseTraceSummary,
  readPageSearchParam,
  replacePageSearchParams,
  toSecurityIncidentTaskCard
} from "./agent-ide-helpers";
import type { AgentPromptSections } from "./agent-ide-helpers";
import type {
  AgentDetail,
  AgentDatabaseBindingInput,
  AgentKnowledgeBindingInput,
  AgentPluginBindingInput,
  AgentVariableBindingInput,
  ChatMessageItem,
  ModelConfigItem,
  StudioAssistantPublication,
  StudioPageProps,
  WorkflowListItem,
  WorkbenchTrace
} from "../types";
import { AgentWorkbenchLayout } from "./agent-workbench-layout";
import { AgentConfigNav } from "./agent-config-nav";
import { AgentConfigPanel } from "./agent-config-panel";
import { AgentWorkbenchConfigTab } from "./agent-workbench-config-tab";
import { AgentDebugPanel } from "./agent-debug-panel";
import { AgentPublishModal } from "./agent-publish-modal";
import type { AgentConfigNavKey } from "./agent-workbench-types";

function Surface({
  title,
  subtitle,
  testId,
  toolbar,
  children
}: {
  title: string;
  subtitle: string;
  testId: string;
  toolbar?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="module-studio__page" data-testid={testId}>
      <div className="module-studio__header">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>{title}</Typography.Title>
          <Typography.Text type="tertiary">{subtitle}</Typography.Text>
        </div>
        {toolbar ? <div className="module-studio__toolbar">{toolbar}</div> : null}
      </div>
      <div className="module-studio__surface">{children}</div>
    </section>
  );
}
export function AgentWorkbench({
  api,
  botId,
  onOpenPublish
}: StudioPageProps & { botId: string; onOpenPublish?: () => void }) {
  const [detail, setDetail] = useState<AgentDetail | null>(null);
  const [publications, setPublications] = useState<StudioAssistantPublication[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [avatarUrl, setAvatarUrl] = useState("");
  const [openingMessage, setOpeningMessage] = useState("");
  const [presetQuestionsInput, setPresetQuestionsInput] = useState("");
  const [systemPrompt, setSystemPrompt] = useState("");
  const [promptSections, setPromptSections] = useState<AgentPromptSections>({ ...EMPTY_AGENT_PROMPT_SECTIONS });
  const [modelConfigId, setModelConfigId] = useState<string | undefined>(undefined);
  const [workflowId, setWorkflowId] = useState<string | undefined>(undefined);
  const [enableMemory, setEnableMemory] = useState(true);
  const [enableShortTermMemory, setEnableShortTermMemory] = useState(true);
  const [enableLongTermMemory, setEnableLongTermMemory] = useState(true);
  const [longTermMemoryTopK, setLongTermMemoryTopK] = useState<number | undefined>(3);
  const [modelConfigs, setModelConfigs] = useState<ModelConfigItem[]>([]);
  const [workflowOptions, setWorkflowOptions] = useState<WorkflowListItem[]>([]);
  const [pluginOptions, setPluginOptions] = useState<Array<{ id: number; name: string; category?: string; status: number }>>([]);
  const [knowledgeBaseOptions, setKnowledgeBaseOptions] = useState<Array<{ id: number; name: string; type: number }>>([]);
  const [databaseOptions, setDatabaseOptions] = useState<Array<{ id: number; name: string; botId?: number }>>([]);
  const [variableOptions, setVariableOptions] = useState<Array<{ id: number; key: string; scopeId?: number }>>([]);
  const [pluginDetailMap, setPluginDetailMap] = useState<Record<number, { id: number; name: string; category?: string; apis: Array<{ id: number; name: string; requestSchemaJson: string; timeoutSeconds: number; isEnabled: boolean }> }>>({});
  const [knowledgeBindings, setKnowledgeBindings] = useState<AgentKnowledgeBindingInput[]>([]);
  const [pluginBindings, setPluginBindings] = useState<AgentPluginBindingInput[]>([]);
  const [databaseBindings, setDatabaseBindings] = useState<AgentDatabaseBindingInput[]>([]);
  const [variableBindings, setVariableBindings] = useState<AgentVariableBindingInput[]>([]);
  const [selectedKnowledgeBaseIds, setSelectedKnowledgeBaseIds] = useState<number[]>([]);
  const [selectedPluginIds, setSelectedPluginIds] = useState<number[]>([]);
  const [selectedDatabaseIds, setSelectedDatabaseIds] = useState<number[]>([]);
  const [selectedVariableIds, setSelectedVariableIds] = useState<number[]>([]);
  const [conversationId, setConversationId] = useState<string>("");
  const [messages, setMessages] = useState<ChatMessageItem[]>([]);
  const [messageInput, setMessageInput] = useState("");
  const [workflowInput, setWorkflowInput] = useState("");
  const [sending, setSending] = useState(false);
  const [saving, setSaving] = useState(false);
  const [runningWorkflow, setRunningWorkflow] = useState(false);
  const [streamingAssistant, setStreamingAssistant] = useState("");
  const [thoughts, setThoughts] = useState<string[]>([]);
  const [lastTrace, setLastTrace] = useState<WorkbenchTrace | null>(null);
  const [publicationLoading, setPublicationLoading] = useState(false);
  const [workbenchLoading, setWorkbenchLoading] = useState(true);
  const [workbenchError, setWorkbenchError] = useState<string | null>(null);
  const [autosaveHint, setAutosaveHint] = useState("草稿已同步");
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const workbenchRequestIdRef = useRef(0);
  const conversationIdRef = useRef("");
  const ensureConversationPromiseRef = useRef<Promise<string> | null>(null);
  const persistedDraftSignatureRef = useRef("");
  const botHydratingRef = useRef(true);
  const autosaveTimerRef = useRef<number | null>(null);
  const readwriteConfirmedRef = useRef(false);
  const [activeNavKey, setActiveNavKey] = useState<AgentConfigNavKey>("basic");
  const [publishModalOpen, setPublishModalOpen] = useState(false);
  const [publishReleaseNote, setPublishReleaseNote] = useState("");
  const [publishSubmitting, setPublishSubmitting] = useState(false);
  const [databaseBindingActionId, setDatabaseBindingActionId] = useState<number | null>(null);

  function applyConversationId(nextConversationId?: string | null) {
    const normalized = nextConversationId?.trim() ?? "";
    conversationIdRef.current = normalized;
    setConversationId(normalized);
    return normalized;
  }

  function buildAgentDraftRequest() {
    const nextSystemPrompt = composeAgentPromptSections(promptSections);
    const presetQuestions = presetQuestionsInput
      .split(/\r?\n/)
      .map(item => item.trim())
      .filter(Boolean)
      .slice(0, 6);
    const normalizedKnowledgeBindings = knowledgeBindings.map(binding => ({
      ...binding,
      enabledContentTypes: Array.from(new Set(binding.enabledContentTypes))
    }));
    const normalizedPluginBindings = pluginBindings.map((binding, index) => ({
      ...binding,
      sortOrder: index,
      toolConfigJson: JSON.stringify({
        toolBindings: (binding.toolBindings ?? []).map(tool => ({
          apiId: tool.apiId,
          isEnabled: tool.isEnabled,
          timeoutSeconds: tool.timeoutSeconds,
          failurePolicy: tool.failurePolicy,
          parameterBindings: tool.parameterBindings
        }))
      })
    }));
    const normalizedDatabaseBindings = databaseBindings.map((binding, index) => ({
      ...binding,
      alias: binding.alias?.trim() || undefined,
      accessMode: binding.accessMode,
      tableAllowlist: Array.from(new Set(binding.tableAllowlist.map(item => item.trim()).filter(Boolean))),
      isDefault: binding.isDefault || (index === 0 && !databaseBindings.some(item => item.isDefault))
    }));
    const normalizedVariableBindings = variableBindings.map(binding => ({
      ...binding,
      alias: binding.alias?.trim() || undefined,
      defaultValueOverride: binding.defaultValueOverride?.trim() || undefined
    }));

    return {
      request: {
        name: name.trim(),
        description: description.trim() || undefined,
        avatarUrl: avatarUrl.trim() || undefined,
        systemPrompt: nextSystemPrompt || undefined,
        personaMarkdown: promptSections.persona.trim() || undefined,
        goals: promptSections.goals.trim() || undefined,
        replyLogic: promptSections.skills.trim() || undefined,
        outputFormat: promptSections.outputFormat.trim() || undefined,
        constraints: promptSections.constraints.trim() || undefined,
        openingMessage: openingMessage.trim() || promptSections.opening.trim() || undefined,
        presetQuestions: presetQuestions.length > 0 ? presetQuestions : undefined,
        knowledgeBindings: normalizedKnowledgeBindings,
        knowledgeBaseIds: normalizedKnowledgeBindings.map(item => item.knowledgeBaseId),
        pluginBindings: normalizedPluginBindings,
        databaseBindings: normalizedDatabaseBindings,
        databaseBindingIds: normalizedDatabaseBindings.map(item => item.databaseId),
        variableBindings: normalizedVariableBindings,
        variableBindingIds: normalizedVariableBindings.map(item => item.variableId),
        modelConfigId,
        modelName: selectedModel?.defaultModel,
        defaultWorkflowId: workflowId,
        defaultWorkflowName: selectedWorkflow?.name,
        enableMemory,
        enableShortTermMemory,
        enableLongTermMemory,
        longTermMemoryTopK
      },
      systemPrompt: nextSystemPrompt,
      presetQuestions
    } as const;
  }

  async function loadWorkbench(nextConversationId?: string) {
    const requestId = ++workbenchRequestIdRef.current;
    botHydratingRef.current = true;
    setWorkbenchLoading(true);
    setPublicationLoading(true);
    setWorkbenchError(null);

    try {
      const [nextDetail, nextPublications, modelResult, workflows, plugins, knowledgeBases, databases, variables] = await Promise.all([
        api.getAgent(botId),
        api.getAgentPublications(botId),
        api.listModelConfigs(),
        api.listWorkflows({ status: "all" }),
        api.listPlugins(),
        api.listKnowledgeBases(),
        api.listDatabases(),
        api.listBotVariables(botId)
      ]);

      if (requestId !== workbenchRequestIdRef.current) {
        return;
      }

      setDetail(nextDetail);
      setPublications(nextPublications);
      setName(nextDetail.name);
      setDescription(nextDetail.description || "");
      setAvatarUrl(nextDetail.avatarUrl || "");
      setOpeningMessage(nextDetail.openingMessage || "");
      setPresetQuestionsInput((nextDetail.presetQuestions ?? []).join("\n"));
      setSystemPrompt(nextDetail.systemPrompt || "");
      const parsedPromptSections = parseAgentPromptSections(nextDetail.systemPrompt || "");
      setPromptSections({
        persona: nextDetail.personaMarkdown || parsedPromptSections.persona,
        goals: nextDetail.goals || parsedPromptSections.goals,
        skills: nextDetail.replyLogic || parsedPromptSections.skills,
        workflow: parsedPromptSections.workflow,
        outputFormat: nextDetail.outputFormat || parsedPromptSections.outputFormat,
        constraints: nextDetail.constraints || parsedPromptSections.constraints,
        opening: nextDetail.openingMessage || parsedPromptSections.opening
      });
      setModelConfigId(nextDetail.modelConfigId);
      setWorkflowId(nextDetail.defaultWorkflowId);
      setEnableMemory(nextDetail.enableMemory ?? true);
      setEnableShortTermMemory(nextDetail.enableShortTermMemory ?? true);
      setEnableLongTermMemory(nextDetail.enableLongTermMemory ?? true);
      setLongTermMemoryTopK(nextDetail.longTermMemoryTopK ?? 3);
      setModelConfigs(
        [...modelResult.items].sort((left, right) =>
          String(right.createdAt ?? "").localeCompare(String(left.createdAt ?? ""))
        )
      );
      setWorkflowOptions(
        [...workflows].sort((left, right) =>
          String(right.updatedAt ?? "").localeCompare(String(left.updatedAt ?? ""))
        )
      );
      setPluginOptions(plugins);
      setKnowledgeBaseOptions(knowledgeBases);
      setDatabaseOptions(databases);
      setVariableOptions(variables);
      const nextKnowledgeBindings = nextDetail.knowledgeBindings && nextDetail.knowledgeBindings.length > 0
        ? nextDetail.knowledgeBindings
        : (nextDetail.knowledgeBaseIds ?? []).map(id => createDefaultKnowledgeBinding(id));
      const nextPluginBindings = nextDetail.pluginBindings && nextDetail.pluginBindings.length > 0
        ? nextDetail.pluginBindings
        : [];
      const nextDatabaseBindings = nextDetail.databaseBindings && nextDetail.databaseBindings.length > 0
        ? nextDetail.databaseBindings
        : (nextDetail.databaseBindingIds ?? []).map((id, index) => createDefaultDatabaseBinding(id, index === 0));
      const nextVariableBindings = nextDetail.variableBindings && nextDetail.variableBindings.length > 0
        ? nextDetail.variableBindings
        : (nextDetail.variableBindingIds ?? []).map(id => createDefaultVariableBinding(id));
      setKnowledgeBindings(nextKnowledgeBindings);
      setPluginBindings(nextPluginBindings);
      setDatabaseBindings(nextDatabaseBindings);
      setVariableBindings(nextVariableBindings);
      setSelectedKnowledgeBaseIds(nextKnowledgeBindings.map(item => item.knowledgeBaseId));
      setSelectedPluginIds(nextPluginBindings.filter(item => item.isEnabled).map(item => item.pluginId));
      setSelectedDatabaseIds(nextDatabaseBindings.map(item => item.databaseId));
      setSelectedVariableIds(nextVariableBindings.map(item => item.variableId));
      readwriteConfirmedRef.current = nextDatabaseBindings.some(item => item.accessMode === "readwrite");

      const pluginIds = nextPluginBindings.map(item => item.pluginId).filter((value, index, array) => array.indexOf(value) === index);
      if (pluginIds.length > 0) {
        const details = await Promise.all(pluginIds.map(async pluginId => {
          const detail = await api.getPluginDetail(pluginId);
          return [pluginId, detail] as const;
        }));
        if (requestId !== workbenchRequestIdRef.current) {
          return;
        }
        setPluginDetailMap(Object.fromEntries(details));
      } else {
        setPluginDetailMap({});
      }

      const conversationResult = await api.listConversations(botId);
      if (requestId !== workbenchRequestIdRef.current) {
        return;
      }

      const activeConversationId = applyConversationId(
        nextConversationId || readPageSearchParam("botConversationId") || conversationIdRef.current || conversationResult.items[0]?.id || ""
      );

      const persistedRequest = {
        name: nextDetail.name.trim(),
        description: nextDetail.description?.trim() || undefined,
        avatarUrl: nextDetail.avatarUrl?.trim() || undefined,
        systemPrompt: nextDetail.systemPrompt || undefined,
        personaMarkdown: (nextDetail.personaMarkdown || parsedPromptSections.persona).trim() || undefined,
        goals: (nextDetail.goals || parsedPromptSections.goals).trim() || undefined,
        replyLogic: (nextDetail.replyLogic || parsedPromptSections.skills).trim() || undefined,
        outputFormat: (nextDetail.outputFormat || parsedPromptSections.outputFormat).trim() || undefined,
        constraints: (nextDetail.constraints || parsedPromptSections.constraints).trim() || undefined,
        openingMessage: (nextDetail.openingMessage || parsedPromptSections.opening).trim() || undefined,
        presetQuestions: (nextDetail.presetQuestions ?? []).map(item => item.trim()).filter(Boolean).slice(0, 6),
        knowledgeBindings: nextKnowledgeBindings,
        knowledgeBaseIds: nextKnowledgeBindings.map(item => item.knowledgeBaseId),
        pluginBindings: nextPluginBindings.map((binding, index) => ({
          ...binding,
          sortOrder: index,
          toolConfigJson: JSON.stringify({
            toolBindings: (binding.toolBindings ?? []).map(tool => ({
              apiId: tool.apiId,
              isEnabled: tool.isEnabled,
              timeoutSeconds: tool.timeoutSeconds,
              failurePolicy: tool.failurePolicy,
              parameterBindings: tool.parameterBindings
            }))
          })
        })),
        databaseBindings: nextDatabaseBindings,
        databaseBindingIds: nextDatabaseBindings.map(item => item.databaseId),
        variableBindings: nextVariableBindings,
        variableBindingIds: nextVariableBindings.map(item => item.variableId),
        modelConfigId: nextDetail.modelConfigId,
        modelName: modelResult.items.find(item => String(item.id) === String(nextDetail.modelConfigId ?? ""))?.defaultModel ?? nextDetail.modelName,
        defaultWorkflowId: nextDetail.defaultWorkflowId,
        defaultWorkflowName: nextDetail.defaultWorkflowName,
        enableMemory: nextDetail.enableMemory ?? true,
        enableShortTermMemory: nextDetail.enableShortTermMemory ?? true,
        enableLongTermMemory: nextDetail.enableLongTermMemory ?? true,
        longTermMemoryTopK: nextDetail.longTermMemoryTopK ?? 3
      };
      persistedDraftSignatureRef.current = JSON.stringify(persistedRequest);
      setHasUnsavedChanges(false);
      setAutosaveHint("草稿已同步");

      if (!activeConversationId) {
        setMessages([]);
        setLastTrace(null);
        return;
      }

      const nextMessages = await api.getMessages(activeConversationId);
      if (requestId !== workbenchRequestIdRef.current) {
        return;
      }

      setMessages(nextMessages);

      const traceMessage = [...nextMessages].reverse().find(message => parseTraceSummary(message.metadata));
      setLastTrace(traceMessage ? parseTraceSummary(traceMessage.metadata) : null);
    } catch (error) {
      if (requestId !== workbenchRequestIdRef.current) {
        return;
      }

      const message = error instanceof Error ? error.message : "加载 Agent 工作台失败。";
      setWorkbenchError(message);
      setMessages([]);
      setLastTrace(null);
      setPublications([]);
    } finally {
      if (requestId === workbenchRequestIdRef.current) {
        setWorkbenchLoading(false);
        setPublicationLoading(false);
        botHydratingRef.current = false;
      }
    }
  }

  useEffect(() => {
    void loadWorkbench();
  }, [api, botId]);

  const selectedModel = useMemo(
    () => modelConfigs.find(item => String(item.id) === String(modelConfigId ?? "")),
    [modelConfigId, modelConfigs]
  );
  const selectedWorkflow = useMemo(
    () => workflowOptions.find(item => item.id === workflowId),
    [workflowId, workflowOptions]
  );
  const persistedDatabaseIds = useMemo(
    () => new Set([
      ...(detail?.databaseBindings ?? []).map(item => item.databaseId),
      ...(detail?.databaseBindingIds ?? [])
    ]),
    [detail]
  );
  const canChat = Boolean(selectedModel && selectedModel.isEnabled);
  const modelCatalogEmpty = modelConfigs.length === 0;
  const modelNotSelected = !modelCatalogEmpty && (!modelConfigId || !selectedModel);
  const resourceReady = !workbenchLoading && !workbenchError;
  const lastResourceUsage = useMemo(
    () => [...messages].reverse().map(message => parseResourceUsageSummary(message.metadata)).find(Boolean) ?? null,
    [messages]
  );
  const draftSignature = useMemo(() => JSON.stringify(buildAgentDraftRequest().request), [
    avatarUrl,
    databaseBindings,
    description,
    enableLongTermMemory,
    enableMemory,
    enableShortTermMemory,
    knowledgeBindings,
    longTermMemoryTopK,
    modelConfigId,
    name,
    openingMessage,
    pluginBindings,
    presetQuestionsInput,
    promptSections,
    selectedModel?.defaultModel,
    selectedWorkflow?.name,
    variableBindings,
    workflowId
  ]);

  useEffect(() => {
    if (botHydratingRef.current) {
      return;
    }

    const changed = draftSignature !== persistedDraftSignatureRef.current;
    setHasUnsavedChanges(changed);
    if (!changed) {
      setAutosaveHint("草稿已同步");
      if (autosaveTimerRef.current) {
        window.clearTimeout(autosaveTimerRef.current);
        autosaveTimerRef.current = null;
      }
      return;
    }

    setAutosaveHint("存在未保存变更");
    if (autosaveTimerRef.current) {
      window.clearTimeout(autosaveTimerRef.current);
    }

    if (saving || sending || runningWorkflow || workbenchLoading) {
      return;
    }

    autosaveTimerRef.current = window.setTimeout(() => {
      void handleSave({ silent: true, source: "autosave" });
    }, 1800);

    return () => {
      if (autosaveTimerRef.current) {
        window.clearTimeout(autosaveTimerRef.current);
        autosaveTimerRef.current = null;
      }
    };
  }, [draftSignature, runningWorkflow, saving, sending, workbenchLoading]);

  useEffect(() => {
    replacePageSearchParams(params => {
      if (conversationId) {
        params.set("botConversationId", conversationId);
      } else {
        params.delete("botConversationId");
      }
    });
  }, [conversationId]);

  useEffect(() => {
    if (!databaseBindings.some(item => item.accessMode === "readwrite")) {
      readwriteConfirmedRef.current = false;
    }
  }, [databaseBindings]);

  async function ensureConversation(): Promise<string> {
    if (conversationIdRef.current) {
      return conversationIdRef.current;
    }

    if (ensureConversationPromiseRef.current) {
      return ensureConversationPromiseRef.current;
    }

    ensureConversationPromiseRef.current = api
      .createConversation(botId, `${name || "Agent"} 调试会话`)
      .then(createdConversationId => applyConversationId(createdConversationId))
      .finally(() => {
        ensureConversationPromiseRef.current = null;
      });

    return ensureConversationPromiseRef.current;
  }

  async function handlePluginSelectionChange(nextPluginIds: number[]) {
    setSelectedPluginIds(nextPluginIds);
    setPluginBindings(current => {
      const next = nextPluginIds.map((pluginId, index) => {
        const existing = current.find(item => item.pluginId === pluginId);
        return existing ? { ...existing, sortOrder: index } : { ...createDefaultPluginBinding(pluginId), sortOrder: index };
      });
      return next;
    });

    const missingPluginIds = nextPluginIds.filter(pluginId => !pluginDetailMap[pluginId]);
    if (missingPluginIds.length === 0) {
      return;
    }

    try {
      const details = await Promise.all(missingPluginIds.map(async pluginId => {
        const detail = await api.getPluginDetail(pluginId);
        return [pluginId, detail] as const;
      }));
      setPluginDetailMap(current => ({
        ...current,
        ...Object.fromEntries(details)
      }));
      setPluginBindings(current => current.map(binding => {
        if (!missingPluginIds.includes(binding.pluginId) || (binding.toolBindings && binding.toolBindings.length > 0)) {
          return binding;
        }

        const pluginDetail = Object.fromEntries(details)[binding.pluginId];
        const toolBindings = pluginDetail.apis.map(apiItem => ({
          apiId: apiItem.id,
          isEnabled: apiItem.isEnabled,
          timeoutSeconds: apiItem.timeoutSeconds || 30,
          failurePolicy: "fail" as const,
          parameterBindings: parsePluginParameterNames(apiItem.requestSchemaJson).map(parameter => ({
            parameterName: parameter.name,
            valueSource: "literal" as const,
            literalValue: "",
            variableKey: undefined
          }))
        }));

        return {
          ...binding,
          toolBindings,
          toolConfigJson: JSON.stringify({ toolBindings })
        };
      }));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载插件详情失败。");
    }
  }

  function handleKnowledgeSelectionChange(nextKnowledgeBaseIds: number[]) {
    setSelectedKnowledgeBaseIds(nextKnowledgeBaseIds);
    setKnowledgeBindings(current => nextKnowledgeBaseIds.map(id => current.find(item => item.knowledgeBaseId === id) ?? createDefaultKnowledgeBinding(id)));
  }

  function handleDatabaseSelectionChange(nextDatabaseIds: number[]) {
    setSelectedDatabaseIds(nextDatabaseIds);
    setDatabaseBindings(current => {
      const retained = current.filter(item => nextDatabaseIds.includes(item.databaseId));
      const defaultDatabaseId = retained.find(item => item.isDefault)?.databaseId ?? nextDatabaseIds[0];
      return nextDatabaseIds.map((id, index) => {
        const existing = retained.find(item => item.databaseId === id);
        if (existing) {
          return {
            ...existing,
            isDefault: id === defaultDatabaseId
          };
        }

        return createDefaultDatabaseBinding(id, id === defaultDatabaseId || (!defaultDatabaseId && index === 0));
      });
    });
  }

  function syncPersistedDatabaseBindings(nextPersistedBindings: AgentDatabaseBindingInput[]) {
    const nextPersistedIds = nextPersistedBindings.map(item => item.databaseId);
    setDetail(current => current ? {
      ...current,
      databaseBindings: nextPersistedBindings,
      databaseBindingIds: nextPersistedIds
    } : current);

    try {
      const parsed = persistedDraftSignatureRef.current ? JSON.parse(persistedDraftSignatureRef.current) as Record<string, unknown> : {};
      persistedDraftSignatureRef.current = JSON.stringify({
        ...parsed,
        databaseBindings: nextPersistedBindings,
        databaseBindingIds: nextPersistedIds
      });
    } catch {
      // persisted signature is always JSON from the current draft request; ignore rare parse failures.
    }
  }

  function mergeDatabaseBindingsAfterPersist(
    currentBindings: AgentDatabaseBindingInput[],
    nextPersistedBindings: AgentDatabaseBindingInput[],
    previousPersistedIds: Set<number>,
    removedDatabaseId?: number
  ) {
    const persistedMap = new Map(nextPersistedBindings.map(item => [item.databaseId, item] as const));
    const seen = new Set<number>();
    const merged: AgentDatabaseBindingInput[] = [];

    currentBindings.forEach(binding => {
      if (removedDatabaseId === binding.databaseId) {
        return;
      }

      const persistedBinding = persistedMap.get(binding.databaseId);
      if (persistedBinding) {
        merged.push({
          ...binding,
          isDefault: persistedBinding.isDefault
        });
        seen.add(binding.databaseId);
        return;
      }

      if (!previousPersistedIds.has(binding.databaseId)) {
        merged.push(binding);
        seen.add(binding.databaseId);
      }
    });

    nextPersistedBindings.forEach(binding => {
      if (!seen.has(binding.databaseId)) {
        merged.push(binding);
      }
    });

    return merged;
  }

  async function handleBindDatabase(binding: AgentDatabaseBindingInput) {
    if (!api.bindAgentDatabase) {
      Toast.warning("当前环境未提供数据库绑定接口。");
      return;
    }

    if (binding.accessMode === "readwrite" && !readwriteConfirmedRef.current) {
      const confirmed = window.confirm("当前数据库绑定包含读写权限，继续绑定将允许智能体执行写入动作。是否继续？");
      if (!confirmed) {
        return;
      }
      readwriteConfirmedRef.current = true;
    }

    const previousPersistedIds = new Set(persistedDatabaseIds);
    const databaseName = databaseOptions.find(item => item.id === binding.databaseId)?.name || `DB ${binding.databaseId}`;
    setDatabaseBindingActionId(binding.databaseId);
    try {
      const nextPersistedBindings = await api.bindAgentDatabase(botId, binding);
      syncPersistedDatabaseBindings(nextPersistedBindings);
      setSelectedDatabaseIds(current => current.includes(binding.databaseId) ? current : [...current, binding.databaseId]);
      setDatabaseBindings(current => mergeDatabaseBindingsAfterPersist(current, nextPersistedBindings, previousPersistedIds));
      Toast.success(`已绑定数据库：${databaseName}`);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "绑定数据库失败。");
    } finally {
      setDatabaseBindingActionId(null);
    }
  }

  async function handleUnbindDatabase(databaseId: number) {
    if (!api.unbindAgentDatabase) {
      Toast.warning("当前环境未提供数据库解绑接口。");
      return;
    }

    const previousPersistedIds = new Set(persistedDatabaseIds);
    const databaseName = databaseOptions.find(item => item.id === databaseId)?.name || `DB ${databaseId}`;
    setDatabaseBindingActionId(databaseId);
    try {
      const nextPersistedBindings = await api.unbindAgentDatabase(botId, databaseId);
      syncPersistedDatabaseBindings(nextPersistedBindings);
      setSelectedDatabaseIds(current => current.filter(item => item !== databaseId));
      setDatabaseBindings(current => mergeDatabaseBindingsAfterPersist(current, nextPersistedBindings, previousPersistedIds, databaseId));
      Toast.success(`已解绑数据库：${databaseName}`);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "解绑数据库失败。");
    } finally {
      setDatabaseBindingActionId(null);
    }
  }

  function handleVariableSelectionChange(nextVariableIds: number[]) {
    setSelectedVariableIds(nextVariableIds);
    setVariableBindings(current => nextVariableIds.map(id => current.find(item => item.variableId === id) ?? createDefaultVariableBinding(id)));
  }

  async function handleSave(options?: { silent?: boolean; source?: "manual" | "autosave" }) {
    if (!name.trim()) {
      if (!options?.silent) {
        Toast.warning("请先填写 Agent 名称。");
      }
      return false;
    }

    if (pluginBindings.some(binding => binding.toolBindings?.some(tool => tool.isEnabled)) && !selectedModel?.enableTools) {
      if (!options?.silent) {
        Toast.warning("当前模型未启用工具调用能力，请先切换或配置支持工具调用的模型。");
      }
      return false;
    }

    if (knowledgeBindings.some(binding => binding.isEnabled && binding.invokeMode === "auto") && !selectedModel?.supportsEmbedding) {
      if (!options?.silent) {
        Toast.warning("当前模型未启用知识检索相关能力，请先切换支持检索的模型或改为手动调用。");
      }
      return false;
    }

    const { request, systemPrompt: nextSystemPrompt, presetQuestions } = buildAgentDraftRequest();
    if (request.databaseBindings?.some(binding => binding.accessMode === "readwrite") && !readwriteConfirmedRef.current) {
      const confirmed = window.confirm("当前数据库绑定包含读写权限，继续保存将允许智能体执行写入动作。是否继续？");
      if (!confirmed) {
        return false;
      }
      readwriteConfirmedRef.current = true;
    }

    setSaving(true);
    setAutosaveHint(options?.source === "autosave" ? "正在自动保存…" : "正在保存…");
    try {
      await api.updateAgent(botId, request);
      persistedDraftSignatureRef.current = JSON.stringify(request);
      setHasUnsavedChanges(false);
      setDetail(current => current ? {
        ...current,
        name: request.name,
        description: request.description,
        avatarUrl: request.avatarUrl,
        systemPrompt: nextSystemPrompt || undefined,
        personaMarkdown: request.personaMarkdown,
        goals: request.goals,
        replyLogic: request.replyLogic,
        outputFormat: request.outputFormat,
        constraints: request.constraints,
        openingMessage: request.openingMessage,
        presetQuestions: presetQuestions.length > 0 ? presetQuestions : undefined,
        knowledgeBindings: request.knowledgeBindings,
        knowledgeBaseIds: request.knowledgeBaseIds,
        pluginBindings: request.pluginBindings,
        databaseBindings: request.databaseBindings,
        databaseBindingIds: request.databaseBindingIds,
        variableBindings: request.variableBindings,
        variableBindingIds: request.variableBindingIds,
        modelConfigId: request.modelConfigId,
        modelName: request.modelName,
        defaultWorkflowId: request.defaultWorkflowId,
        defaultWorkflowName: request.defaultWorkflowName,
        enableMemory: request.enableMemory,
        enableShortTermMemory: request.enableShortTermMemory,
        enableLongTermMemory: request.enableLongTermMemory,
        longTermMemoryTopK: request.longTermMemoryTopK
      } : current);
      setSystemPrompt(nextSystemPrompt);
      setAutosaveHint(options?.source === "autosave" ? "草稿已自动保存" : "草稿已保存");
      if (!options?.silent) {
        Toast.success("Agent 配置已保存。");
      }
      return true;
    } catch (error) {
      setAutosaveHint(options?.source === "autosave" ? "自动保存失败" : "保存失败");
      Toast.error(error instanceof Error ? error.message : options?.source === "autosave" ? "自动保存 Agent 草稿失败。" : "保存 Agent 配置失败。");
      return false;
    } finally {
      setSaving(false);
    }
  }

  async function handleClearConversationContext() {
    if (!conversationId) {
      Toast.warning("当前还没有可清理的会话。");
      return;
    }

    try {
      await api.clearConversationContext(conversationId);
      await loadWorkbench(conversationId);
      Toast.success("会话上下文已清空。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "清空上下文失败。");
    }
  }

  async function handleClearConversationHistory() {
    if (!conversationId) {
      Toast.warning("当前还没有可清理的会话。");
      return;
    }

    try {
      await api.clearConversationHistory(conversationId);
      await loadWorkbench(conversationId);
      Toast.success("会话历史已清空。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "清空历史失败。");
    }
  }

  async function handleDeleteConversation() {
    if (!conversationId) {
      Toast.warning("当前还没有可删除的会话。");
      return;
    }

    try {
      await api.deleteConversation(conversationId);
      applyConversationId("");
      await loadWorkbench();
      Toast.success("调试会话已删除。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "删除会话失败。");
    }
  }

  async function handleBindWorkflow() {
    if (!workflowId) {
      Toast.warning("请先选择一个工作流。");
      return;
    }

    try {
      const binding = await api.bindAgentWorkflow(botId, workflowId);
      setWorkflowId(binding.workflowId || workflowId);
      await loadWorkbench(conversationIdRef.current || undefined);
      Toast.success(`已绑定工作流：${binding.workflowName || selectedWorkflow?.name || workflowId}`);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "绑定工作流失败。");
    }
  }

  function openPublishModal() {
    setPublishReleaseNote("");
    setPublishModalOpen(true);
  }

  async function submitPublish() {
    setPublishSubmitting(true);
    try {
      const result = await api.publishAgent(botId, publishReleaseNote.trim() || undefined);
      setDetail(current => current ? {
        ...current,
        status: "Published"
      } : current);
      setPublications(current => [
        {
          id: result.publicationId,
          agentId: result.agentId,
          version: result.version,
          embedToken: result.embedToken,
          embedTokenExpiresAt: result.embedTokenExpiresAt,
          isActive: true,
          publishedByUserId: "",
          createdAt: new Date().toISOString()
        },
        ...current.map(item => ({ ...item, isActive: false }))
      ]);
      Toast.success(`智能体已发布为 v${result.version}`);
      setPublishModalOpen(false);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "发布智能体失败。");
    } finally {
      setPublishSubmitting(false);
    }
  }

  async function handleRegenerateEmbedToken() {
    try {
      const result = await api.regenerateAgentEmbedToken(botId);
      setPublications(current => current.map(item =>
        item.version === result.version
          ? {
              ...item,
              embedToken: result.embedToken,
              embedTokenExpiresAt: result.embedTokenExpiresAt,
              isActive: true
            }
          : item
      ));
      Toast.success("嵌入令牌已刷新。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "刷新嵌入令牌失败。");
    }
  }

  async function handleSendMessage() {
    if (!canChat) {
      Toast.warning("请先绑定并启用可用模型。");
      return;
    }

    if (!messageInput.trim()) {
      Toast.warning("请先输入消息。");
      return;
    }

    setSending(true);
    setThoughts([]);
    setStreamingAssistant("");

    try {
      const currentConversationId = await ensureConversation();
      let finalContent = "";
      for await (const chunk of api.sendAgentMessage(botId, {
        conversationId: currentConversationId,
        message: messageInput.trim(),
        enableRag: true
      })) {
        if (chunk.type === "thought") {
          setThoughts(current => [...current, chunk.content]);
          continue;
        }

        finalContent += chunk.content;
        setStreamingAssistant(finalContent);
      }

      setMessageInput("");
      setWorkflowInput(current => current || finalContent || messageInput.trim());
      await loadWorkbench(currentConversationId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "发送消息失败。");
    } finally {
      setStreamingAssistant("");
      setSending(false);
    }
  }

  async function handleRunWorkflow() {
    if (!workflowId) {
      Toast.warning("请先绑定默认工作流。");
      return;
    }

    const incident = workflowInput.trim() || messageInput.trim();
    if (!incident) {
      Toast.warning("请先输入安全事件描述。");
      return;
    }

    setRunningWorkflow(true);
    try {
      const currentConversationId = await ensureConversation();
      const result = await api.runWorkflowTask(workflowId, incident);
      const card = toSecurityIncidentTaskCard(result.execution.outputsJson);
      const content = card
        ? formatWorkflowResultMessage(card)
        : (result.execution.outputsJson || "工作流已完成，但未返回结构化结果。");
      const metadata = JSON.stringify({
        kind: "workflow-task",
        workflowId,
        workflowName: selectedWorkflow?.name,
        trace: result.trace,
        execution: result.execution
      });

      await api.appendConversationMessage(currentConversationId, {
        role: "tool",
        content,
        metadata
      });

      setLastTrace(result.trace ?? null);
      await loadWorkbench(currentConversationId);
      Toast.success("安全事件处置任务已完成。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "执行工作流任务失败。");
    } finally {
      setRunningWorkflow(false);
    }
  }

  function updatePromptSection(key: keyof AgentPromptSections, value: string) {
    setPromptSections(current => ({ ...current, [key]: value }));
  }

  return (
    <Surface title="智能体编排" subtitle="角色设定、记忆开关、工作流绑定和实时调试集中在一个 Coze 风格界面里。" testId="app-bot-ide-page">
      {workbenchLoading ? (
        <Banner type="info" bordered={false} fullMode={false} title="正在加载工作台资源" description="正在同步模型配置、工作流列表和最近会话，请稍候。" />
      ) : null}
      {workbenchError ? (
        <Banner type="danger" bordered={false} fullMode={false} title="工作台资源加载失败" description={workbenchError} />
      ) : null}
      <AgentWorkbenchLayout
        nav={
          <AgentConfigNav
            activeKey={activeNavKey}
            onActiveKeyChange={setActiveNavKey}
            statusLabel={detail?.status || "draft"}
            resourceHint={`${autosaveHint} · 模型 ${modelConfigs.length} 个 / 工作流 ${workflowOptions.length} 个`}
            workbenchLoading={workbenchLoading}
            locale={locale}
          />
        }
        config={
          <AgentConfigPanel
            activeNavKey={activeNavKey}
            onActiveNavKeyChange={setActiveNavKey}
            footer={
              <Space wrap>
                <Button theme="solid" type="primary" onClick={() => void handleSave()} loading={saving} disabled={!resourceReady} data-testid="app-bot-ide-save">
                  保存配置
                </Button>
              </Space>
            }
          >
            <AgentWorkbenchConfigTab
              activeNavKey={activeNavKey}
              detail={detail}
              name={name}
              setName={setName}
              description={description}
              setDescription={setDescription}
              avatarUrl={avatarUrl}
              setAvatarUrl={setAvatarUrl}
              openingMessage={openingMessage}
              setOpeningMessage={setOpeningMessage}
              presetQuestionsInput={presetQuestionsInput}
              setPresetQuestionsInput={setPresetQuestionsInput}
              promptSections={promptSections}
              updatePromptSection={updatePromptSection}
              saving={saving}
              resourceReady={resourceReady}
              modelConfigId={modelConfigId}
              setModelConfigId={setModelConfigId}
              modelConfigs={modelConfigs}
              workflowId={workflowId}
              setWorkflowId={setWorkflowId}
              workflowOptions={workflowOptions}
              handleBindWorkflow={handleBindWorkflow}
              pluginOptions={pluginOptions}
              selectedPluginIds={selectedPluginIds}
              handlePluginSelectionChange={handlePluginSelectionChange}
              knowledgeBaseOptions={knowledgeBaseOptions}
              selectedKnowledgeBaseIds={selectedKnowledgeBaseIds}
              handleKnowledgeSelectionChange={handleKnowledgeSelectionChange}
              knowledgeBindings={knowledgeBindings}
              setKnowledgeBindings={setKnowledgeBindings}
              pluginBindings={pluginBindings}
              setPluginBindings={setPluginBindings}
              pluginDetailMap={pluginDetailMap}
              variableOptions={variableOptions}
              databaseOptions={databaseOptions}
              selectedDatabaseIds={selectedDatabaseIds}
              handleDatabaseSelectionChange={handleDatabaseSelectionChange}
              databaseBindings={databaseBindings}
              setDatabaseBindings={setDatabaseBindings}
              persistedDatabaseIds={[...persistedDatabaseIds]}
              databaseBindingActionId={databaseBindingActionId}
              handleBindDatabase={handleBindDatabase}
              handleUnbindDatabase={handleUnbindDatabase}
              selectedVariableIds={selectedVariableIds}
              handleVariableSelectionChange={handleVariableSelectionChange}
              variableBindings={variableBindings}
              setVariableBindings={setVariableBindings}
              enableMemory={enableMemory}
              setEnableMemory={setEnableMemory}
              enableShortTermMemory={enableShortTermMemory}
              setEnableShortTermMemory={setEnableShortTermMemory}
              enableLongTermMemory={enableLongTermMemory}
              setEnableLongTermMemory={setEnableLongTermMemory}
              longTermMemoryTopK={longTermMemoryTopK}
              setLongTermMemoryTopK={setLongTermMemoryTopK}
              selectedModel={selectedModel}
              selectedWorkflow={selectedWorkflow}
            />
          </AgentConfigPanel>
        }
        debug={
          <AgentDebugPanel
            hasUnsavedChanges={hasUnsavedChanges}
            conversationId={conversationId}
            lastResourceUsage={lastResourceUsage}
            knowledgeBindings={knowledgeBindings}
            pluginBindings={pluginBindings}
            databaseBindings={databaseBindings}
            databaseOptions={databaseOptions}
            variableBindings={variableBindings}
            publicationLoading={publicationLoading}
            publications={publications}
            onPublishClick={openPublishModal}
            onOpenPublish={onOpenPublish}
            onRegenerateEmbedToken={() => void handleRegenerateEmbedToken()}
            saving={saving}
            workbenchLoading={workbenchLoading}
            messages={messages}
            streamingAssistant={streamingAssistant}
            messageInput={messageInput}
            onMessageInputChange={setMessageInput}
            modelCatalogEmpty={modelCatalogEmpty}
            modelNotSelected={modelNotSelected}
            canChat={canChat}
            workbenchError={workbenchError}
            sending={sending}
            onSendMessage={() => void handleSendMessage()}
            workflowInput={workflowInput}
            onWorkflowInputChange={setWorkflowInput}
            workflowId={workflowId}
            runningWorkflow={runningWorkflow}
            onRunWorkflow={() => void handleRunWorkflow()}
            thoughts={thoughts}
            lastTrace={lastTrace}
            onClearConversationContext={() => void handleClearConversationContext()}
            onClearConversationHistory={() => void handleClearConversationHistory()}
            onDeleteConversation={() => void handleDeleteConversation()}
            locale={locale}
          />
        }
      />
      <AgentPublishModal
        visible={publishModalOpen}
        releaseNote={publishReleaseNote}
        onReleaseNoteChange={setPublishReleaseNote}
        onCancel={() => setPublishModalOpen(false)}
        onConfirm={() => void submitPublish()}
        submitting={publishSubmitting}
        locale={locale}
      />
    </Surface>
  );
}
