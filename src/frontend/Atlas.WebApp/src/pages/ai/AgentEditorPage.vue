<template>
  <a-card :bordered="false" class="agent-editor-container">
    <template #title>
      <a-space>
        <a-button type="link" style="padding-left: 0" @click="goBack">
          {{ t("ai.agent.backToList") }}
        </a-button>
        <span class="editor-title">{{ t("ai.agent.editorTitle") }} <span v-if="form.name" class="agent-name-tag">- {{ form.name }}</span></span>
      </a-space>
    </template>
    <template #extra>
      <a-space>
        <a-button @click="goBack">{{ t("ai.agent.cancel") }}</a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">{{ t("common.save") }}</a-button>
      </a-space>
    </template>

    <a-tabs v-model:activeKey="activeTab" class="editor-tabs" :animated="false">
      <a-tab-pane key="setup" :tab="t('ai.agent.tabSetup', 'Agent 编排')">
        <div class="tab-content-scrollable">
          <a-row :gutter="24">
            <a-col :span="12">
              <div class="panel-container">
                <div class="section-title">{{ t('ai.agent.sectionBasic', '基础配置') }}</div>
                <a-form layout="vertical">
                  <a-row :gutter="16">
                    <a-col :span="16">
                      <a-form-item :label="t('ai.promptLib.colName')">
                        <a-input v-model:value="form.name" />
                      </a-form-item>
                    </a-col>
                    <a-col :span="8">
                      <a-form-item :label="t('ai.agent.labelAvatar')">
                        <a-input v-model:value="form.avatarUrl" />
                      </a-form-item>
                    </a-col>
                  </a-row>
                  <a-form-item :label="t('ai.promptLib.labelDescription')">
                    <a-textarea v-model:value="form.description" :rows="2" />
                  </a-form-item>

                  <a-divider class="section-divider" />
                  
                  <div class="section-title">{{ t('ai.agent.sectionModel', '模型设置') }}</div>
                  <a-row :gutter="16">
                    <a-col :span="12">
                      <a-form-item :label="t('ai.agent.labelModelConfig')">
                        <a-select v-model:value="form.modelConfigId" allow-clear :options="modelOptions" />
                      </a-form-item>
                    </a-col>
                    <a-col :span="12">
                      <a-form-item :label="t('ai.agent.labelModelOverride')">
                        <a-input v-model:value="form.modelName" />
                      </a-form-item>
                    </a-col>
                  </a-row>
                  
                  <a-row :gutter="16">
                    <a-col :span="12">
                      <a-form-item label="Temperature">
                        <a-slider v-model:value="form.temperature" :min="0" :max="2" :step="0.1" />
                      </a-form-item>
                    </a-col>
                    <a-col :span="12">
                      <a-form-item label="MaxTokens">
                        <a-input-number v-model:value="form.maxTokens" :min="1" :max="128000" style="width: 100%" />
                      </a-form-item>
                    </a-col>
                  </a-row>

                  <a-divider class="section-divider" />

                  <div class="section-title">{{ t('ai.agent.sectionMemory', '记忆与知识库') }}</div>
                  <a-form-item :label="t('ai.agent.labelKbIds')">
                    <a-input v-model:value="knowledgeBaseInput" :placeholder="t('ai.agent.kbPlaceholder')" />
                  </a-form-item>

                  <a-row :gutter="16">
                    <a-col :span="8">
                      <a-form-item :label="t('ai.agent.memoryEnable')">
                        <a-switch v-model:checked="form.enableMemory" />
                      </a-form-item>
                    </a-col>
                    <a-col :span="8">
                      <a-form-item :label="t('ai.agent.memoryShortTermEnable')">
                        <a-switch v-model:checked="form.enableShortTermMemory" :disabled="!form.enableMemory" />
                      </a-form-item>
                    </a-col>
                    <a-col :span="8">
                      <a-form-item :label="t('ai.agent.memoryLongTermEnable')">
                        <a-switch v-model:checked="form.enableLongTermMemory" :disabled="!form.enableMemory" />
                      </a-form-item>
                    </a-col>
                  </a-row>
                  <a-form-item :label="t('ai.agent.memoryLongTermTopK')" v-if="form.enableMemory && form.enableLongTermMemory">
                    <a-input-number v-model:value="form.longTermMemoryTopK" :min="1" :max="10" style="width: 100%" />
                  </a-form-item>

                  <a-divider class="section-divider" />

                  <div class="section-title">{{ t('ai.agent.sectionPlugins', '插件扩展') }}</div>
                  <a-form-item>
                    <div class="tool-binding-list">
                      <div v-for="(binding, index) in pluginBindings" :key="binding.rowId" class="tool-binding-row">
                        <a-select
                          v-model:value="binding.pluginId"
                          show-search
                          :filter-option="false"
                          :placeholder="t('ai.agent.pluginSelectPlaceholder')"
                          :options="pluginOptions"
                          :loading="pluginSearchLoading"
                          @search="handlePluginSearch"
                        />
                        <a-input-number
                          v-model:value="binding.sortOrder"
                          :min="0"
                          :placeholder="t('ai.agent.pluginSortOrder')"
                          style="width: 100px"
                        />
                        <a-switch v-model:checked="binding.isEnabled" />
                        <a-button danger type="text" @click="removePluginBinding(index)">
                          {{ t("common.delete") }}
                        </a-button>
                        <a-textarea
                          v-model:value="binding.toolConfigJson"
                          :rows="2"
                          :placeholder="t('ai.agent.pluginConfigPlaceholder')"
                        />
                      </div>

                      <a-button type="dashed" block @click="addPluginBinding">
                        {{ t("ai.agent.addPluginBinding") }}
                      </a-button>
                    </div>
                  </a-form-item>
                </a-form>
              </div>
            </a-col>

            <a-col :span="12">
              <div class="panel-container">
                <div class="section-title">{{ t('ai.agent.cardSystemPrompt') }}</div>
                <a-textarea v-model:value="form.systemPrompt" :rows="36" class="system-prompt-textarea" />
                <div class="counter">{{ t("ai.agent.charCount", { count: form.systemPrompt.length }) }}</div>
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>

      <a-tab-pane key="publish" :tab="t('ai.agent.tabPublish', '发布与集成')">
        <div class="tab-content-scrollable">
          <a-row :gutter="24">
            <a-col :span="14">
              <div class="panel-container">
                <div class="section-title">发布新版本</div>
                <div class="publish-action-box">
                  <a-textarea 
                    v-model:value="publicationNote" 
                    :rows="3" 
                    :placeholder="t('ai.agent.pubReleaseNote')" 
                    class="custom-textarea"
                  />
                  <div class="publish-btn-row">
                    <a-button type="primary" :loading="publicationLoading" @click="handlePublishPublication">
                      <template #icon><SendOutlined /></template>
                      {{ t("ai.agent.pubPublishBtn") }}
                    </a-button>
                  </div>
                </div>

                <a-divider class="section-divider" />

                <div class="section-header-row">
                  <div class="section-title" style="margin-bottom: 0;">版本历史</div>
                  <a-button size="small" :loading="tokenRefreshing" @click="handleRefreshEmbedToken">
                    <template #icon><ReloadOutlined /></template>
                    {{ t("ai.agent.pubRefreshTokenBtn") }}
                  </a-button>
                </div>

                <a-table
                  row-key="id"
                  :columns="publicationColumns"
                  :data-source="publicationItems"
                  :pagination="false"
                  size="middle"
                  :locale="{ emptyText: t('ai.agent.pubEmpty') }"
                  class="custom-table"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.key === 'status'">
                      <a-badge :status="record.isActive ? 'success' : 'default'" :text="record.isActive ? t('ai.agent.pubStatusActive') : t('ai.agent.pubStatusInactive')" />
                    </template>
                    <template v-if="column.key === 'actions'">
                      <a-popconfirm
                        :title="t('ai.agent.pubRollbackConfirm', { version: record.version })"
                        @confirm="handleRollbackPublication(record.version)"
                      >
                        <a-button
                          type="link"
                          size="small"
                          :disabled="record.isActive"
                          :loading="rollbackTarget === record.version"
                        >
                          {{ t("ai.agent.pubRollbackBtn") }}
                        </a-button>
                      </a-popconfirm>
                    </template>
                  </template>
                </a-table>
              </div>
            </a-col>

            <a-col :span="10">
              <div class="panel-container">
                <div class="section-title">{{ t('ai.agent.pubEmbedTitle') }}</div>
                
                <div class="token-alert-box">
                  <div class="token-alert-header">
                    <InfoCircleFilled class="token-alert-icon" />
                    <span>当前生效版本的 Embed Token</span>
                  </div>
                  <div class="token-alert-content">
                    {{ activePublication?.embedToken || t('ai.agent.pubNoActiveVersion') }}
                  </div>
                </div>
                
                <div class="code-snippet-container">
                  <div class="code-snippet-header">
                    <span class="snippet-title">{{ t("ai.agent.pubJsSnippet") }}</span>
                    <a-button type="link" size="small" @click="copySnippet(embedJsSnippet, t('ai.agent.pubCopySuccess'))">
                      <template #icon><CopyOutlined /></template>
                      {{ t("ai.agent.pubCopyJs") }}
                    </a-button>
                  </div>
                  <pre class="code-block"><code>{{ embedJsSnippet }}</code></pre>
                </div>

                <div class="code-snippet-container" style="margin-top: 20px">
                  <div class="code-snippet-header">
                    <span class="snippet-title">{{ t("ai.agent.pubIframeSnippet") }}</span>
                    <a-button type="link" size="small" @click="copySnippet(embedIframeSnippet, t('ai.agent.pubCopySuccess'))">
                      <template #icon><CopyOutlined /></template>
                      {{ t("ai.agent.pubCopyIframe") }}
                    </a-button>
                  </div>
                  <pre class="code-block"><code>{{ embedIframeSnippet }}</code></pre>
                </div>
              </div>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>
    </a-tabs>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";
