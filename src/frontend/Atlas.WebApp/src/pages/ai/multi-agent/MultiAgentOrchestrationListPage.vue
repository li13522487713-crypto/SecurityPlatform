<template>
  <a-card :title="t('ai.multiAgent.listTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.multiAgent.searchPlaceholder')"
          style="width: 260px"
          @search="loadData"
        />
        <a-button type="primary" @click="openCreate">
          {{ t("ai.multiAgent.newOrchestration") }}
        </a-button>
      </a-space>
    </div>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="rows"
      :loading="loading"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'mode'">
          <a-tag :color="record.mode === 0 ? 'blue' : 'purple'">
            {{ record.mode === 0 ? t("ai.multiAgent.modeSequential") : t("ai.multiAgent.modeParallel") }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">
            {{ statusText(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goDetail(record.id)">{{ t("common.edit") }}</a-button>
            <a-popconfirm :title="t('ai.multiAgent.deleteConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" danger>{{ t("common.delete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <div class="pager">
      <a-pagination
        v-model:current="pageIndex"
        v-model:page-size="pageSize"
        :total="total"
        show-size-changer
        :page-size-options="['10', '20', '50']"
        @change="loadData"
      />
    </div>

    <a-modal
      v-model:open="createModalVisible"
      :title="t('ai.multiAgent.createTitle')"
      width="980px"
      :confirm-loading="createLoading"
      @ok="handleCreate"
      @cancel="closeCreateModal"
    >
      <a-form ref="formRef" :model="createForm" layout="vertical" :rules="rules">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('ai.multiAgent.formName')" name="name">
              <a-input v-model:value="createForm.name" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('ai.multiAgent.formMode')" name="mode">
              <a-radio-group v-model:value="createForm.mode">
                <a-radio :value="0">{{ t("ai.multiAgent.modeSequential") }}</a-radio>
                <a-radio :value="1">{{ t("ai.multiAgent.modeParallel") }}</a-radio>
              </a-radio-group>
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item :label="t('ai.multiAgent.formDescription')" name="description">
          <a-textarea v-model:value="createForm.description" :rows="2" />
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
          :data-source="createForm.members"
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
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import {
  createMultiAgentOrchestration,
  deleteMultiAgentOrchestration,
  getMultiAgentOrchestrationsPaged,
  type MultiAgentOrchestrationStatus
} from "@/services/api-multi-agent";
import { getAgentsPaged } from "@/services/api-agent";
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

const rows = ref<Awaited<ReturnType<typeof getMultiAgentOrchestrationsPaged>>["items"]>([]);
const loading = ref(false);
const keyword = ref("");
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = computed(() => [
  { title: t("ai.multiAgent.colName"), dataIndex: "name", key: "name" },
  { title: t("ai.multiAgent.colMode"), dataIndex: "mode", key: "mode", width: 130 },
  { title: t("ai.multiAgent.colStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("ai.multiAgent.colMembers"), dataIndex: "memberCount", key: "memberCount", width: 120 },
  { title: t("ai.multiAgent.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 220 },
  { title: t("ai.colActions"), key: "action", width: 180 }
]);

const memberColumns = computed(() => [
  { title: t("ai.multiAgent.memberAgent"), dataIndex: "agentId", key: "agentId", width: 240 },
  { title: t("ai.multiAgent.memberAlias"), dataIndex: "alias", key: "alias", width: 140 },
  { title: t("ai.multiAgent.memberSort"), dataIndex: "sortOrder", key: "sortOrder", width: 100 },
  { title: t("ai.multiAgent.memberEnabled"), dataIndex: "isEnabled", key: "isEnabled", width: 100 },
  { title: t("ai.multiAgent.memberPromptPrefix"), dataIndex: "promptPrefix", key: "promptPrefix" },
  { title: t("ai.colActions"), key: "action", width: 100 }
]);

const createModalVisible = ref(false);
const createLoading = ref(false);
const formRef = ref<FormInstance>();
const createForm = reactive({
  name: "",
  description: "",
  mode: 0 as 0 | 1,
  members: [] as MemberDraft[]
});

const rules = computed(() => ({
  name: [{ required: true, message: t("ai.multiAgent.ruleName") }]
}));

const agentOptions = ref<Array<{ label: string; value: string }>>([]);
const agentLoading = ref(false);

function statusText(status: MultiAgentOrchestrationStatus) {
  if (status === 1) return t("ai.multiAgent.statusActive");
  if (status === 2) return t("ai.multiAgent.statusDisabled");
  return t("ai.multiAgent.statusDraft");
}

function statusColor(status: MultiAgentOrchestrationStatus) {
  if (status === 1) return "green";
  if (status === 2) return "orange";
  return "default";
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getMultiAgentOrchestrationsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined
    });
    rows.value = result.items;
    total.value = Number(result.total);
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.multiAgent.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function goDetail(id: number) {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/multi-agent/${id}`);
    return;
  }

  void router.push(`/ai/multi-agent/${id}`);
}

function openCreate() {
  createForm.name = "";
  createForm.description = "";
  createForm.mode = 0;
  createForm.members = [];
  addMember();
  createModalVisible.value = true;
}

function closeCreateModal() {
  createModalVisible.value = false;
}

function addMember() {
  createForm.members.push({
    rowKey: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    sortOrder: createForm.members.length,
    isEnabled: true
  });
}

function removeMember(index: number) {
  createForm.members.splice(index, 1);
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

async function handleCreate() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  const members = createForm.members
    .filter((item) => item.agentId)
    .map((item, index) => ({
      agentId: item.agentId as string,
      alias: item.alias?.trim() || undefined,
      sortOrder: item.sortOrder ?? index,
      isEnabled: item.isEnabled,
      promptPrefix: item.promptPrefix?.trim() || undefined
    }));

  if (!members.some((item) => item.isEnabled)) {
    message.warning(t("ai.multiAgent.memberRequired"));
    return;
  }

  createLoading.value = true;
  try {
    const id = await createMultiAgentOrchestration({
      name: createForm.name.trim(),
      description: createForm.description.trim() || undefined,
      mode: createForm.mode,
      members
    });
    message.success(t("crud.createSuccess"));
    createModalVisible.value = false;
    await loadData();
    if (id) {
      goDetail(Number(id));
    }
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.multiAgent.createFailed"));
  } finally {
    createLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteMultiAgentOrchestration(id);
    message.success(t("crud.deleteSuccess"));
    await loadData();
  } catch (err: unknown) {
    message.error((err as Error).message || t("crud.deleteFailed"));
  }
}

onMounted(() => {
  void loadData();
  void handleSearchAgents("");
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}

.member-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  font-weight: 500;
}
</style>
