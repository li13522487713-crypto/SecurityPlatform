<template>
  <div class="agent-workspace-page">
    <section class="workspace-hero">
      <div class="workspace-hero__main">
        <div class="workspace-hero__eyebrow">{{ t("ai.agent.workspaceEyebrow") }}</div>
        <h1 class="workspace-hero__title">{{ t("ai.agent.workspaceTitle") }}</h1>
        <p class="workspace-hero__subtitle">{{ t("ai.agent.workspaceSubtitle") }}</p>
      </div>
      <div class="workspace-hero__actions">
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.agent.searchPlaceholder')"
          class="workspace-search"
          allow-clear
          @search="loadWorkspace"
        />
        <a-button type="primary" @click="openSingleCreate">
          {{ t("ai.agent.newSingleAgent") }}
        </a-button>
      </div>
    </section>

    <section class="workspace-metrics">
      <a-card v-for="metric in metrics" :key="metric.key" class="metric-card" :bordered="false">
        <div class="metric-card__label">{{ metric.label }}</div>
        <div class="metric-card__value">{{ metric.value }}</div>
        <div class="metric-card__hint">{{ metric.hint }}</div>
      </a-card>
    </section>

    <section class="workspace-shortcuts">
      <a-card :bordered="false">
        <div class="section-header">
          <div>
            <div class="section-title">{{ t("ai.agent.quickCreateTitle") }}</div>
            <div class="section-subtitle">{{ t("ai.agent.quickCreateSubtitle") }}</div>
          </div>
        </div>
        <div class="shortcut-grid">
          <button class="shortcut-card" type="button" @click="openSingleCreate">
            <span class="shortcut-card__title">{{ t("ai.agent.newSingleAgent") }}</span>
            <span class="shortcut-card__desc">{{ t("ai.agent.quickSingleDesc") }}</span>
          </button>
          <button class="shortcut-card" type="button" @click="openTeamCreate('group_chat')">
            <span class="shortcut-card__title">{{ t("ai.agent.newTeamAgent") }}</span>
            <span class="shortcut-card__desc">{{ t("ai.agent.quickTeamDesc") }}</span>
          </button>
          <button class="shortcut-card" type="button" @click="createTemplateTeam('group_chat')">
            <span class="shortcut-card__title">{{ t("ai.agent.quickGroupChatTemplate") }}</span>
            <span class="shortcut-card__desc">{{ t("ai.agent.quickGroupChatDesc") }}</span>
          </button>
          <button class="shortcut-card shortcut-card--accent" type="button" @click="createTemplateTeam('schema_builder')">
            <span class="shortcut-card__title">{{ t("ai.agent.quickSchemaBuilder") }}</span>
            <span class="shortcut-card__desc">{{ t("ai.agent.quickSchemaBuilderDesc") }}</span>
          </button>
        </div>
      </a-card>
    </section>

    <section class="workspace-filters">
      <a-card :bordered="false">
        <div class="filter-row">
          <a-segmented v-model:value="typeFilter" :options="typeFilterOptions" />
          <a-segmented v-model:value="modeFilter" :options="modeFilterOptions" />
          <a-segmented v-model:value="statusFilter" :options="statusFilterOptions" />
          <a-select v-model:value="capabilityFilter" class="capability-filter" :options="capabilityOptions" />
        </div>
      </a-card>
    </section>

    <section class="workspace-main">
      <div class="workspace-main__content">
        <a-spin :spinning="loading">
          <div v-if="filteredCards.length === 0" class="workspace-empty">
            <a-empty :description="t('ai.agent.workspaceEmpty')" />
          </div>
          <div v-else class="agent-card-grid">
            <a-card
              v-for="card in filteredCards"
              :key="card.id"
              class="agent-work-card"
              :bordered="false"
            >
              <div class="agent-work-card__header">
                <div>
                  <div class="agent-work-card__type">
                    <a-tag color="blue">
                      {{ card.agentType === "team" ? t("ai.agent.agentTypeTeam") : t("ai.agent.agentTypeSingle") }}
                    </a-tag>
                    <a-tag v-if="card.teamMode" :color="teamModeColor(card.teamMode)">
                      {{ teamModeText(card.teamMode) }}
                    </a-tag>
                    <a-tag :color="statusColor(card.status)">
                      {{ statusText(card.status) }}
                    </a-tag>
                  </div>
                  <div class="agent-work-card__title">{{ card.name }}</div>
                  <div class="agent-work-card__desc">{{ card.description || t("ai.agent.noDescription") }}</div>
                </div>
                <a-avatar-group>
                  <a-avatar
                    v-for="member in card.memberNames.slice(0, 3)"
                    :key="member"
                    size="small"
                  >
                    {{ member.slice(0, 1) }}
                  </a-avatar>
                  <a-avatar v-if="card.memberCount > 3" size="small">
                    +{{ card.memberCount - 3 }}
                  </a-avatar>
                </a-avatar-group>
              </div>

              <div class="agent-work-card__meta">
                <div class="agent-work-card__meta-item">
                  <span>{{ t("ai.agent.cardMembers") }}</span>
                  <strong>{{ card.memberCount }}</strong>
                </div>
                <div class="agent-work-card__meta-item">
                  <span>{{ t("ai.agent.cardVersion") }}</span>
                  <strong>v{{ card.publishedVersion }}</strong>
                </div>
                <div class="agent-work-card__meta-item">
                  <span>{{ t("ai.agent.cardUpdatedAt") }}</span>
                  <strong>{{ formatDate(card.updatedAt) }}</strong>
                </div>
              </div>

              <div class="agent-work-card__tags">
                <a-tag v-for="tag in card.capabilityTags" :key="`${card.id}-${tag}`">
                  {{ capabilityText(tag) }}
                </a-tag>
              </div>

              <div class="agent-work-card__members">
                <span class="agent-work-card__label">{{ t("ai.agent.cardMemberRoles") }}</span>
                <span>{{ formatMemberNames(card.memberNames, card.memberCount) }}</span>
              </div>

              <div class="agent-work-card__actions">
                <a-button type="link" size="small" @click="goEdit(card)">{{ t("common.edit") }}</a-button>
                <a-button type="link" size="small" @click="goChat(card)">{{ t("ai.agent.actionChat") }}</a-button>
                <a-button type="link" size="small" @click="handleRun(card)">{{ t("ai.agent.actionRun") }}</a-button>
                <a-button type="link" size="small" @click="handlePublish(card)">{{ t("ai.agent.actionPublish") }}</a-button>
                <a-dropdown>
                  <a-button type="text" size="small">{{ t("ai.agent.actionMore") }}</a-button>
                  <template #overlay>
                    <a-menu>
                      <a-menu-item @click="handleSchemaBuild(card)">{{ t("ai.agent.actionSchemaBuild") }}</a-menu-item>
                      <a-menu-item @click="handleDuplicate(card)">{{ t("ai.agent.duplicate") }}</a-menu-item>
                      <a-menu-item danger @click="handleDelete(card)">{{ t("common.delete") }}</a-menu-item>
                    </a-menu>
                  </template>
                </a-dropdown>
              </div>
            </a-card>
          </div>
        </a-spin>
      </div>

      <aside class="workspace-main__aside">
        <a-card :bordered="false">
          <div class="section-header">
            <div>
              <div class="section-title">{{ t("ai.agent.activityTitle") }}</div>
              <div class="section-subtitle">{{ t("ai.agent.activitySubtitle") }}</div>
            </div>
          </div>
          <a-timeline class="activity-timeline">
            <a-timeline-item v-for="activity in activities" :key="activity.id">
              <div class="activity-item__title">{{ activityText(activity.title, activity.agentName) }}</div>
              <div class="activity-item__time">{{ formatDateTime(activity.updatedAt) }}</div>
            </a-timeline-item>
          </a-timeline>
        </a-card>
      </aside>
    </section>

    <a-modal
      v-model:open="singleModalVisible"
      :title="t('ai.agent.modalCreateTitle')"
      :confirm-loading="singleModalLoading"
      @ok="submitSingleCreate"
      @cancel="closeSingleModal"
    >
      <a-form ref="singleFormRef" :model="singleForm" layout="vertical" :rules="singleRules">
        <a-form-item :label="t('ai.promptLib.colName')" name="name">
          <a-input v-model:value="singleForm.name" />
        </a-form-item>
        <a-form-item :label="t('ai.promptLib.labelDescription')" name="description">
          <a-textarea v-model:value="singleForm.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('ai.agent.labelModelConfig')">
          <a-select
            v-model:value="singleForm.modelConfigId"
            allow-clear
            show-search
            :options="modelOptions"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="teamModalVisible"
      :title="t('ai.agent.teamCreateTitle')"
      width="920px"
      :confirm-loading="teamModalLoading"
      @ok="submitTeamCreate"
      @cancel="closeTeamModal"
    >
      <a-form ref="teamFormRef" :model="teamForm" layout="vertical" :rules="teamRules">
        <a-row :gutter="16">
          <a-col :span="10">
            <a-form-item :label="t('ai.agent.formTeamName')" name="name">
              <a-input v-model:value="teamForm.name" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('ai.agent.formTeamMode')" name="mode">
              <a-select v-model:value="teamForm.mode" :options="teamModeSelectOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="6">
            <a-form-item :label="t('ai.agent.formTemplate')" name="template">
              <a-select v-model:value="teamForm.template" :options="teamTemplateOptions" @change="applyTemplateSelection" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item :label="t('ai.promptLib.labelDescription')">
          <a-textarea v-model:value="teamForm.description" :rows="2" />
        </a-form-item>
        <div class="team-members__header">
          <span>{{ t("ai.agent.teamMembersTitle") }}</span>
          <a-button type="dashed" size="small" @click="addTeamMember">
            {{ t("ai.agent.addTeamMember") }}
          </a-button>
        </div>
        <a-table
          row-key="rowKey"
          size="small"
          :columns="teamMemberColumns"
          :data-source="teamForm.members"
          :pagination="false"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'agentId'">
              <a-select
                v-model:value="record.agentId"
                show-search
                :filter-option="false"
                :options="agentSelectOptions"
                style="width: 220px"
              />
            </template>
            <template v-else-if="column.key === 'alias'">
              <a-input v-model:value="record.alias" />
            </template>
            <template v-else-if="column.key === 'promptPrefix'">
              <a-input v-model:value="record.promptPrefix" />
            </template>
            <template v-else-if="column.key === 'sortOrder'">
              <a-input-number v-model:value="record.sortOrder" :min="0" style="width: 100%" />
            </template>
            <template v-else-if="column.key === 'isEnabled'">
              <a-switch v-model:checked="record.isEnabled" />
            </template>
            <template v-else-if="column.key === 'action'">
              <a-button type="link" danger @click="removeTeamMember(index)">
                {{ t("common.delete") }}
              </a-button>
            </template>
          </template>
        </a-table>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue";