import { SendOutlined, ReloadOutlined, CopyOutlined, InfoCircleFilled } from '@ant-design/icons-vue';

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  getAgentById,
  publishAgent,
  type AgentDetail,
  updateAgent
} from "@/services/api-agent";
import {
  getAgentPublications,
  publishAgentPublication,
  regenerateAgentEmbedToken,
  rollbackAgentPublication,
  type AgentPublicationItem
} from "@/services/api-agent-publication";
import { getAiPluginsPaged, type AiPluginListItem } from "@/services/api-ai-plugin";
import { getEnabledModelConfigs, type ModelConfigDto } from "@/services/api-model-config";
import { resolveCurrentAppId } from "@/utils/app-context";
import { getTenantId } from "@/utils/auth";

const route = useRoute();
const router = useRouter();
const agentId = String(route.params.id ?? "");

const activeTab = ref("setup");
const agent = ref<AgentDetail | null>(null);
const modelConfigs = ref<ModelConfigDto[]>([]);
const saving = ref(false);
const publishing = ref(false);
const knowledgeBaseInput = ref("");
const pluginSearchLoading = ref(false);
const pluginSource = ref<AiPluginListItem[]>([]);
const pluginBindings = ref<PluginBindingRow[]>([]);
const publicationItems = ref<AgentPublicationItem[]>([]);
const publicationNote = ref("");
const publicationLoading = ref(false);
const tokenRefreshing = ref(false);
const rollbackTarget = ref<number | null>(null);

