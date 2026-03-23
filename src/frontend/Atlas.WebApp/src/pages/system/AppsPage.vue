<template>
  <a-card :title="t('systemAppsConfig.pageTitle')" class="page-card">
    <div class="toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          :placeholder="t('systemAppsConfig.searchPlaceholder')"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-button @click="handleSearch">{{ t("systemAppsConfig.query") }}</a-button>
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
      </a-space>
      <TableViewToolbar :controller="tableViewController" />
    </div>

    <a-table
      :columns="tableColumns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      :size="tableSize"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isActive'">
          <a-tag v-if="record.isActive" color="green">{{ t("systemAppsConfig.tagEnabled") }}</a-tag>
          <a-tag v-else color="red">{{ t("systemAppsConfig.tagDisabled") }}</a-tag>
        </template>
        <template v-if="column.key === 'enableProjectScope'">
          <a-tag v-if="record.enableProjectScope" color="blue">{{ t("systemAppsConfig.tagProjectOn") }}</a-tag>
          <a-tag v-else>{{ t("systemAppsConfig.tagProjectOff") }}</a-tag>
        </template>
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("systemAppsConfig.edit") }}</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="formVisible"
      :title="t('systemAppsConfig.drawerTitle')"
      placement="right"
      width="520"
      destroy-on-close
      @close="closeForm"
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item :label="t('systemAppsConfig.labelAppId')">
          <a-input v-model:value="formModel.appId" disabled />
        </a-form-item>
        <a-form-item :label="t('systemAppsConfig.labelName')" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item :label="t('systemAppsConfig.labelStatus')">
          <a-switch
            v-model:checked="formModel.isActive"
            :checked-children="t('common.statusEnabled')"
            :un-checked-children="t('common.statusDisabled')"
          />
        </a-form-item>
        <a-form-item :label="t('systemAppsConfig.labelProjectScope')">
          <a-switch
            v-model:checked="formModel.enableProjectScope"
            :checked-children="t('systemAppsConfig.tagProjectOn')"
            :un-checked-children="t('systemAppsConfig.tagProjectOff')"
          />
        </a-form-item>
        <a-form-item :label="t('systemAppsConfig.labelSort')">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item :label="t('systemAppsConfig.labelDescription')" name="description">
          <a-input v-model:value="formModel.description" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="closeForm">{{ t("common.cancel") }}</a-button>
          <a-button type="primary" @click="submitForm">{{ t("common.save") }}</a-button>
        </a-space>
      </template>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useTableView } from "@/composables/useTableView";
import { getAppConfigsPaged, updateAppConfig } from "@/services/api";
import type { AppConfigListItem, AppConfigUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

const { t } = useI18n();

const baseColumns = computed(() => [
  { title: t("systemAppsConfig.colAppId"), dataIndex: "appId", key: "appId" },
  { title: t("systemAppsConfig.colName"), dataIndex: "name", key: "name" },
  { title: t("systemAppsConfig.colStatus"), key: "isActive" },
  { title: t("systemAppsConfig.colProjectScope"), key: "enableProjectScope" },
  { title: t("systemAppsConfig.colSort"), dataIndex: "sortOrder", key: "sortOrder" },
  { title: t("systemAppsConfig.colActions"), key: "actions", view: { canHide: false } }
]);

const dataSource = ref<AppConfigListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => t("crud.totalItems", { total })
});

const formVisible = ref(false);
const formRef = ref<FormInstance>();
const formModel = reactive<AppConfigUpdateRequest & { id: string; appId: string }>({
  id: "",
  appId: "",
  name: "",
  isActive: true,
  enableProjectScope: false,
  description: "",
  sortOrder: 0
});

const formRules = computed<Record<string, Rule[]>>(() => ({
  name: [{ required: true, message: t("systemAppsConfig.nameRequired") }]
}));

const profile = getAuthProfile();
const canUpdate = hasPermission(profile, "apps:update");

const fetchData = async () => {
  loading.value = true;
  try {
    const result  = await getAppConfigsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });
    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || t("systemAppsConfig.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const { controller: tableViewController, tableColumns, tableSize } = useTableView<AppConfigListItem>({
  tableKey: "system.apps",
  columns: baseColumns,
  pagination,
  onRefresh: fetchData
});

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const handleReset = () => {
  keyword.value = "";
  handleSearch();
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const openEdit = (record: AppConfigListItem) => {
  formModel.id = record.id;
  formModel.appId = record.appId;
  formModel.name = record.name;
  formModel.isActive = record.isActive;
  formModel.enableProjectScope = record.enableProjectScope;
  formModel.description = record.description ?? "";
  formModel.sortOrder = record.sortOrder;
  formVisible.value = true;
};

const closeForm = () => {
  formVisible.value = false;
};

const submitForm = async () => {
  const valid  = await formRef.value?.validate().catch(() => false);
  if (!isMounted.value) return;
  if (!valid) return;

  try {
    const previousScope = dataSource.value.find((item) => item.id === formModel.id)?.enableProjectScope;
    await updateAppConfig(formModel.id, {
      name: formModel.name,
      isActive: formModel.isActive,
      enableProjectScope: formModel.enableProjectScope,
      description: formModel.description || undefined,
      sortOrder: formModel.sortOrder
    });
    if (!isMounted.value) return;
    message.success(t("systemAppsConfig.updateSuccess"));
    if (previousScope !== formModel.enableProjectScope) {
      window.dispatchEvent(new CustomEvent("app-config-changed"));
    }
    formVisible.value = false;
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("systemAppsConfig.updateFailed"));
  }
};

onMounted(fetchData);
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
</style>
