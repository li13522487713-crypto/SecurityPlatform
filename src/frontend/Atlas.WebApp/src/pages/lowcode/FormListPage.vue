<template>
  <div class="form-list-page">
    <div class="page-header">
      <div class="page-header-left">
        <h2 class="page-header-title">{{ t("lowcode.formList.title") }}</h2>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('lowcode.formList.phSearch')"
          allow-clear
          style="width: 260px"
          @search="handleSearch"
        />
        <a-select
          v-model:value="categoryFilter"
          :placeholder="t('lowcode.formList.phCategory')"
          allow-clear
          style="width: 160px"
          @change="handleSearch"
        >
          <a-select-option value="人事类">{{ t("lowcode.formList.catHr") }}</a-select-option>
          <a-select-option value="财务类">{{ t("lowcode.formList.catFinance") }}</a-select-option>
          <a-select-option value="采购类">{{ t("lowcode.formList.catPurchase") }}</a-select-option>
          <a-select-option value="通用">{{ t("lowcode.formList.catGeneral") }}</a-select-option>
        </a-select>
      </div>
      <div class="page-header-right">
        <a-button v-if="canManageApps" type="primary" @click="handleCreate">{{ t("lowcode.formList.newForm") }}</a-button>
      </div>
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
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">
            {{ statusLabel(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canManageApps" type="link" @click="handleEdit(record.id)">{{ t("lowcode.formList.design") }}</a-button>
            <a-button
              v-if="canManageApps && record.status === 'Draft'"
              type="link"
              @click="handlePublish(record.id)"
            >{{ t("lowcode.formList.publish") }}</a-button>
            <a-button
              v-if="canManageApps && record.status === 'Published'"
              type="link"
              @click="handleDisable(record.id)"
            >{{ t("lowcode.formList.disable") }}</a-button>
            <a-button
              v-if="canManageApps && record.status === 'Disabled'"
              type="link"
              @click="handleEnable(record.id)"
            >{{ t("lowcode.formList.enable") }}</a-button>
            <a-popconfirm
              v-if="canManageApps"
              :title="t('lowcode.formList.deleteConfirm')"
              :ok-text="t('lowcode.formList.okDelete')"
              :cancel-text="t('common.cancel')"
              @confirm="handleDelete(record.id)"
            >
              <a-button type="link" danger>{{ t("lowcode.formList.okDelete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="createModalVisible"
      :title="t('lowcode.formList.modalCreateTitle')"
      :ok-text="t('lowcode.formList.okCreate')"
      :cancel-text="t('common.cancel')"
      @ok="handleCreateSubmit"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('lowcode.formList.labelName')" required>
          <a-input v-model:value="createForm.name" :placeholder="t('lowcode.formList.phName')" />
        </a-form-item>
        <a-form-item :label="t('lowcode.formList.labelCategory')">
          <a-select v-model:value="createForm.category" :placeholder="t('lowcode.formList.phSelectCat')" allow-clear>
            <a-select-option value="人事类">{{ t("lowcode.formList.catHr") }}</a-select-option>
            <a-select-option value="财务类">{{ t("lowcode.formList.catFinance") }}</a-select-option>
            <a-select-option value="采购类">{{ t("lowcode.formList.catPurchase") }}</a-select-option>
            <a-select-option value="通用">{{ t("lowcode.formList.catGeneral") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcode.formList.labelDesc')">
          <a-textarea v-model:value="createForm.description" :rows="3" :placeholder="t('lowcode.formList.phDesc')" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useRouter } from "vue-router";
import type { FormDefinitionListItem } from "@/types/lowcode";
import { getAuthProfile, hasPermission } from "@/utils/auth";
import {
  getFormDefinitionsPaged,
  createFormDefinition,
  publishFormDefinition,
  disableFormDefinition,
  enableFormDefinition,
  deleteFormDefinition
} from "@/services/lowcode";

const { t } = useI18n();
const router = useRouter();
const canManageApps = hasPermission(getAuthProfile(), "apps:update");

const keyword = ref("");
const categoryFilter = ref<string | undefined>(undefined);
const loading = ref(false);
const dataSource = ref<FormDefinitionListItem[]>([]);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total: number) => t("crud.totalItems", { total })
});

const createModalVisible = ref(false);
const createForm = reactive({
  name: "",
  category: undefined as string | undefined,
  description: ""
});

const columns = computed(() => [
  { title: t("lowcode.formList.colName"), dataIndex: "name", key: "name" },
  { title: t("lowcode.formList.colCategory"), dataIndex: "category", key: "category", width: 120 },
  { title: t("lowcode.formList.colVersion"), dataIndex: "version", key: "version", width: 80 },
  { title: t("lowcode.formList.colStatus"), key: "status", width: 100 },
  { title: t("lowcode.formList.colUpdated"), dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: t("lowcode.formList.colActions"), key: "actions", width: 260 }
]);

const statusColor = (status: string) => {
  const map: Record<string, string> = {
    Draft: "default",
    Published: "green",
    Disabled: "red",
    Archived: "gray"
  };
  return map[status] ?? "default";
};

const statusLabel = (status: string) => {
  const map: Record<string, string> = {
    Draft: t("lowcode.formList.stDraft"),
    Published: t("lowcode.formList.stPublished"),
    Disabled: t("lowcode.formList.stDisabled"),
    Archived: t("lowcode.formList.stArchived")
  };
  return map[status] ?? status;
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getFormDefinitionsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined,
      category: categoryFilter.value
    });

    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formList.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const handleCreate = () => {
  if (!canManageApps) {
    message.warning(t("lowcode.formList.noEditPerm"));
    return;
  }
  createForm.name = "";
  createForm.category = undefined;
  createForm.description = "";
  createModalVisible.value = true;
};

const handleCreateSubmit = async () => {
  if (!createForm.name.trim()) {
    message.warning(t("lowcode.formList.warnName"));
    return;
  }

  try {
    const defaultSchema = JSON.stringify({
      type: "page",
      title: createForm.name,
      body: [
        {
          type: "form",
          title: "",
          body: []
        }
      ]
    });

    const result = await createFormDefinition({
      name: createForm.name,
      description: createForm.description || undefined,
      category: createForm.category,
      schemaJson: defaultSchema
    });

    if (!isMounted.value) return;

    createModalVisible.value = false;
    message.success(t("lowcode.formList.createOk"));
    router.push({ name: "apps-form-designer", params: { id: result.id } });
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formList.createFailed"));
  }
};

const handleEdit = (id: string) => {
  if (!canManageApps) {
    message.warning(t("lowcode.formList.noDesignPerm"));
    return;
  }
  router.push({ name: "apps-form-designer", params: { id } });
};

const handlePublish = async (id: string) => {
  try {
    await publishFormDefinition(id);

    if (!isMounted.value) return;
    message.success(t("lowcode.formList.publishOk"));
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formList.publishFailed"));
  }
};

const handleDisable = async (id: string) => {
  try {
    await disableFormDefinition(id);

    if (!isMounted.value) return;
    message.success(t("lowcode.formList.disabledOk"));
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formList.disableFailed"));
  }
};

const handleEnable = async (id: string) => {
  try {
    await enableFormDefinition(id);

    if (!isMounted.value) return;
    message.success(t("lowcode.formList.enabledOk"));
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formList.enableFailed"));
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteFormDefinition(id);

    if (!isMounted.value) return;
    message.success(t("lowcode.formList.deleteOk"));
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formList.deleteFailed"));
  }
};

onMounted(() => {
  fetchData();
});
</script>

<style scoped>
.form-list-page {
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.page-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.page-header-left h2 {
  margin: 0;
  font-size: 20px;
}
</style>