import {
  createAgent,
  deleteAgent,
  duplicateAgent,
  getAgentsPaged,
  publishAgent
} from "@/services/api-agent";
import {
  createMultiAgentOrchestration,
  deleteMultiAgentOrchestration,
  getMultiAgentOrchestrationById,
  getMultiAgentOrchestrationsPaged,
  updateMultiAgentOrchestration
} from "@/services/api-multi-agent";
import { getEnabledModelConfigs, type ModelConfigDto } from "@/services/api-model-config";
import {
  buildWorkspaceActivities,
  buildWorkspaceAgentCards,
  getDefaultTeamAgentMetadata,
  saveTeamAgentMetadata,
  type WorkspaceAgentCard,
  type WorkspaceAgentCapability,
  type WorkspaceTeamMode
} from "@/services/agent-workspace";

interface TeamMemberDraft {
  rowKey: string;
  agentId?: string;
  alias?: string;
  promptPrefix?: string;
  sortOrder: number;
  isEnabled: boolean;
}

type TeamTemplateType = "blank" | "schema_builder" | "group_chat" | "security_ops" | "review";

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();

const loading = ref(false);
const keyword = ref("");
const typeFilter = ref<"all" | "single" | "team">("all");
const modeFilter = ref<"all" | WorkspaceTeamMode>("all");
const statusFilter = ref<"all" | "Draft" | "Published" | "Disabled">("all");
const capabilityFilter = ref<"all" | WorkspaceAgentCapability>("all");