const form = reactive({
  name: "",
  description: "",
  avatarUrl: "",
  systemPrompt: "",
  modelConfigId: undefined as string | undefined,
  modelName: "",
  temperature: 1,
  maxTokens: 2048,
  enableMemory: true,
  enableShortTermMemory: true,
  enableLongTermMemory: true,
  longTermMemoryTopK: 3
});

const modelOptions = computed(() =>
  modelConfigs.value.map((item) => ({
    label: `${item.name} (${item.providerType})`,
    value: item.id
  }))
);

const pluginOptions = computed(() =>
  pluginSource.value.map((item) => ({
    label: `${item.name}${item.category ? ` (${item.category})` : ""}`,
    value: item.id
  }))
);
const activePublication = computed(() => publicationItems.value.find((item) => item.isActive));
const publicationColumns = computed(() => [
  { title: t("ai.agent.pubColVersion"), dataIndex: "version", key: "version", width: 70 },
  { title: t("ai.agent.pubColStatus"), key: "status", width: 90 },
  { title: t("ai.agent.pubColReleaseNote"), dataIndex: "releaseNote", key: "releaseNote", ellipsis: true },
  { title: t("ai.agent.pubColTokenExpire"), dataIndex: "embedTokenExpiresAt", key: "embedTokenExpiresAt", width: 160 },
  { title: t("ai.agent.pubColCreatedAt"), dataIndex: "createdAt", key: "createdAt", width: 160 },
  { title: t("ai.colActions"), key: "actions", width: 100, align: 'center' }
]);
const embedJsSnippet = computed(() => {
  const token = activePublication.value?.embedToken ?? "";
  const tenantId = getTenantId() || "";
  const origin = window.location.origin;
  return `<div id="atlas-embed-chat"></div>
<script src="${origin}/embed-chat.js"><\/script>
<script>
window.AtlasEmbedChat.mount('#atlas-embed-chat', {
  apiBaseUrl: '${origin}/api/v1',
  tenantId: '${tenantId}',
  embedToken: '${token}',
  externalUserId: 'demo-user-1'
});
<\/script>`;
});
const embedIframeSnippet = computed(() => {
  const token = activePublication.value?.embedToken ?? "";
  const tenantId = encodeURIComponent(getTenantId() || "");
  const origin = window.location.origin;
  return `<iframe src="${origin}/embed-chat.html?tenantId=${tenantId}&embedToken=${token}" style="width:430px;height:620px;border:0;"></iframe>`;
});

