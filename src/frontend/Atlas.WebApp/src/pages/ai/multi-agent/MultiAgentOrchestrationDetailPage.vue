<template>
  <a-card :title="t('ai.multiAgent.detailTitle')" :bordered="false">
    <template #extra>
      <a-space>
        <a-button @click="goBack">{{ t("common.cancel") }}</a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">
          {{ t("common.save") }}
        </a-button>
      </a-space>
    </template>

    <a-spin :spinning="loading">
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item :label="t('ai.multiAgent.formName')" name="name">
              <a-input v-model:value="form.name" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('ai.multiAgent.formMode')" name="mode">
              <a-radio-group v-model:value="form.mode">
                <a-radio :value="0">{{ t("ai.multiAgent.modeSequential") }}</a-radio>
                <a-radio :value="1">{{ t("ai.multiAgent.modeParallel") }}</a-radio>
              </a-radio-group>
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('ai.multiAgent.formStatus')" name="status">
              <a-select v-model:value="form.status" :options="statusOptions" />
            </a-form-item>
          </a-col>
        </a-row>

        <a-form-item :label="t('ai.multiAgent.formDescription')" name="description">
          <a-textarea v-model:value="form.description" :rows="2" />
        </a-form-item>

        <a-divider />
        <div class="member-header">
          <span>{{ t("ai.multiAgent.memberList") }}</span>
          <a-button type="dashed" size="small" @click="addMember">
            {{ t("ai.multiAgent.addMember") }}
          </a-button>
        </div>
        <a-table
          row-key="rowKey"
          size="small"
          :columns="memberColumns"
          :data-source="form.members"
          :pagination="false"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'agentId'">
              <a-select
                v-model:value="record.agentId"
                show-search
                :filter-option="false"
                :options="agentOptions"
                :placeholder="t('ai.multiAgent.selectAgent')"
                style="width: 220px"
                :not-found-content="agentLoading ? t('common.loading') : undefined"
                @search="handleSearchAgents"
              />
            </template>
            <template v-else-if="column.key === 'alias'">
              <a-input v-model:value="record.alias" :placeholder="t('ai.multiAgent.aliasPlaceholder')" />
            </template>
            <template v-else-if="column.key === 'sortOrder'">
              <a-input-number v-model:value="record.sortOrder" :min="0" style="width: 100%" />
            </template>
            <template v-else-if="column.key === 'isEnabled'">
              <a-switch v-model:checked="record.isEnabled" />
            </template>
            <template v-else-if="column.key === 'promptPrefix'">
              <a-input v-model:value="record.promptPrefix" :placeholder="t('ai.multiAgent.promptPrefixPlaceholder')" />
            </template>
            <template v-else-if="column.key === 'action'">
              <a-button type="link" danger @click="removeMember(index)">
                {{ t("common.delete") }}
              </a-button>
            </template>
          </template>
        </a-table>
      </a-form>
    </a-spin>
  </a-card>

  <a-card style="margin-top: 16px" :bordered="false">
    <MultiAgentRunPanel :orchestration-id="orchestrationId" />
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import {
  getMultiAgentOrchestrationById,
  updateMultiAgentOrchestration,
  type MultiAgentOrchestrationStatus
} from "@/services/api-multi-agent";
import { getAgentsPaged } from "@/services/api-agent";
import MultiAgentRunPanel from "@/components/multi-agent/MultiAgentRunPanel.vue";
import { resolveCurrentAppId } from "@/utils/app-context";

interface MemberDraft {
  rowKey: string;
  agentId?: string;
  alias?: string;
  sortOrder: number;
  isEnabled: boolean;
  promptPrefix?: string;
}

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const orchestrationId = computed(() => Number(route.params.id || 0));
const loading = ref(false);
const saving = ref(false);
const formRef = ref<FormInstance>();

const form = reactive({
  name: "",
  description: "",
  mode: 0 as 0 | 1,
  status: 0 as MultiAgentOrchestrationStatus,
  members: [] as MemberDraft[]
});

const rules = computed(() => ({
  name: [{ required: true, message: t("ai.multiAgent.ruleName") }]
}));

const statusOptions = computed(() => [
  { label: t("ai.multiAgent.statusDraft"), value: 0 },
  { label: t("ai.multiAgent.statusActive"), value: 1 },
  { label: t("ai.multiAgent.statusDisabled"), value: 2 }
]);

const memberColumns = computed(() => [
  { title: t("ai.multiAgent.memberAgent"), dataIndex: "agentId", key: "agentId", width: 240 },
  { title: t("ai.multiAgent.memberAlias"), dataIndex: "alias", key: "alias", width: 140 },
  { title: t("ai.multiAgent.memberSort"), dataIndex: "sortOrder", key: "sortOrder", width: 100 },
  { title: t("ai.multiAgent.memberEnabled"), dataIndex: "isEnabled", key: "isEnabled", width: 100 },
  { title: t("ai.multiAgent.memberPromptPrefix"), dataIndex: "promptPrefix", key: "promptPrefix" },
  { title: t("ai.colActions"), key: "action", width: 100 }
]);

const agentOptions = ref<Array<{ label: string; value: string }>>([]);
const agentLoading = ref(false);

async function loadData() {
  if (!orchestrationId.value) {
    return;
  }

  loading.value = true;
  try {
    const detail = await getMultiAgentOrchestrationById(orchestrationId.value);
    form.name = detail.name;
    form.description = detail.description || "";
    form.mode = detail.mode;
    form.status = detail.status;
    form.members = detail.members.map((member, index) => ({
      rowKey: `${member.agentId}-${index}`,
      agentId: member.agentId,
      alias: member.alias,
      sortOrder: member.sortOrder,
      isEnabled: member.isEnabled,
      promptPrefix: member.promptPrefix
    }));
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.multiAgent.loadDetailFailed"));
  } finally {
    loading.value = false;
  }
}

async function handleSearchAgents(searchText: string) {
  agentLoading.value = true;
  try {
    const result = await getAgentsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: searchText || undefined
    });
    agentOptions.value = result.items.map((item) => ({
      label: `${item.name} (#${item.id})`,
      value: item.id
    }));
  } catch {
    agentOptions.value = [];
  } finally {
    agentLoading.value = false;
  }
}

function addMember() {
  form.members.push({
    rowKey: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    sortOrder: form.members.length,
    isEnabled: true
  });
}

function removeMember(index: number) {
  form.members.splice(index, 1);
}

async function handleSave() {
  if (!orchestrationId.value) {
    return;
  }

  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  const members = form.members
    .filter((member) => member.agentId)
    .map((member, index) => ({
      agentId: member.agentId as string,
      alias: member.alias?.trim() || undefined,
      sortOrder: member.sortOrder ?? index,
      isEnabled: member.isEnabled,
      promptPrefix: member.promptPrefix?.trim() || undefined
    }));

  if (!members.some((item) => item.isEnabled)) {
    message.warning(t("ai.multiAgent.memberRequired"));
    return;
  }

  saving.value = true;
  try {
    await updateMultiAgentOrchestration(orchestrationId.value, {
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      mode: form.mode,
      status: form.status,
      members
    });
    message.success(t("crud.updateSuccess"));
    await loadData();
  } catch (err: unknown) {
    message.error((err as Error).message || t("crud.submitFailed"));
  } finally {
    saving.value = false;
  }
}

function goBack() {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/multi-agent`);
    return;
  }

  void router.push("/ai/multi-agent");
}

onMounted(() => {
  void handleSearchAgents("");
  void loadData();
});
</script>

<style scoped>
.member-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  font-weight: 500;
}
</style>
