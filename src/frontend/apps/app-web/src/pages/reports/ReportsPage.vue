<template>
  <a-card :title="t('reports.pageTitle')" class="page-card">
    <template #extra>
      <a-button type="primary" @click="handleCreate">{{ t("common.create") }}</a-button>
    </template>

    <div class="toolbar">
      <a-input
        v-model:value="keyword"
        :placeholder="t('reports.searchPlaceholder')"
        allow-clear
        style="width: 240px"
        @press-enter="handleSearch"
      />
      <a-button @click="handleSearch">{{ t("reports.search") }}</a-button>
      <a-button @click="handleReset">{{ t("reports.reset") }}</a-button>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'createdAt'">{{ formatTime(record.createdAt) }}</template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleEdit(record)">{{ t("common.edit") }}</a-button>
            <a-popconfirm :title="t('reports.deleteConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>{{ t("common.delete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="editingId ? t('reports.editTitle') : t('reports.createTitle')"
      :confirm-loading="saving"
      @ok="handleSave"
    >
      <a-form :model="formState" layout="vertical">
        <a-form-item :label="t('reports.colName')" required>
          <a-input v-model:value="formState.name" />
        </a-form-item>
        <a-form-item :label="t('reports.colDescription')">
          <a-textarea v-model:value="formState.description" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  createReport,
  deleteReport,
  getReportsPaged,
  updateReport,
  type ReportItem,
} from "@/services/api-reports";

const { t, locale } = useI18n();
const route = useRoute();
const appKey = computed(() => String(route.params.appKey ?? ""));

const columns = computed(() => [
  { title: t("reports.colName"), dataIndex: "name", key: "name" },
  { title: t("reports.colDescription"), dataIndex: "description", key: "description" },
  { title: t("reports.colCreatedAt"), key: "createdAt", width: 180 },
  { title: t("common.actions"), key: "action", width: 160 },
]);

const keyword = ref("");
const dataSource = ref<ReportItem[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showTotal: (total: number) => String(total),
});

const modalVisible = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const formState = reactive({ name: "", description: "" });

const formatTime = (iso: string) => {
  const language = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(iso).toLocaleString(language, { hour12: false });
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getReportsPaged(appKey.value, {
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      keyword: keyword.value.trim() || undefined,
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || t("reports.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => {
  pagination.current = 1;
  void fetchData();
};

const handleReset = () => {
  keyword.value = "";
  handleSearch();
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  void fetchData();
};

const handleCreate = () => {
  editingId.value = null;
  formState.name = "";
  formState.description = "";
  modalVisible.value = true;
};

const handleEdit = (record: ReportItem) => {
  editingId.value = record.id;
  formState.name = record.name;
  formState.description = record.description || "";
  modalVisible.value = true;
};

const handleSave = async () => {
  if (!formState.name.trim()) {
    message.warning(t("reports.nameRequired"));
    return;
  }
  saving.value = true;
  try {
    if (editingId.value) {
      await updateReport(appKey.value, editingId.value, {
        name: formState.name.trim(),
        description: formState.description.trim() || undefined,
      });
    } else {
      await createReport(appKey.value, {
        name: formState.name.trim(),
        description: formState.description.trim() || undefined,
      });
    }
    modalVisible.value = false;
    message.success(t("reports.saveSuccess"));
    await fetchData();
  } catch (error) {
    message.error((error as Error).message || t("reports.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteReport(appKey.value, id);
    message.success(t("reports.deleteSuccess"));
    await fetchData();
  } catch (error) {
    message.error((error as Error).message || t("reports.deleteFailed"));
  }
};

onMounted(() => {
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