type PluginBindingRow = {
  rowId: string;
  pluginId?: string;
  sortOrder: number;
  isEnabled: boolean;
  toolConfigJson: string;
};

function goBack() {
  const currentAppId = resolveCurrentAppId(route);
  if (!currentAppId) {
    void router.push("/console/apps");
    return;
  }
  void router.push(`/apps/${currentAppId}/agents`);
}

async function loadData() {
  if (!agentId) {
    message.error(t("ai.agent.loadAgentFailed"));
    return;
  }

  try {
    const [detail, models, publications]  = await Promise.all([
      getAgentById(agentId),
      getEnabledModelConfigs(),
      getAgentPublications(agentId)
    ]);

    if (!isMounted.value) return;
    agent.value = detail;
    modelConfigs.value = models;
    Object.assign(form, {
      name: detail.name,
      description: detail.description || "",
      avatarUrl: detail.avatarUrl || "",
      systemPrompt: detail.systemPrompt || "",
      modelConfigId: detail.modelConfigId,
      modelName: detail.modelName || "",
      temperature: detail.temperature ?? 1,
      maxTokens: detail.maxTokens ?? 2048,
      enableMemory: detail.enableMemory ?? true,
      enableShortTermMemory: detail.enableShortTermMemory ?? true,
      enableLongTermMemory: detail.enableLongTermMemory ?? true,
      longTermMemoryTopK: detail.longTermMemoryTopK ?? 3
    });
    knowledgeBaseInput.value = (detail.knowledgeBaseIds || []).join(",");
    publicationItems.value = publications;
    pluginBindings.value = (detail.pluginBindings || []).map((binding, index) => ({
      rowId: `${binding.pluginId}-${index}-${Date.now()}`,
      pluginId: binding.pluginId,
      sortOrder: binding.sortOrder,
      isEnabled: binding.isEnabled,
      toolConfigJson: binding.toolConfigJson || "{}"
    }));
    if (pluginBindings.value.length === 0) {
      addPluginBinding();
    }

    await loadPluginOptions();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.loadAgentFailed"));
  }
}

function parseKnowledgeBaseIds() {
  return knowledgeBaseInput.value
    .split(",")
    .map((item) => item.trim())
    .filter((item) => /^\d+$/.test(item));
}