const workspaceCards = ref<WorkspaceAgentCard[]>([]);
const activities = ref<Array<{ id: string; title: string; updatedAt: string; agentName: string }>>([]);
const agentsSource = ref<Awaited<ReturnType<typeof getAgentsPaged>>["items"]>([]);
const modelConfigs = ref<ModelConfigDto[]>([]);

const singleModalVisible = ref(false);
const singleModalLoading = ref(false);
const singleFormRef = ref<FormInstance>();
const singleForm = reactive({
  name: "",
  description: "",
  modelConfigId: undefined as string | undefined
});

const teamModalVisible = ref(false);
const teamModalLoading = ref(false);
const teamFormRef = ref<FormInstance>();
const teamForm = reactive({
  name: "",
  description: "",
  mode: "group_chat" as WorkspaceTeamMode,
  template: "blank" as TeamTemplateType,
  members: [] as TeamMemberDraft[]
});

const singleRules = computed(() => ({
  name: [{ required: true, message: t("ai.agent.ruleName") }]
}));

const teamRules = computed(() => ({
  name: [{ required: true, message: t("ai.agent.ruleName") }]
}));

const modelOptions = computed(() =>
  modelConfigs.value.map((item) => ({
    label: `${item.name} (${item.providerType})`,
    value: item.id
  }))
);

