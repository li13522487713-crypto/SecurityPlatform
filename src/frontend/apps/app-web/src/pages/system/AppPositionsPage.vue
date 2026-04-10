<template>
  <CrudPageLayout
    data-testid="app-positions-page"
    v-model:keyword="keyword"
    :title="t('systemPositions.pageTitle')"
    :subtitle="t('systemPositions.pageSubtitle')"
    :search-placeholder="t('systemPositions.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'edit' ? t('systemPositions.drawerEditTitle') : t('systemPositions.drawerCreateTitle')"
    :drawer-width="520"
    :submit-loading="submitting"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="resetFilters"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" data-testid="app-positions-create" @click="openCreate">
        <template #icon><PlusOutlined /></template>
        {{ t("systemPositions.addPosition") }}
      </a-button>
    </template>
    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
    </template>

    <template #table>
      <a-table
        data-testid="app-positions-table"
        :columns="tableColumns"
        :data-source="dataSource"
        :loading="loading"
        :pagination="pagination"
        :size="tableSize"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'name'">
            <span style="font-weight: 600">{{ record.name }}</span>
          </template>
          <template v-else-if="column.key === 'code'">
            <a-tag color="default" class="app-positions-code-tag">{{ record.code }}</a-tag>
          </template>
          <template v-else-if="column.key === 'description'">
            <span class="app-positions-desc-ellipsis" :title="record.description || undefined">{{ record.description || "-" }}</span>
          </template>
          <template v-else-if="column.key === 'isActive'">
            <a-tag :color="record.isActive ? 'green' : 'red'">
              {{ record.isActive ? t("common.statusEnabled") : t("common.statusDisabled") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button v-if="canUpdate" type="link" size="small" :data-testid="`app-positions-edit-${record.id}`" @click="openEdit(record)">
                {{ t("common.edit") }}
              </a-button>
              <a-popconfirm
                v-if="canDelete"
                :title="t('systemPositions.deleteConfirm')"
                    @confirm="handleDelete(record.id)"
              >
                <a-button type="link" size="small" danger :data-testid="`app-positions-delete-${record.id}`">{{ t("common.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formModel" :rules="rules" layout="vertical">
        <a-form-item :label="t('systemPositions.positionName')" name="name">
          <a-input v-model:value="formModel.name" data-testid="app-positions-form-name" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.code')" name="code">
          <a-input v-model:value="formModel.code" data-testid="app-positions-form-code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.description')" name="description">
          <a-textarea v-model:value="formModel.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.status')" name="isActive">
          <a-switch v-model:checked="formModel.isActive" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.sortOrder')" name="sortOrder">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { PlusOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import { CrudPageLayout, TableViewToolbar } from "@atlas/shared-ui";
import type { Rule } from "ant-design-vue/es/form";
import { type AppPositionListItem } from "@/types/organization";
import { APP_PERMISSIONS } from "@/constants/permissions";
import { useAppCrudPage } from "@/composables/useAppCrudPage";
import {
  getPositionsPaged,
  getPositionDetail,
  createPosition,
  updatePosition,
  deletePosition,
} from "@/services/api-org-management";

const { t } = useI18n();
const columns = computed(() => [
  { title: t("systemPositions.colPositionName"), dataIndex: "name", key: "name" },
  { title: t("systemPositions.colCode"), dataIndex: "code", key: "code" },
  { title: t("systemPositions.colDescription"), dataIndex: "description", key: "description", ellipsis: true },
  { title: t("systemPositions.colStatus"), dataIndex: "isActive", key: "isActive", width: 100 },
  { title: t("systemPositions.colSortOrder"), dataIndex: "sortOrder", key: "sortOrder", width: 80 },
  { title: t("systemPositions.colActions"), key: "action", width: 150, fixed: "right" as const }
]);

interface PositionFormModel {
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

interface PositionUpdatePayload {
  name: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

const rules: Record<string, Rule[]> = {
  name: [{ required: true, message: t("systemPositions.nameRequired"), trigger: "blur" as const }],
  code: [{ required: true, message: t("systemPositions.codeRequired"), trigger: "blur" as const }]
};

const {
  keyword,
  dataSource,
  loading,
  pagination,
  formVisible,
  formMode,
  formRef,
  formModel,
  submitting,
  tableColumns,
  tableViewController,
  tableSize,
  canCreate,
  canUpdate,
  canDelete,
  onTableChange,
  handleSearch,
  resetFilters,
  openCreate,
  openEdit,
  closeForm,
  submitForm,
  handleDelete
} = useAppCrudPage<AppPositionListItem, AppPositionListItem, PositionFormModel, PositionUpdatePayload>({
  tableKey: "app.positions",
  columns,
  permissions: {
    create: APP_PERMISSIONS.APP_POSITIONS_UPDATE,
    update: APP_PERMISSIONS.APP_POSITIONS_UPDATE,
    delete: APP_PERMISSIONS.APP_POSITIONS_UPDATE
  },
  appApi: {
    list: (appId, params) => getPositionsPaged(appId, params),
    detail: (appId, id) => getPositionDetail(appId, id),
    create: (appId, data) => createPosition(appId, data),
    update: (appId, id, data) => updatePosition(appId, id, data),
    delete: (appId, id) => deletePosition(appId, id)
  },
  defaultFormModel: () => ({
    name: "",
    code: "",
    description: "",
    isActive: true,
    sortOrder: 0
  }),
  formRules: rules,
  buildListParams: (base) => ({
    keyword: base.keyword,
    pageIndex: base.pageIndex,
    pageSize: base.pageSize
  }),
  mapDetailToForm: (detail, model) => {
    model.name = detail.name;
    model.code = detail.code;
    model.description = detail.description ?? "";
    model.isActive = detail.isActive;
    model.sortOrder = detail.sortOrder;
  },
  buildCreatePayload: (model) => ({
    name: model.name,
    code: model.code,
    description: model.description || undefined,
    isActive: model.isActive,
    sortOrder: model.sortOrder
  }),
  buildUpdatePayload: (model) => ({
    name: model.name,
    description: model.description || undefined,
    isActive: model.isActive,
    sortOrder: model.sortOrder
  }),
  translate: (key, params) => String(t(key, (params ?? {}) as Record<string, string | number | boolean>))
});
</script>

<style scoped>
.app-positions-code-tag {
  margin: 0;
  border: none;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
  background: var(--ant-color-fill-secondary, #f5f5f5);
}

.app-positions-desc-ellipsis {
  display: block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--ant-color-text-secondary, rgba(0, 0, 0, 0.45));
}
</style>