async function loadPluginOptions(keyword?: string) {
  pluginSearchLoading.value = true;
  try {
    const result = await getAiPluginsPaged(
      { pageIndex: 1, pageSize: 20 },
      keyword && keyword.trim() ? keyword.trim() : undefined
    );

    if (!isMounted.value) return;
    pluginSource.value = result.items;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.loadPluginsFailed"));
  } finally {
    pluginSearchLoading.value = false;
  }
}

function addPluginBinding() {
  pluginBindings.value.push({
    rowId: `binding-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    pluginId: undefined,
    sortOrder: pluginBindings.value.length,
    isEnabled: true,
    toolConfigJson: "{}"
  });
}

function removePluginBinding(index: number) {
  pluginBindings.value.splice(index, 1);
}

function handlePluginSearch(keyword: string) {
  void loadPluginOptions(keyword);
}

async function handleSave() {
  if (!agentId) {
    message.error(t("ai.agent.loadAgentFailed"));
    return;
  }

  if (!form.name.trim()) {
    message.warning(t("ai.agent.warnName"));
    return;
  }

  saving.value = true;
  try {
    await updateAgent(agentId, {
      name: form.name,
      description: form.description || undefined,
      avatarUrl: form.avatarUrl || undefined,
      systemPrompt: form.systemPrompt || undefined,
      modelConfigId: form.modelConfigId,
      modelName: form.modelName || undefined,
      temperature: form.temperature,
      maxTokens: form.maxTokens,
      enableMemory: form.enableMemory,
      enableShortTermMemory: form.enableShortTermMemory,
      enableLongTermMemory: form.enableLongTermMemory,
      longTermMemoryTopK: form.longTermMemoryTopK,
      knowledgeBaseIds: parseKnowledgeBaseIds(),
      pluginBindings: pluginBindings.value
        .filter((binding) => typeof binding.pluginId === "string" && /^\d+$/.test(binding.pluginId))
        .map((binding) => ({
          pluginId: binding.pluginId as string,
          sortOrder: binding.sortOrder,
          isEnabled: binding.isEnabled,
          toolConfigJson: binding.toolConfigJson || "{}"
        }))
    });

    if (!isMounted.value) return;
    message.success(t("ai.agent.saveSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.workflow.saveFailed"));
  } finally {
    saving.value = false;
  }
}

async function handlePublish() {
  if (!agentId) {
    message.error(t("ai.agent.loadAgentFailed"));
    return;
  }

  publishing.value = true;
  try {
    await publishAgent(agentId);

    if (!isMounted.value) return;
    message.success(t("ai.agent.publishSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.publishFailed"));
  } finally {
    publishing.value = false;
  }
}

async function handlePublishPublication() {
  if (!agentId) {
    message.error(t("ai.agent.loadAgentFailed"));
    return;
  }

  publicationLoading.value = true;
  try {
    await publishAgentPublication(agentId, publicationNote.value || undefined);
    message.success(t("ai.agent.pubPublishSuccess"));
    publicationNote.value = "";
    publicationItems.value = await getAgentPublications(agentId);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.pubPublishFailed"));
  } finally {
    publicationLoading.value = false;
  }
}

async function handleRollbackPublication(targetVersion: number) {
  if (!agentId) {
    message.error(t("ai.agent.loadAgentFailed"));
    return;
  }

  rollbackTarget.value = targetVersion;
  try {
    await rollbackAgentPublication(agentId, targetVersion, publicationNote.value || undefined);
    message.success(t("ai.agent.pubRollbackSuccess"));
    publicationItems.value = await getAgentPublications(agentId);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.pubRollbackFailed"));
  } finally {
    rollbackTarget.value = null;
  }
}

async function handleRefreshEmbedToken() {
  if (!agentId) {
    message.error(t("ai.agent.loadAgentFailed"));
    return;
  }

  tokenRefreshing.value = true;
  try {
    await regenerateAgentEmbedToken(agentId);
    message.success(t("ai.agent.pubTokenRefreshSuccess"));
    publicationItems.value = await getAgentPublications(agentId);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.pubTokenRefreshFailed"));
  } finally {
    tokenRefreshing.value = false;
  }
}

async function copySnippet(text: string, successMessage: string) {
  try {
    await navigator.clipboard.writeText(text);
    message.success(successMessage);
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.copyFailed"));
  }
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.agent-editor-container {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.agent-editor-container :deep(.ant-card-body) {
  padding: 0;
  display: flex;
  flex-direction: column;
  height: calc(100vh - 140px); /* Adjust based on your layout header height */
}

.editor-title {
  font-weight: 600;
  font-size: 16px;
}

.agent-name-tag {
  color: #1890ff;
  font-weight: normal;
}

.editor-tabs {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.editor-tabs :deep(.ant-tabs-nav) {
  margin-bottom: 0;
  padding: 0 24px;
}

.editor-tabs :deep(.ant-tabs-content-holder) {
  flex: 1;
  overflow: hidden;
  background-color: #f0f2f5;
}

.editor-tabs :deep(.ant-tabs-content) {
  height: 100%;
}

.editor-tabs :deep(.ant-tabs-tabpane) {
  height: 100%;
}

.tab-content-scrollable {
  height: 100%;
  overflow-y: auto;
  padding: 24px;
}

.panel-container {
  background: #fff;
  border-radius: 8px;
  padding: 24px;
  height: 100%;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.03);
}

.section-title {
  font-size: 15px;
  font-weight: 600;
  margin-bottom: 16px;
  color: #1f2329;
  display: flex;
  align-items: center;
}

.section-title::before {
  content: '';
  display: inline-block;
  width: 4px;
  height: 14px;
  background-color: #1890ff;
  margin-right: 8px;
  border-radius: 2px;
}

.section-divider {
  margin: 24px 0;
}

.system-prompt-textarea {
  font-family: 'Consolas', 'Courier New', monospace;
  font-size: 14px;
  line-height: 1.6;
}

.counter {
  margin-top: 8px;
  text-align: right;
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.publish-action-box {
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px;
}

.custom-textarea {
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  resize: none;
}

.custom-textarea:focus {
  box-shadow: 0 0 0 2px rgba(24, 144, 255, 0.1);
}

.publish-btn-row {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
}

.section-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.token-alert-box {
  background-color: #e6f4ff;
  border: 1px solid #91caff;
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 24px;
}

.token-alert-header {
  display: flex;
  align-items: center;
  color: #1677ff;
  font-weight: 500;
  margin-bottom: 8px;
}

.token-alert-icon {
  margin-right: 8px;
  font-size: 16px;
}

.token-alert-content {
  font-family: 'Consolas', 'Courier New', monospace;
  color: #333;
  word-break: break-all;
  background: rgba(255,255,255,0.6);
  padding: 8px;
  border-radius: 4px;
}

.code-snippet-container {
  background: #282c34;
  border-radius: 8px;
  overflow: hidden;
}

.code-snippet-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px;
  background: #1c1f24;
  border-bottom: 1px solid #3e4451;
}

.code-snippet-header .snippet-title {
  color: #abb2bf;
  font-size: 13px;
  font-weight: 500;
}

.code-snippet-header .ant-btn-link {
  color: #98c379;
}

.code-snippet-header .ant-btn-link:hover {
  color: #b5e890;
}

.code-block {
  margin: 0;
  padding: 16px;
  overflow-x: auto;
}

.code-block code {
  font-family: 'Consolas', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.5;
  color: #abb2bf;
}

.custom-table :deep(.ant-table-thead > tr > th) {
  background: #fafafa;
  font-weight: 500;
}

.tool-binding-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.tool-binding-row {
  display: grid;
  grid-template-columns: 1fr 100px auto auto;
  gap: 12px;
  align-items: start;
  padding: 16px;
  background: #fafafa;
  border: 1px solid #e8e8e8;
  border-radius: 6px;
  transition: all 0.3s;
}

.tool-binding-row:hover {
  border-color: #d9d9d9;
  box-shadow: 0 2px 4px rgba(0,0,0,0.02);
}

.tool-binding-row :deep(.ant-input-textarea) {
  grid-column: 1 / 5;
  margin-top: 4px;
}
</style>