const agentSelectOptions = computed(() =>
  agentsSource.value.map((item) => ({
    label: `${item.name} (#${item.id})`,
    value: item.id
  }))
);

const metrics = computed(() => {
  const cards = workspaceCards.value;
  const teamCards = cards.filter((item) => item.agentType === "team");
  const schemaCards = cards.filter((item) => item.capabilityTags.includes("schema_builder"));
  return [
    { key: "total", label: t("ai.agent.metricTotal"), value: cards.length, hint: t("ai.agent.metricTotalHint") },
    { key: "team", label: t("ai.agent.metricTeam"), value: teamCards.length, hint: t("ai.agent.metricTeamHint") },
    {
      key: "member",
      label: t("ai.agent.metricSubAgent"),
      value: teamCards.reduce((sum, item) => sum + item.memberCount, 0),
      hint: t("ai.agent.metricSubAgentHint")
    },
    { key: "run", label: t("ai.agent.metricRecentRuns"), value: activities.value.length, hint: t("ai.agent.metricRecentRunsHint") },
    { key: "schema", label: t("ai.agent.metricSchema"), value: schemaCards.length, hint: t("ai.agent.metricSchemaHint") }
  ];
});

const filteredCards = computed(() =>
  workspaceCards.value.filter((card) => {
    if (typeFilter.value !== "all" && card.agentType !== typeFilter.value) {
      return false;
    }

    if (modeFilter.value !== "all" && card.teamMode !== modeFilter.value) {
      return false;
    }

    if (statusFilter.value !== "all" && card.status !== statusFilter.value) {
      return false;
    }

    if (capabilityFilter.value !== "all" && !card.capabilityTags.includes(capabilityFilter.value)) {
      return false;
    }

    return true;
  })
);

const typeFilterOptions = computed(() => [
  { label: t("common.all"), value: "all" },
  { label: t("ai.agent.agentTypeSingle"), value: "single" },
  { label: t("ai.agent.agentTypeTeam"), value: "team" }
]);

const modeFilterOptions = computed(() => [
  { label: t("common.all"), value: "all" },
  { label: t("ai.agent.teamModeGroupChat"), value: "group_chat" },
  { label: t("ai.agent.teamModeWorkflow"), value: "workflow" },
  { label: t("ai.agent.teamModeHandoff"), value: "handoff" }
]);

const statusFilterOptions = computed(() => [
  { label: t("common.all"), value: "all" },
  { label: t("ai.multiAgent.statusDraft"), value: "Draft" },
  { label: t("ai.agent.statusPublished"), value: "Published" },
  { label: t("ai.agent.statusDisabled"), value: "Disabled" }
]);

const capabilityOptions = computed(() => [
  { label: t("ai.agent.filterCapabilityAll"), value: "all" },
  { label: t("ai.agent.capabilityChat"), value: "chat" },
  { label: t("ai.agent.capabilityKnowledge"), value: "knowledge" },
  { label: t("ai.agent.capabilityAutomation"), value: "automation" },
  { label: t("ai.agent.capabilitySchema"), value: "schema_builder" },
  { label: t("ai.agent.capabilityOps"), value: "ops" }
]);

