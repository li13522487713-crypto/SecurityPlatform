<template>
  <div class="agent-editor-page">
    <a-card :bordered="false" class="editor-header-card">
      <div class="editor-header">
        <div>
          <a-button type="link" class="back-btn" @click="goBack">
            {{ t("ai.agent.backToWorkspace") }}
          </a-button>
          <div class="editor-title-row">
            <h1 class="editor-title">{{ headerTitle }}</h1>
            <a-tag color="blue">{{ isTeamEditor ? t("ai.agent.agentTypeTeam") : t("ai.agent.agentTypeSingle") }}</a-tag>
            <a-tag v-if="isTeamEditor" :color="teamModeColor(teamMetadata.mode)">
              {{ teamModeText(teamMetadata.mode) }}
            </a-tag>
          </div>
          <p class="editor-subtitle">{{ headerSubtitle }}</p>
        </div>
        <a-space>
          <a-button v-if="!isTeamEditor" :loading="publishing" @click="handlePublish">
            {{ t("ai.agent.actionPublish") }}
          </a-button>
          <a-button @click="goChat">{{ isTeamEditor ? t("ai.agent.actionChat") : t("ai.agent.openChat") }}</a-button>
          <a-button type="primary" :loading="saving" @click="handleSave">{{ t("common.save") }}</a-button>
        </a-space>
      </div>
    </a-card>

    <div class="editor-body">
      <section class="editor-canvas-section">
        <a-card :bordered="false" class="panel-card panel-card--canvas">
          <div class="panel-head">
            <div>
              <div class="panel-title">{{ t("ai.agent.canvasTitle") }}</div>
              <div class="panel-subtitle">{{ t("ai.agent.canvasSubtitle") }}</div>
            </div>
            <a-segmented
              v-if="isTeamEditor"
              v-model:value="teamMetadata.mode"
              :options="teamModeOptions"
            />
          </div>

          <div v-if="isTeamEditor" class="team-canvas">
            <div class="team-canvas__lead">
              <div class="canvas-node canvas-node--lead">
                <div class="canvas-node__eyebrow">{{ t("ai.agent.coordinatorLabel") }}</div>
                <div class="canvas-node__title">{{ teamMetadata.coordinatorName }}</div>
                <div class="canvas-node__desc">{{ teamMetadata.coordinatorPrompt }}</div>
              </div>
            </div>
            <div class="team-canvas__links">
              <div v-for="member in teamMembersPreview" :key="member.rowKey" class="team-canvas__member">
                <div class="team-canvas__line"></div>
                <div class="canvas-node">
                  <div class="canvas-node__eyebrow">{{ member.alias || t("ai.agent.subAgentLabel") }}</div>
                  <div class="canvas-node__title">{{ member.agentName }}</div>
                  <div class="canvas-node__desc">{{ member.promptPrefix || t("ai.agent.memberPromptFallback") }}</div>
                </div>
              </div>
            </div>
          </div>

          <div v-else class="single-canvas">
            <div class="canvas-node canvas-node--lead canvas-node--single">
              <div class="canvas-node__eyebrow">{{ t("ai.agent.singleAgentLabel") }}</div>
              <div class="canvas-node__title">{{ singleForm.name || t("ai.agent.singleAgentUntitled") }}</div>
              <div class="canvas-node__desc">
                {{ singleForm.systemPrompt || t("ai.agent.singleAgentPromptEmpty") }}
              </div>
            </div>
            <div class="single-canvas__facts">
              <div class="fact-chip">
                <span>{{ t("ai.agent.labelModelConfig") }}</span>
                <strong>{{ currentModelLabel }}</strong>
              </div>
              <div class="fact-chip">
                <span>{{ t("ai.agent.memoryEnable") }}</span>
                <strong>{{ singleForm.enableMemory ? t("common.statusEnabled") : t("common.statusDisabled") }}</strong>
              </div>
              <div class="fact-chip">
                <span>{{ t("ai.agent.cardVersion") }}</span>
                <strong>v{{ singlePublishVersion }}</strong>
              </div>
            </div>
          </div>

          <div v-if="isTeamEditor" class="canvas-footer">
            <div class="footer-chip">{{ t("ai.agent.relationshipModeLabel") }}: {{ relationshipModeText }}</div>
            <div class="footer-chip">{{ t("ai.agent.stopConditionLabel") }}: {{ stopConditionText }}</div>
          </div>
        </a-card>

        <a-card v-if="isTeamEditor" :bordered="false" class="panel-card">
          <div class="panel-head">
            <div>
              <div class="panel-title">{{ t("ai.agent.debugPanelTitle") }}</div>
              <div class="panel-subtitle">{{ t("ai.agent.debugPanelSubtitle") }}</div>
            </div>
          </div>
          <MultiAgentRunPanel :orchestration-id="teamSourceId" @executed="handleTeamExecuted" />
        </a-card>

        <a-card v-else :bordered="false" class="panel-card">
          <div class="panel-head">
            <div>
              <div class="panel-title">{{ t("ai.agent.publishPanelTitle") }}</div>
              <div class="panel-subtitle">{{ t("ai.agent.publishPanelSubtitle") }}</div>
            </div>
          </div>
          <a-space class="publish-toolbar">
            <a-button type="primary" :loading="publicationLoading" @click="handlePublishPublication">
              {{ t("ai.agent.pubPublishBtn") }}
            </a-button>
            <a-button :loading="tokenRefreshing" @click="handleRefreshEmbedToken">
              {{ t("ai.agent.pubRefreshTokenBtn") }}
            </a-button>
          </a-space>
          <a-textarea
            v-model:value="publicationNote"
            :rows="3"
            :placeholder="t('ai.agent.pubReleaseNote')"
            class="publish-note"
          />
          <a-table
            row-key="id"
            :columns="publicationColumns"
            :data-source="publicationItems"
            size="small"
            :pagination="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'status'">
                <a-badge :status="record.isActive ? 'success' : 'default'" :text="record.isActive ? t('ai.agent.pubStatusActive') : t('ai.agent.pubStatusInactive')" />
              </template>
              <template v-else-if="column.key === 'actions'">
                <a-button
                  type="link"
                  size="small"
                  :disabled="record.isActive"
                  :loading="rollbackTarget === record.version"
                  @click="handleRollbackPublication(record.version)"
                >
                  {{ t("ai.agent.pubRollbackBtn") }}
                </a-button>
              </template>
            </template>
          </a-table>
        </a-card>
      </section>

      <section class="editor-side-section">
        <a-card :bordered="false" class="panel-card">
          <div class="panel-head">
            <div>
              <div class="panel-title">{{ t("ai.agent.configPanelTitle") }}</div>
              <div class="panel-subtitle">{{ t("ai.agent.configPanelSubtitle") }}</div>
            </div>
          </div>

          <template v-if="isTeamEditor">
            <a-form layout="vertical">
              <a-form-item :label="t('ai.promptLib.colName')">
                <a-input v-model:value="teamForm.name" />
              </a-form-item>
              <a-form-item :label="t('ai.promptLib.labelDescription')">
                <a-textarea v-model:value="teamForm.description" :rows="3" />
              </a-form-item>
              <a-form-item :label="t('ai.agent.coordinatorPromptLabel')">
                <a-textarea v-model:value="teamMetadata.coordinatorPrompt" :rows="4" />
              </a-form-item>
              <a-row :gutter="12">
                <a-col :span="12">
                  <a-form-item :label="t('ai.agent.relationshipModeLabel')">
                    <a-select v-model:value="teamMetadata.relationshipMode" :options="relationshipModeOptions" />
                  </a-form-item>
                </a-col>
                <a-col :span="12">
                  <a-form-item :label="t('ai.agent.stopConditionLabel')">
                    <a-select v-model:value="teamMetadata.stopCondition" :options="stopConditionOptions" />
                  </a-form-item>
                </a-col>
              </a-row>
            </a-form>
          </template>

          <template v-else>
            <a-form layout="vertical">
              <a-form-item :label="t('ai.promptLib.colName')">
                <a-input v-model:value="singleForm.name" />
              </a-form-item>
              <a-form-item :label="t('ai.promptLib.labelDescription')">
                <a-textarea v-model:value="singleForm.description" :rows="3" />
              </a-form-item>
              <a-form-item :label="t('ai.agent.labelAvatar')">
                <a-input v-model:value="singleForm.avatarUrl" />
              </a-form-item>
              <a-row :gutter="12">
                <a-col :span="12">
                  <a-form-item :label="t('ai.agent.labelModelConfig')">
                    <a-select v-model:value="singleForm.modelConfigId" allow-clear :options="modelOptions" />
                  </a-form-item>
                </a-col>
                <a-col :span="12">
                  <a-form-item :label="t('ai.agent.labelModelOverride')">
                    <a-input v-model:value="singleForm.modelName" />
                  </a-form-item>
                </a-col>
              </a-row>
              <a-row :gutter="12">
                <a-col :span="12">
                  <a-form-item :label="t('ai.agent.labelTemperature')">
                    <a-slider v-model:value="singleForm.temperature" :min="0" :max="2" :step="0.1" />
                  </a-form-item>
                </a-col>
                <a-col :span="12">
                  <a-form-item :label="t('ai.agent.labelMaxTokens')">
                    <a-input-number v-model:value="singleForm.maxTokens" :min="1" :max="128000" style="width: 100%" />
                  </a-form-item>
                </a-col>
              </a-row>
              <a-form-item :label="t('ai.agent.cardSystemPrompt')">
                <a-textarea v-model:value="singleForm.systemPrompt" :rows="8" />
              </a-form-item>
            </a-form>
          </template>
        </a-card>

        <a-card :bordered="false" class="panel-card">
          <div class="panel-head">
            <div>
              <div class="panel-title">{{ t("ai.agent.memberPanelTitle") }}</div>
              <div class="panel-subtitle">
                {{ isTeamEditor ? t("ai.agent.memberPanelSubtitle") : t("ai.agent.capabilityPanelSubtitle") }}
              </div>
            </div>
            <a-button v-if="isTeamEditor" type="dashed" size="small" @click="addTeamMember">
              {{ t("ai.agent.addTeamMember") }}
            </a-button>
          </div>

          <template v-if="isTeamEditor">
            <div class="member-list">
              <div v-for="(member, index) in teamForm.members" :key="member.rowKey" class="member-card">
                <div class="member-card__header">
                  <span>{{ member.alias || t("ai.agent.subAgentLabel") }}</span>
                  <a-button type="text" danger size="small" @click="removeTeamMember(index)">
                    {{ t("common.delete") }}
                  </a-button>
                </div>
                <a-select v-model:value="member.agentId" :options="agentOptions" style="width: 100%" />
                <a-input v-model:value="member.alias" class="member-card__field" :placeholder="t('ai.multiAgent.aliasPlaceholder')" />
                <a-input v-model:value="member.promptPrefix" class="member-card__field" :placeholder="t('ai.agent.memberPromptPlaceholder')" />
              </div>
            </div>
          </template>

          <template v-else>
            <a-form layout="vertical">
              <a-form-item :label="t('ai.agent.labelKbIds')">
                <a-input v-model:value="knowledgeBaseInput" :placeholder="t('ai.agent.kbPlaceholder')" />
              </a-form-item>
              <a-row :gutter="12">
                <a-col :span="8">
                  <a-form-item :label="t('ai.agent.memoryEnable')">
                    <a-switch v-model:checked="singleForm.enableMemory" />
                  </a-form-item>
                </a-col>
                <a-col :span="8">
                  <a-form-item :label="t('ai.agent.memoryShortTermEnable')">
                    <a-switch v-model:checked="singleForm.enableShortTermMemory" />
                  </a-form-item>
                </a-col>
                <a-col :span="8">
                  <a-form-item :label="t('ai.agent.memoryLongTermEnable')">
                    <a-switch v-model:checked="singleForm.enableLongTermMemory" />
                  </a-form-item>
                </a-col>
              </a-row>
              <a-form-item :label="t('ai.agent.memoryLongTermTopK')">
                <a-input-number v-model:value="singleForm.longTermMemoryTopK" :min="1" :max="10" style="width: 100%" />
              </a-form-item>
              <div class="member-list">
                <div v-for="(binding, index) in pluginBindings" :key="binding.rowId" class="member-card">
                  <div class="member-card__header">
                    <span>{{ t("ai.agent.labelPluginBindings") }}</span>
                    <a-button type="text" danger size="small" @click="removePluginBinding(index)">
                      {{ t("common.delete") }}
                    </a-button>
                  </div>
                  <a-select v-model:value="binding.pluginId" :options="pluginOptions" style="width: 100%" />
                  <a-input v-model:value="binding.toolConfigJson" class="member-card__field" :placeholder="t('ai.agent.pluginConfigPlaceholder')" />
                </div>
                <a-button type="dashed" block @click="addPluginBinding">
                  {{ t("ai.agent.addPluginBinding") }}
                </a-button>
              </div>
            </a-form>
          </template>
        </a-card>

        <a-card :bordered="false" class="panel-card">
          <div class="panel-head">
            <div>
              <div class="panel-title">{{ t("ai.agent.schemaPanelTitle") }}</div>
              <div class="panel-subtitle">{{ t("ai.agent.schemaPanelSubtitle") }}</div>
            </div>
          </div>
          <a-form layout="vertical">
            <a-form-item :label="t('ai.agent.schemaPromptLabel')">
              <a-textarea v-model:value="schemaPrompt" :rows="4" :placeholder="t('ai.agent.schemaPromptPlaceholder')" />
            </a-form-item>
            <div class="schema-flags">
              <a-checkbox v-model:checked="teamMetadata.capabilityFlags.schemaBuilder">{{ t("ai.agent.schemaEnable") }}</a-checkbox>
              <a-checkbox v-model:checked="teamMetadata.capabilityFlags.fieldSuggestions">{{ t("ai.agent.schemaFields") }}</a-checkbox>
              <a-checkbox v-model:checked="teamMetadata.capabilityFlags.indexSuggestions">{{ t("ai.agent.schemaIndexes") }}</a-checkbox>
              <a-checkbox v-model:checked="teamMetadata.capabilityFlags.permissionSuggestions">{{ t("ai.agent.schemaPermissions") }}</a-checkbox>
            </div>
            <a-space class="schema-actions">
              <a-button type="primary" @click="generateSchemaDraft">{{ t("ai.agent.schemaGenerate") }}</a-button>
              <a-button @click="goChatWithSchema">{{ t("ai.agent.actionSchemaBuild") }}</a-button>
              <a-button @click="goDataManagement">{{ t("ai.agent.schemaConfirmCreate") }}</a-button>
            </a-space>
          </a-form>

          <div v-if="teamMetadata.schemaDraft" class="schema-draft">
            <div class="schema-draft__title">{{ teamMetadata.schemaDraft.title }}</div>
            <div class="schema-draft__summary">{{ teamMetadata.schemaDraft.summary }}</div>
            <div class="schema-draft__section">
              <div class="schema-draft__label">{{ t("ai.agent.schemaEntityCount") }}</div>
              <a-tag v-for="entity in teamMetadata.schemaDraft.entities" :key="entity.name">{{ entity.title }}</a-tag>
            </div>
            <div class="schema-draft__section">
              <div class="schema-draft__label">{{ t("ai.agent.schemaQuestions") }}</div>
              <ul class="schema-draft__list">
                <li v-for="question in teamMetadata.schemaDraft.openQuestions" :key="question">{{ question }}</li>
              </ul>
            </div>
          </div>
        </a-card>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  getAgentsPaged,
  getAgentById,
  publishAgent,
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
import {
  getMultiAgentOrchestrationById,
  updateMultiAgentOrchestration
} from "@/services/api-multi-agent";
import MultiAgentRunPanel from "@/components/multi-agent/MultiAgentRunPanel.vue";
import {
  buildSchemaDraftFromPrompt,
  getDefaultTeamAgentMetadata,
  getTeamAgentSourceId,
  isTeamAgentId,
  loadTeamAgentMetadata,
  saveTeamAgentMetadata,
  toOrchestrationMode,
  type TeamAgentMetadata,
  type WorkspaceTeamMode
} from "@/services/agent-workspace";

