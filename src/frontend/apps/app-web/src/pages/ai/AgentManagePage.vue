<template>
  <a-card :title="t('ai.agentMgmt.title')" class="page-card" data-testid="app-agent-mgmt-page">
    <template #extra>
      <a-button type="primary" @click="openCreate">{{ t("common.create") }}</a-button>
    </template>

    <div class="toolbar">
      <a-input
        v-model:value="keyword"
        :placeholder="t('ai.agentMgmt.searchPlaceholder')"
        allow-clear
        style="width: 260px"
        @press-enter="handleSearch"
      />
      <a-button @click="handleSearch">{{ t("common.search") }}</a-button>
      <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :loading="loading"
      :pagination="pagination"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="record.status === 'Published' ? 'green' : 'blue'">
            {{ record.status }}
          </a-tag>
        </template>
        <template v-if="column.key === 'createdAt'">
          {{ formatTime(record.createdAt) }}
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
            <a-popconfirm :title="t('ai.agentMgmt.deleteConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>{{ t("common.delete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="editingId ? t('ai.agentMgmt.editTitle') : t('ai.agentMgmt.createTitle')"
      :confirm-loading="saving"
      @ok="handleSave"
    >
      <a-form :model="formState" layout="vertical">
        <a-form-item :label="t('ai.agentMgmt.formName')" required>
          <a-input v-model:value="formState.name" />
        </a-form-item>
        <a-form-item :label="t('ai.agentMgmt.formDescription')">
          <a-textarea v-model:value="formState.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('ai.agentMgmt.formModelConfig')">
          <a-select
            v-model:value="formState.modelConfigId"
            :options="modelOptions"
            allow-clear
            show-search
            :placeholder="t('ai.agentMgmt.formModelConfigPlaceholder')"
            :filter-option="filterModelOption"
          />
        </a-form-item>
        <a-form-item :label="t('ai.agentMgmt.formModelName')">
          <a-input v-model:value="formState.modelName" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  createAgent,
  deleteAgent,
  getAgentById,
  getAgentsPaged,
  updateAgent,
  type AgentListItem
} from "@/services/api-agent";
import { getModelConfigsPaged, type ModelConfigDto } from "@/services/api-model-config";

const { t, locale } = useI18n();

const columns = computed(() => [
  { title: t("ai.agentMgmt.colName"), dataIndex: "name", key: "name" },
  { title: t("ai.agentMgmt.colModel"), dataIndex: "modelName", key: "modelName", width: 180 },
  { title: t("ai.agentMgmt.colStatus"), key: "status", width: 120 },
  { title: t("ai.agentMgmt.colCreatedAt"), key: "createdAt", width: 180 },
  { title: t("common.actions"), key: "action", width: 140 },
]);

const keyword = ref("");
const dataSource = ref<AgentListItem[]>([]);
const loading = ref(false);
const saving = ref(false);
const modalVisible = ref(false);
const editingId = ref<string | null>(null);
const modelConfigs = ref<ModelConfigDto[]>([]);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showTotal: (total: number) => t("crud.totalItems", { total }),
});

const formState = reactive({
  name: "",
  description: "",
  modelConfigId: undefined as string | undefined,
  modelName: "",
});

const modelOptions = computed(() =>
  modelConfigs.value.map((item) => ({
    label: `${item.name} (${item.providerType})`,
    value: String(item.id),
  }))
);

function filterModelOption(input: string, option: { label: string; value: string }) {
  return option.label.toLowerCase().includes(input.toLowerCase());
}

function formatTime(iso?: string) {
  if (!iso) return "-";
  const currentLocale = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(iso).toLocaleString(currentLocale, { hour12: false });
}

async function fetchData() {
  loading.value = true;
  try {
    const result = await getAgentsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      keyword: keyword.value.trim() || undefined,
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
}

async function fetchModelConfigs() {
  try {
    const result = await getModelConfigsPaged({
      pageIndex: 1,
      pageSize: 100,
    });
    modelConfigs.value = result.items.filter((item) => item.isEnabled);
  } catch {
    // 模型配置加载失败不阻断 Agent 管理主流程
    modelConfigs.value = [];
  }
}

function handleSearch() {
  pagination.current = 1;
  void fetchData();
}

function handleReset() {
  keyword.value = "";
  handleSearch();
}

function onTableChange(pager: TablePaginationConfig) {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  void fetchData();
}

function openCreate() {
  editingId.value = null;
  formState.name = "";
  formState.description = "";
  formState.modelConfigId = undefined;
  formState.modelName = "";
  modalVisible.value = true;
}

async function openEdit(record: AgentListItem) {
  editingId.value = record.id;
  modalVisible.value = true;
  try {
    const detail = await getAgentById(record.id);
    formState.name = detail.name;
    formState.description = detail.description || "";
    formState.modelConfigId = detail.modelConfigId ? String(detail.modelConfigId) : undefined;
    formState.modelName = detail.modelName || "";
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.loadDetailFailed"));
  }
}

async function handleSave() {
  if (!formState.name.trim()) {
    message.warning(t("ai.agentMgmt.nameRequired"));
    return;
  }

  saving.value = true;
  try {
    if (editingId.value) {
      await updateAgent(editingId.value, {
        name: formState.name.trim(),
        description: formState.description.trim() || undefined,
        modelConfigId: formState.modelConfigId,
        modelName: formState.modelName.trim() || undefined,
      });
      message.success(t("crud.updateSuccess"));
    } else {
      await createAgent({
        name: formState.name.trim(),
        description: formState.description.trim() || undefined,
        modelConfigId: formState.modelConfigId,
        modelName: formState.modelName.trim() || undefined,
      });
      message.success(t("crud.createSuccess"));
    }
    modalVisible.value = false;
    await fetchData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.submitFailed"));
  } finally {
    saving.value = false;
  }
}

async function handleDelete(id: string) {
  try {
    await deleteAgent(id);
    message.success(t("crud.deleteSuccess"));
    await fetchData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

onMounted(() => {
  void fetchModelConfigs();
  void fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>
