<template>
  <a-card :title="t('dashboards.pageTitle')" class="page-card" data-testid="app-dashboards-page">
    <template #extra>
      <a-button type="primary" data-testid="app-dashboards-create" @click="handleCreate">{{ t("common.create") }}</a-button>
    </template>

    <div class="toolbar">
      <a-input
        v-model:value="keyword"
        :placeholder="t('dashboards.searchPlaceholder')"
        allow-clear
        style="width: 240px"
        @press-enter="handleSearch"
      />
      <a-button @click="handleSearch">{{ t("common.search") }}</a-button>
      <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
    </div>

    <a-table
      data-testid="app-dashboards-table"
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isDefault'">
          <a-tag :color="record.isDefault ? 'blue' : 'default'">
            {{ record.isDefault ? t("dashboards.defaultTag") : "-" }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'createdAt'">{{ formatTime(record.createdAt) }}</template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" :data-testid="`app-dashboards-edit-${record.id}`" @click="handleEdit(record)">{{ t("common.edit") }}</a-button>
            <a-popconfirm :title="t('dashboards.deleteConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger :data-testid="`app-dashboards-delete-${record.id}`">{{ t("common.delete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="editingId ? t('dashboards.editTitle') : t('dashboards.createTitle')"
      :confirm-loading="saving"
      @ok="handleSave"
    >
      <a-form :model="formState" layout="vertical">
        <a-form-item :label="t('dashboards.colName')" required>
          <a-input v-model:value="formState.name" data-testid="app-dashboards-form-name" />
        </a-form-item>
        <a-form-item :label="t('dashboards.colDescription')">
          <a-textarea v-model:value="formState.description" :rows="3" />
        </a-form-item>
        <a-form-item>
          <a-checkbox v-model:checked="formState.isDefault">{{ t("dashboards.setDefault") }}</a-checkbox>
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
  createDashboard,
  deleteDashboard,
  getDashboardsPaged,
  updateDashboard,
  type DashboardItem,
} from "@/services/api-reports";

const { t, locale } = useI18n();
const route = useRoute();
const appKey = computed(() => String(route.params.appKey ?? ""));

const columns = computed(() => [
  { title: t("dashboards.colName"), dataIndex: "name", key: "name" },
  { title: t("dashboards.colDescription"), dataIndex: "description", key: "description" },
  { title: t("dashboards.colDefault"), key: "isDefault", width: 130 },
  { title: t("dashboards.colCreatedAt"), key: "createdAt", width: 180 },
  { title: t("common.actions"), key: "action", width: 160 },
]);

const keyword = ref("");
const dataSource = ref<DashboardItem[]>([]);
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
const formState = reactive({ name: "", description: "", isDefault: false });
const defaultDashboardLayoutJson = JSON.stringify({
  type: "grid",
  widgets: []
});

const formatTime = (iso: string) => {
  const language = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(iso).toLocaleString(language, { hour12: false });
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getDashboardsPaged(appKey.value, {
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      keyword: keyword.value.trim() || undefined,
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || t("dashboards.loadFailed"));
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
  formState.isDefault = false;
  modalVisible.value = true;
};

const handleEdit = (record: DashboardItem) => {
  editingId.value = record.id;
  formState.name = record.name;
  formState.description = record.description || "";
  formState.isDefault = record.isDefault;
  modalVisible.value = true;
};

const handleSave = async () => {
  if (!formState.name.trim()) {
    message.warning(t("dashboards.nameRequired"));
    return;
  }
  saving.value = true;
  try {
    if (editingId.value) {
      await updateDashboard(appKey.value, editingId.value, {
        name: formState.name.trim(),
        description: formState.description.trim(),
        category: "e2e",
        layoutJson: defaultDashboardLayoutJson,
        isDefault: formState.isDefault,
        isLargeScreen: true,
        canvasWidth: 1920,
        canvasHeight: 1080,
        themeJson: "",
      });
    } else {
      await createDashboard(appKey.value, {
        name: formState.name.trim(),
        description: formState.description.trim(),
        category: "e2e",
        layoutJson: defaultDashboardLayoutJson,
        isDefault: formState.isDefault,
        isLargeScreen: true,
        canvasWidth: 1920,
        canvasHeight: 1080,
        themeJson: "",
      });
    }
    modalVisible.value = false;
    message.success(t("dashboards.saveSuccess"));
    await fetchData();
  } catch (error) {
    message.error((error as Error).message || t("dashboards.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteDashboard(appKey.value, id);
    message.success(t("dashboards.deleteSuccess"));
    await fetchData();
  } catch (error) {
    message.error((error as Error).message || t("dashboards.deleteFailed"));
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