type PluginBindingRow = {
  rowId: string;
  pluginId?: string;
  sortOrder: number;
  isEnabled: boolean;
  toolConfigJson: string;
};

type TeamMemberRow = {
  rowKey: string;
  agentId?: string;
  alias?: string;
  promptPrefix?: string;
  sortOrder: number;
  isEnabled: boolean;
};

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const editorId = computed(() => String(route.params.id ?? ""));
const isTeamEditor = computed(() => isTeamAgentId(editorId.value));
const teamSourceId = computed(() => (isTeamEditor.value ? getTeamAgentSourceId(editorId.value) : 0));

const saving = ref(false);
const publishing = ref(false);
const publicationLoading = ref(false);
const tokenRefreshing = ref(false);
const rollbackTarget = ref<number | null>(null);
const publicationNote = ref("");
const publicationItems = ref<AgentPublicationItem[]>([]);
const knowledgeBaseInput = ref("");
const schemaPrompt = ref("");

const modelConfigs = ref<ModelConfigDto[]>([]);
const pluginSource = ref<AiPluginListItem[]>([]);
const agentOptions = ref<Array<{ label: string; value: string }>>([]);

const singleForm = reactive({
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

const teamForm = reactive({
  name: "",
  description: "",
  members: [] as TeamMemberRow[]
});

const teamMetadata = reactive<TeamAgentMetadata>(getDefaultTeamAgentMetadata("", ""));
const pluginBindings = ref<PluginBindingRow[]>([]);

const teamModeOptions = computed(() => [
  { label: t("ai.agent.teamModeGroupChat"), value: "group_chat" },
  { label: t("ai.agent.teamModeWorkflow"), value: "workflow" },
  { label: t("ai.agent.teamModeHandoff"), value: "handoff" }
]);

const relationshipModeOptions = computed(() => [
  { label: t("ai.agent.relationshipRoundRobin"), value: "round_robin" },
  { label: t("ai.agent.relationshipDag"), value: "dag" },
  { label: t("ai.agent.relationshipHandoff"), value: "handoff" }
]);

const stopConditionOptions = computed(() => [
  { label: t("ai.agent.stopApproved"), value: "approved_or_manual" },
  { label: t("ai.agent.stopMaxRound"), value: "max_round_or_manual" },
  { label: t("ai.agent.stopManual"), value: "manual_only" }
]);

const modelOptions = computed(() =>
  modelConfigs.value.map((item) => ({
    label: `${item.name} (${item.providerType})`,
    value: String(item.id)
  }))
);

const pluginOptions = computed(() =>
  pluginSource.value.map((item) => ({
    label: `${item.name}${item.category ? ` (${item.category})` : ""}`,
    value: item.id
  }))
);

const publicationColumns = computed(() => [
  { title: t("ai.agent.pubColVersion"), dataIndex: "version", key: "version", width: 80 },
  { title: t("ai.agent.pubColStatus"), key: "status", width: 120 },
  { title: t("ai.agent.pubColReleaseNote"), dataIndex: "releaseNote", key: "releaseNote" },
  { title: t("ai.colActions"), key: "actions", width: 100 }
]);

const currentModelLabel = computed(() => {
  const selected = modelOptions.value.find((item) => item.value === singleForm.modelConfigId);
  return selected?.label || singleForm.modelName || "-";
});

const singlePublishVersion = computed(() => publicationItems.value.find((item) => item.isActive)?.version || 0);

const headerTitle = computed(() =>
  isTeamEditor.value
    ? teamForm.name || t("ai.agent.teamDesignerTitle")
    : singleForm.name || t("ai.agent.editorTitle")
);

const headerSubtitle = computed(() =>
  isTeamEditor.value
    ? t("ai.agent.teamDesignerSubtitle")
    : t("ai.agent.singleDesignerSubtitle")
);

const relationshipModeText = computed(() => {
  if (teamMetadata.relationshipMode === "dag") {
    return t("ai.agent.relationshipDag");
  }

  if (teamMetadata.relationshipMode === "handoff") {
    return t("ai.agent.relationshipHandoff");
  }

  return t("ai.agent.relationshipRoundRobin");
});

const stopConditionText = computed(() => {
  if (teamMetadata.stopCondition === "manual_only") {
    return t("ai.agent.stopManual");
  }

  if (teamMetadata.stopCondition === "max_round_or_manual") {
    return t("ai.agent.stopMaxRound");
  }

  return t("ai.agent.stopApproved");
});

const teamMembersPreview = computed(() =>
  teamForm.members.map((member) => ({
    ...member,
    agentName: agentOptions.value.find((item) => item.value === member.agentId)?.label || t("ai.agent.memberAgentFallback")
  }))
);

function teamModeText(mode: WorkspaceTeamMode) {
  if (mode === "workflow") {
    return t("ai.agent.teamModeWorkflow");
  }

  if (mode === "handoff") {
    return t("ai.agent.teamModeHandoff");
  }

  return t("ai.agent.teamModeGroupChat");
}

function teamModeColor(mode: WorkspaceTeamMode) {
  if (mode === "workflow") {
    return "purple";
  }

  if (mode === "handoff") {
    return "orange";
  }

  return "cyan";
}

function goBack() {
  void router.push("/ai/agents");
}

function goChat() {
  void router.push(`/ai/agents/${editorId.value}/chat`);
}

function goChatWithSchema() {
  void router.push({
    path: `/ai/agents/${editorId.value}/chat`,
    query: { entrySkill: "schema_builder" }
  });
}

function goDataManagement() {
  void router.push("/console/resources");
}

function addTeamMember() {
  teamForm.members.push({
    rowKey: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    sortOrder: teamForm.members.length,
    isEnabled: true
  });
}

function removeTeamMember(index: number) {
  teamForm.members.splice(index, 1);
}

function addPluginBinding() {
  pluginBindings.value.push({
    rowId: `binding-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    sortOrder: pluginBindings.value.length,
    isEnabled: true,
    toolConfigJson: "{}"
  });
}

function removePluginBinding(index: number) {
  pluginBindings.value.splice(index, 1);
}

function parseKnowledgeBaseIds() {
  return knowledgeBaseInput.value
    .split(",")
    .map((item) => item.trim())
    .filter((item) => /^\d+$/.test(item));
}

async function loadBaseOptions() {
  const [models, plugins, agents] = await Promise.all([
    getEnabledModelConfigs(),
    getAiPluginsPaged({ pageIndex: 1, pageSize: 50 }),
    getAgentsPaged({ pageIndex: 1, pageSize: 100 })
  ]);

  modelConfigs.value = models;
  pluginSource.value = plugins.items;
  agentOptions.value = agents.items.map((item) => ({
    label: item.name,
    value: String(item.id)
  }));
}

async function loadSingleAgent() {
  const [detail, publications] = await Promise.all([
    getAgentById(editorId.value),
    getAgentPublications(editorId.value)
  ]);

  Object.assign(singleForm, {
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
  pluginBindings.value = (detail.pluginBindings || []).map((binding, index) => ({
    rowId: `${binding.pluginId}-${index}`,
    pluginId: binding.pluginId,
    sortOrder: binding.sortOrder,
    isEnabled: binding.isEnabled,
    toolConfigJson: binding.toolConfigJson || "{}"
  }));
  if (pluginBindings.value.length === 0) {
    addPluginBinding();
  }
  publicationItems.value = publications;
}

async function loadTeamAgent() {
  const detail = await getMultiAgentOrchestrationById(teamSourceId.value);
  teamForm.name = detail.name;
  teamForm.description = detail.description || "";
  teamForm.members = detail.members.map((member, index) => ({
    rowKey: `${member.agentId}-${index}`,
    agentId: String(member.agentId),
    alias: member.alias,
    promptPrefix: member.promptPrefix,
    sortOrder: member.sortOrder,
    isEnabled: member.isEnabled
  }));
  if (teamForm.members.length === 0) {
    addTeamMember();
  }

  const metadata = loadTeamAgentMetadata(teamSourceId.value, detail.name, detail.description);
  Object.assign(teamMetadata, metadata);
  schemaPrompt.value = detail.description || "";
}

async function loadEditor() {
  try {
    await loadBaseOptions();
    if (isTeamEditor.value) {
      await loadTeamAgent();
    } else {
      await loadSingleAgent();
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.loadAgentFailed"));
  }
}

async function handleSave() {
  saving.value = true;
  try {
    if (isTeamEditor.value) {
      const members = teamForm.members
        .filter((member) => member.agentId)
        .map((member, index) => ({
          agentId: String(member.agentId),
          alias: member.alias?.trim() || undefined,
          promptPrefix: member.promptPrefix?.trim() || undefined,
          sortOrder: member.sortOrder ?? index,
          isEnabled: member.isEnabled
        }));

      await updateMultiAgentOrchestration(teamSourceId.value, {
        name: teamForm.name.trim(),
        description: teamForm.description.trim() || undefined,
        mode: toOrchestrationMode(teamMetadata.mode),
        members
      });
      saveTeamAgentMetadata(teamSourceId.value, JSON.parse(JSON.stringify(teamMetadata)) as TeamAgentMetadata);
    } else {
      await updateAgent(editorId.value, {
        name: singleForm.name.trim(),
        description: singleForm.description.trim() || undefined,
        avatarUrl: singleForm.avatarUrl.trim() || undefined,
        systemPrompt: singleForm.systemPrompt.trim() || undefined,
        modelConfigId: singleForm.modelConfigId,
        modelName: singleForm.modelName.trim() || undefined,
        temperature: singleForm.temperature,
        maxTokens: singleForm.maxTokens,
        enableMemory: singleForm.enableMemory,
        enableShortTermMemory: singleForm.enableShortTermMemory,
        enableLongTermMemory: singleForm.enableLongTermMemory,
        longTermMemoryTopK: singleForm.longTermMemoryTopK,
        knowledgeBaseIds: parseKnowledgeBaseIds(),
        pluginBindings: pluginBindings.value
          .filter((binding) => binding.pluginId)
          .map((binding) => ({
            pluginId: String(binding.pluginId),
            sortOrder: binding.sortOrder,
            isEnabled: binding.isEnabled,
            toolConfigJson: binding.toolConfigJson || "{}"
          }))
      });
    }

    message.success(t("ai.agent.saveSuccess"));
    await loadEditor();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.submitFailed"));
  } finally {
    saving.value = false;
  }
}

async function handlePublish() {
  if (isTeamEditor.value) {
    return;
  }

  publishing.value = true;
  try {
    await publishAgent(editorId.value);
    message.success(t("ai.agent.publishSuccess"));
    await loadSingleAgent();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.publishFailed"));
  } finally {
    publishing.value = false;
  }
}

async function handlePublishPublication() {
  if (isTeamEditor.value) {
    return;
  }

  publicationLoading.value = true;
  try {
    await publishAgentPublication(editorId.value, publicationNote.value || undefined);
    publicationItems.value = await getAgentPublications(editorId.value);
    publicationNote.value = "";
    message.success(t("ai.agent.pubPublishSuccess"));
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.pubPublishFailed"));
  } finally {
    publicationLoading.value = false;
  }
}

async function handleRefreshEmbedToken() {
  if (isTeamEditor.value) {
    return;
  }

  tokenRefreshing.value = true;
  try {
    await regenerateAgentEmbedToken(editorId.value);
    publicationItems.value = await getAgentPublications(editorId.value);
    message.success(t("ai.agent.pubTokenRefreshSuccess"));
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.pubTokenRefreshFailed"));
  } finally {
    tokenRefreshing.value = false;
  }
}

async function handleRollbackPublication(targetVersion: number) {
  if (isTeamEditor.value) {
    return;
  }

  rollbackTarget.value = targetVersion;
  try {
    await rollbackAgentPublication(editorId.value, targetVersion, publicationNote.value || undefined);
    publicationItems.value = await getAgentPublications(editorId.value);
    message.success(t("ai.agent.pubRollbackSuccess"));
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.pubRollbackFailed"));
  } finally {
    rollbackTarget.value = null;
  }
}

function generateSchemaDraft() {
  if (!schemaPrompt.value.trim()) {
    message.warning(t("ai.agent.schemaPromptRequired"));
    return;
  }

  if (!isTeamEditor.value) {
    goChatWithSchema();
    return;
  }

  teamMetadata.schemaDraft = buildSchemaDraftFromPrompt(schemaPrompt.value.trim());
  teamMetadata.capabilityFlags.schemaBuilder = true;
  saveTeamAgentMetadata(teamSourceId.value, JSON.parse(JSON.stringify(teamMetadata)) as TeamAgentMetadata);
  message.success(t("ai.agent.schemaDraftCreated"));
}

function handleTeamExecuted() {
  if (!teamMetadata.schemaDraft && schemaPrompt.value.trim()) {
    teamMetadata.schemaDraft = buildSchemaDraftFromPrompt(schemaPrompt.value.trim());
    saveTeamAgentMetadata(teamSourceId.value, JSON.parse(JSON.stringify(teamMetadata)) as TeamAgentMetadata);
  }
}

onMounted(() => {
  void loadEditor();
});
</script>

<style scoped>
.agent-editor-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
}

.back-btn {
  padding-left: 0;
}

.editor-title-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.editor-title {
  margin: 0;
  font-size: 28px;
  color: #0f172a;
}

.editor-subtitle {
  margin: 8px 0 0;
  color: rgba(0, 0, 0, 0.45);
}

.editor-body {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(380px, 0.8fr);
  gap: 20px;
}

.editor-canvas-section,
.editor-side-section {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.panel-card {
  border-radius: 20px;
}

.panel-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 18px;
}

.panel-title {
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.panel-subtitle {
  margin-top: 4px;
  color: rgba(0, 0, 0, 0.45);
}

.team-canvas,
.single-canvas {
  display: flex;
  flex-direction: column;
  gap: 18px;
  min-height: 260px;
  padding: 18px;
  border-radius: 18px;
  background:
    radial-gradient(circle at top right, rgba(59, 130, 246, 0.12), transparent 35%),
    linear-gradient(180deg, #f8fbff, #f8fafc);
}

.team-canvas__lead {
  display: flex;
  justify-content: center;
}

.team-canvas__links {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.team-canvas__member {
  display: flex;
  align-items: stretch;
  gap: 10px;
}

.team-canvas__line {
  width: 20px;
  min-width: 20px;
  border-left: 2px dashed #93c5fd;
  border-bottom: 2px dashed #93c5fd;
  border-bottom-left-radius: 10px;
}

.canvas-node {
  flex: 1;
  padding: 16px;
  border: 1px solid #dbeafe;
  border-radius: 16px;
  background: #fff;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.05);
}

.canvas-node--lead {
  max-width: 420px;
  border-color: #93c5fd;
  background: linear-gradient(180deg, #eff6ff, #ffffff);
}

.canvas-node--single {
  max-width: none;
}

.canvas-node__eyebrow {
  color: #1d4ed8;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
}

.canvas-node__title {
  margin-top: 10px;
  font-size: 18px;
  font-weight: 700;
  color: #0f172a;
}

.canvas-node__desc {
  margin-top: 10px;
  color: rgba(0, 0, 0, 0.56);
  line-height: 1.6;
  white-space: pre-wrap;
}

.single-canvas__facts {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.fact-chip,
.footer-chip {
  padding: 10px 14px;
  border-radius: 999px;
  background: #fff;
  color: rgba(0, 0, 0, 0.65);
}

.fact-chip strong,
.footer-chip {
  color: #0f172a;
}

.canvas-footer {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 16px;
}

.publish-toolbar,
.schema-actions {
  margin-bottom: 12px;
}

.publish-note {
  margin-bottom: 16px;
}

.member-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.member-card {
  padding: 14px;
  border: 1px solid #edf2f7;
  border-radius: 16px;
  background: #f8fafc;
}

.member-card__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  font-weight: 600;
}

.member-card__field {
  margin-top: 10px;
}

.schema-flags {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 12px;
}

.schema-draft {
  margin-top: 16px;
  padding: 16px;
  border-radius: 16px;
  background: #f8fafc;
}

.schema-draft__title {
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.schema-draft__summary {
  margin-top: 8px;
  color: rgba(0, 0, 0, 0.56);
}

.schema-draft__section {
  margin-top: 14px;
}

.schema-draft__label {
  margin-bottom: 8px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
  text-transform: uppercase;
}

.schema-draft__list {
  margin: 0;
  padding-left: 18px;
  color: rgba(0, 0, 0, 0.65);
}

@media (max-width: 1200px) {
  .editor-body {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 768px) {
  .editor-header {
    flex-direction: column;
  }

  .team-canvas__links {
    grid-template-columns: 1fr;
  }
}
</style>