const teamModeSelectOptions = computed(() => [
  { label: t("ai.agent.teamModeGroupChat"), value: "group_chat" },
  { label: t("ai.agent.teamModeWorkflow"), value: "workflow" },
  { label: t("ai.agent.teamModeHandoff"), value: "handoff" }
]);

const teamTemplateOptions = computed(() => [
  { label: t("ai.agent.templateBlank"), value: "blank" },
  { label: t("ai.agent.templateSchemaBuilder"), value: "schema_builder" },
  { label: t("ai.agent.templateGroupChat"), value: "group_chat" },
  { label: t("ai.agent.templateSecurityOps"), value: "security_ops" },
  { label: t("ai.agent.templateReview"), value: "review" }
]);

const teamMemberColumns = computed(() => [
  { title: t("ai.multiAgent.memberAgent"), dataIndex: "agentId", key: "agentId", width: 240 },
  { title: t("ai.multiAgent.memberAlias"), dataIndex: "alias", key: "alias", width: 150 },
  { title: t("ai.multiAgent.memberPromptPrefix"), dataIndex: "promptPrefix", key: "promptPrefix" },
  { title: t("ai.multiAgent.memberSort"), dataIndex: "sortOrder", key: "sortOrder", width: 100 },
  { title: t("ai.multiAgent.memberEnabled"), dataIndex: "isEnabled", key: "isEnabled", width: 100 },
  { title: t("ai.colActions"), key: "action", width: 100 }
]);

function capabilityText(tag: WorkspaceAgentCapability) {
  if (tag === "knowledge") {
    return t("ai.agent.capabilityKnowledge");
  }

  if (tag === "automation") {
    return t("ai.agent.capabilityAutomation");
  }

  if (tag === "schema_builder") {
    return t("ai.agent.capabilitySchema");
  }

  if (tag === "ops") {
    return t("ai.agent.capabilityOps");
  }

  return t("ai.agent.capabilityChat");
}

function statusText(status: string) {
  if (status === "Published") {
    return t("ai.agent.statusPublished");
  }

  if (status === "Disabled") {
    return t("ai.agent.statusDisabled");
  }

  return t("ai.multiAgent.statusDraft");
}

function statusColor(status: string) {
  if (status === "Published") {
    return "green";
  }

  if (status === "Disabled") {
    return "default";
  }

  return "blue";
}

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

function formatDate(iso: string) {
  const targetLocale = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(iso).toLocaleDateString(targetLocale, { month: "2-digit", day: "2-digit" });
}

function formatDateTime(iso: string) {
  const targetLocale = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(iso).toLocaleString(targetLocale, {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function formatMemberNames(names: string[], count: number) {
  if (names.length > 0) {
    return names.join(" / ");
  }

  return t("ai.agent.memberCountFallback", { count });
}

function activityText(type: string, name: string) {
  if (type === "schema_draft_created") {
    return t("ai.agent.activitySchemaDraft", { name });
  }

  if (type === "team_run_finished") {
    return t("ai.agent.activityTeamRun", { name });
  }

  return t("ai.agent.activityUpdated", { name });
}

async function loadWorkspace() {
  loading.value = true;
  try {
    const [agents, teams, models] = await Promise.all([
      getAgentsPaged({ pageIndex: 1, pageSize: 100, keyword: keyword.value || undefined }),
      getMultiAgentOrchestrationsPaged({ pageIndex: 1, pageSize: 100, keyword: keyword.value || undefined }),
      getEnabledModelConfigs()
    ]);

    agentsSource.value = agents.items;
    modelConfigs.value = models;
    const cards = buildWorkspaceAgentCards(agents.items, teams.items);
    const agentNameMap = new Map(agents.items.map((item) => [String(item.id), item.name]));
    const teamDetails = await Promise.all(
      teams.items.map(async (item) => {
        try {
          return await getMultiAgentOrchestrationById(item.id);
        } catch {
          return null;
        }
      })
    );
    const teamDetailMap = new Map(
      teamDetails
        .filter((item): item is NonNullable<typeof item> => item !== null)
        .map((item) => [Number(item.id), item])
    );

    workspaceCards.value = cards.map((card) => {
      if (card.agentType !== "team") {
        return card;
      }

      const detail = teamDetailMap.get(Number(card.sourceId));
      if (!detail) {
        return card;
      }

      return {
        ...card,
        memberCount: detail.members.length,
        memberNames: detail.members.map((member) => agentNameMap.get(String(member.agentId)) || member.alias || `#${member.agentId}`)
      };
    });
    activities.value = buildWorkspaceActivities(workspaceCards.value);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function openSingleCreate() {
  singleForm.name = "";
  singleForm.description = "";
  singleForm.modelConfigId = undefined;
  singleModalVisible.value = true;
}

function closeSingleModal() {
  singleModalVisible.value = false;
  singleFormRef.value?.resetFields();
}

async function submitSingleCreate() {
  try {
    await singleFormRef.value?.validate();
  } catch {
    return;
  }

  singleModalLoading.value = true;
  try {
    await createAgent({
      name: singleForm.name.trim(),
      description: singleForm.description.trim() || undefined,
      modelConfigId: singleForm.modelConfigId
    });
    message.success(t("crud.createSuccess"));
    closeSingleModal();
    await loadWorkspace();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.createFailed"));
  } finally {
    singleModalLoading.value = false;
  }
}

function openTeamCreate(mode: WorkspaceTeamMode) {
  teamForm.name = "";
  teamForm.description = "";
  teamForm.mode = mode;
  teamForm.template = "blank";
  teamForm.members = [];
  addTeamMember();
  teamModalVisible.value = true;
}

function closeTeamModal() {
  teamModalVisible.value = false;
}

function addTeamMember(seed?: Partial<TeamMemberDraft>) {
  teamForm.members.push({
    rowKey: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    sortOrder: teamForm.members.length,
    isEnabled: true,
    ...seed
  });
}

function removeTeamMember(index: number) {
  teamForm.members.splice(index, 1);
}

function applyTemplateSelection() {
  const preset = buildTemplatePreset(teamForm.template);
  teamForm.name = preset.name;
  teamForm.description = preset.description;
  teamForm.mode = preset.mode;
  teamForm.members = [];
  preset.members.forEach((member) => addTeamMember(member));
  if (teamForm.members.length === 0) {
    addTeamMember();
  }
}

function buildTemplatePreset(template: TeamTemplateType) {
  if (template === "schema_builder") {
    return {
      name: t("ai.agent.templateSchemaBuilderName"),
      description: t("ai.agent.templateSchemaBuilderDesc"),
      mode: "group_chat" as WorkspaceTeamMode,
      members: seedMembersByAliases([
        t("ai.agent.roleBusinessAnalyst"),
        t("ai.agent.roleDba"),
        t("ai.agent.roleApiDesigner"),
        t("ai.agent.roleSecurityReviewer")
      ])
    };
  }

  if (template === "group_chat") {
    return {
      name: t("ai.agent.templateGroupChatName"),
      description: t("ai.agent.templateGroupChatDesc"),
      mode: "group_chat" as WorkspaceTeamMode,
      members: seedMembersByAliases([
        t("ai.agent.roleCoordinator"),
        t("ai.agent.rolePlanner"),
        t("ai.agent.roleReviewer")
      ])
    };
  }

  if (template === "security_ops") {
    return {
      name: t("ai.agent.templateSecurityOpsName"),
      description: t("ai.agent.templateSecurityOpsDesc"),
      mode: "workflow" as WorkspaceTeamMode,
      members: seedMembersByAliases([
        t("ai.agent.roleScanner"),
        t("ai.agent.roleAnalyst"),
        t("ai.agent.roleReporter")
      ])
    };
  }

  if (template === "review") {
    return {
      name: t("ai.agent.templateReviewName"),
      description: t("ai.agent.templateReviewDesc"),
      mode: "handoff" as WorkspaceTeamMode,
      members: seedMembersByAliases([
        t("ai.agent.roleWriter"),
        t("ai.agent.roleReviewer"),
        t("ai.agent.roleApprover")
      ])
    };
  }

  return {
    name: "",
    description: "",
    mode: teamForm.mode,
    members: []
  };
}

function seedMembersByAliases(aliases: string[]) {
  return aliases.map((alias, index) => ({
    agentId: agentsSource.value[index]?.id,
    alias,
    promptPrefix: alias,
    sortOrder: index,
    isEnabled: true
  }));
}

async function submitTeamCreate() {
  try {
    await teamFormRef.value?.validate();
  } catch {
    return;
  }

  const members = teamForm.members
    .filter((member) => member.agentId)
    .map((member, index) => ({
      agentId: String(member.agentId),
      alias: member.alias?.trim() || undefined,
      promptPrefix: member.promptPrefix?.trim() || undefined,
      sortOrder: member.sortOrder ?? index,
      isEnabled: member.isEnabled
    }));

  if (members.length === 0) {
    message.warning(t("ai.multiAgent.memberRequired"));
    return;
  }

  teamModalLoading.value = true;
  try {
    const createdId = await createMultiAgentOrchestration({
      name: teamForm.name.trim(),
      description: teamForm.description.trim() || undefined,
      mode: teamForm.mode === "workflow" ? 1 : 0,
      members
    });

    if (createdId) {
      const metadata = getDefaultTeamAgentMetadata(teamForm.name, teamForm.description);
      metadata.mode = teamForm.mode;
      if (teamForm.template === "schema_builder") {
        metadata.capabilityFlags.schemaBuilder = true;
        metadata.capabilityFlags.fieldSuggestions = true;
        metadata.capabilityFlags.indexSuggestions = true;
        metadata.capabilityFlags.permissionSuggestions = true;
      }
      saveTeamAgentMetadata(Number(createdId), metadata);
    }

    message.success(t("crud.createSuccess"));
    closeTeamModal();
    await loadWorkspace();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.multiAgent.createFailed"));
  } finally {
    teamModalLoading.value = false;
  }
}

async function createTemplateTeam(template: Exclude<TeamTemplateType, "blank">) {
  const preset = buildTemplatePreset(template);
  if (preset.members.length === 0) {
    message.warning(t("ai.agent.templateRequiresMembers"));
    return;
  }

  teamForm.name = preset.name;
  teamForm.description = preset.description;
  teamForm.mode = preset.mode;
  teamForm.template = template;
  teamForm.members = [];
  preset.members.forEach((member) => addTeamMember(member));
  await submitTeamCreate();
}

function goEdit(card: WorkspaceAgentCard) {
  void router.push(`/apps/${route.params.appId}/agents/${card.id}/edit`);
}

function goChat(card: WorkspaceAgentCard, entrySkill?: string) {
  void router.push({
    path: `/apps/${route.params.appId}/agents/${card.id}/chat`,
    query: entrySkill ? { entrySkill } : undefined
  });
}

function handleRun(card: WorkspaceAgentCard) {
  goChat(card);
}

async function handlePublish(card: WorkspaceAgentCard) {
  try {
    if (card.agentType === "single") {
      await publishAgent(String(card.sourceId));
    } else {
      const detail = await getMultiAgentOrchestrationById(Number(card.sourceId));
      await updateMultiAgentOrchestration(Number(card.sourceId), {
        name: detail.name,
        description: detail.description,
        mode: detail.mode,
        members: detail.members,
        status: 1
      });
    }
    message.success(t("ai.agent.publishSuccess"));
    await loadWorkspace();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.publishFailed"));
  }
}

async function handleDuplicate(card: WorkspaceAgentCard) {
  try {
    if (card.agentType === "single") {
      await duplicateAgent(String(card.sourceId));
    } else {
      const detail = await getMultiAgentOrchestrationById(Number(card.sourceId));
      const createdId = await createMultiAgentOrchestration({
        name: `${detail.name} Copy`,
        description: detail.description,
        mode: detail.mode,
        members: detail.members
      });
      if (createdId) {
        saveTeamAgentMetadata(Number(createdId), getDefaultTeamAgentMetadata(detail.name, detail.description));
      }
    }
    message.success(t("ai.workflow.copySuccess"));
    await loadWorkspace();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.duplicateFailed"));
  }
}

async function handleDelete(card: WorkspaceAgentCard) {
  try {
    if (card.agentType === "single") {
      await deleteAgent(String(card.sourceId));
    } else {
      await deleteMultiAgentOrchestration(Number(card.sourceId));
    }
    message.success(t("crud.deleteSuccess"));
    await loadWorkspace();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

function handleSchemaBuild(card: WorkspaceAgentCard) {
  goChat(card, "schema_builder");
}

onMounted(() => {
  void loadWorkspace();
});
</script>

<style scoped>
.agent-workspace-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.workspace-hero {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  padding: 28px 32px;
  border-radius: 20px;
  background:
    radial-gradient(circle at top left, rgba(21, 101, 192, 0.26), transparent 34%),
    linear-gradient(135deg, #0f172a, #162447 56%, #1d4ed8);
  color: #fff;
}

.workspace-hero__eyebrow {
  font-size: 12px;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  opacity: 0.75;
}

.workspace-hero__title {
  margin: 8px 0 10px;
  font-size: 30px;
  line-height: 1.1;
  color: #fff;
}

.workspace-hero__subtitle {
  max-width: 720px;
  margin: 0;
  color: rgba(255, 255, 255, 0.82);
}

.workspace-hero__actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.workspace-search {
  width: 300px;
}

.workspace-metrics {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 16px;
}

.metric-card {
  border-radius: 16px;
}

.metric-card__label {
  color: rgba(0, 0, 0, 0.45);
  font-size: 13px;
}

.metric-card__value {
  margin-top: 12px;
  font-size: 32px;
  font-weight: 700;
  color: #0f172a;
}

.metric-card__hint {
  margin-top: 8px;
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
}

.section-title {
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.section-subtitle {
  margin-top: 4px;
  color: rgba(0, 0, 0, 0.45);
}

.shortcut-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}

.shortcut-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-height: 120px;
  padding: 18px;
  border: 1px solid #e5e7eb;
  border-radius: 18px;
  background: linear-gradient(180deg, #ffffff, #f8fafc);
  text-align: left;
  cursor: pointer;
  transition: transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease;
}

.shortcut-card:hover {
  transform: translateY(-2px);
  border-color: #93c5fd;
  box-shadow: 0 12px 24px rgba(15, 23, 42, 0.08);
}

.shortcut-card--accent {
  background:
    radial-gradient(circle at top right, rgba(249, 115, 22, 0.18), transparent 35%),
    linear-gradient(180deg, #fff7ed, #ffffff);
}

.shortcut-card__title {
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.shortcut-card__desc {
  color: rgba(0, 0, 0, 0.56);
  line-height: 1.5;
}

.filter-row {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.capability-filter {
  width: 180px;
}

.workspace-main {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 320px;
  gap: 20px;
}

.workspace-main__content {
  min-width: 0;
}

.workspace-empty {
  padding: 56px 0;
  border-radius: 16px;
  background: #fff;
}

.agent-card-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 18px;
}

.agent-work-card {
  border-radius: 20px;
}

.agent-work-card__header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
}

.agent-work-card__type {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.agent-work-card__title {
  margin-top: 12px;
  font-size: 20px;
  font-weight: 700;
  color: #0f172a;
}

.agent-work-card__desc {
  margin-top: 8px;
  min-height: 42px;
  color: rgba(0, 0, 0, 0.56);
  line-height: 1.6;
}

.agent-work-card__meta {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  margin-top: 18px;
  padding: 14px;
  border-radius: 14px;
  background: #f8fafc;
}

.agent-work-card__meta-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.agent-work-card__meta-item span {
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.agent-work-card__meta-item strong {
  color: #0f172a;
  font-size: 16px;
}

.agent-work-card__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 16px;
}

.agent-work-card__members {
  margin-top: 16px;
  color: rgba(0, 0, 0, 0.65);
}

.agent-work-card__label {
  display: block;
  margin-bottom: 6px;
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.agent-work-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 18px;
  padding-top: 12px;
  border-top: 1px solid #f0f0f0;
}

.activity-item__title {
  color: #0f172a;
  font-weight: 600;
}

.activity-item__time {
  margin-top: 4px;
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.team-members__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  font-weight: 600;
}

@media (max-width: 1400px) {
  .workspace-metrics {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .shortcut-grid,
  .agent-card-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 1100px) {
  .workspace-main {
    grid-template-columns: 1fr;
  }

  .workspace-hero {
    flex-direction: column;
  }

  .workspace-hero__actions {
    width: 100%;
    flex-wrap: wrap;
  }

  .workspace-search {
    width: 100%;
  }
}

@media (max-width: 768px) {
  .workspace-metrics,
  .shortcut-grid,
  .agent-card-grid {
    grid-template-columns: 1fr;
  }
}
</style>
