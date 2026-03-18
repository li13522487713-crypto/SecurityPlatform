<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('systemPositions.pageTitle')"
    :search-placeholder="t('systemPositions.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? t('systemPositions.drawerCreateTitle') : t('systemPositions.drawerEditTitle')"
    :drawer-width="520"
    :submit-loading="submitting"
    :submit-disabled="submitting"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="resetFilters"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" @click="openCreate">{{ t("systemPositions.addPosition") }}</a-button>
    </template>
    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
    </template>

    <template #table>
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
          <template v-if="column.key === 'status'">
            <a-tag :color="record.isActive ? 'green' : 'red'">
              {{ record.isActive ? t("common.statusEnabled") : t("common.statusDisabled") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'system'">
            <a-tag :color="record.isSystem ? 'blue' : 'default'">
              {{ record.isSystem ? t("systemPositions.systemBuiltIn") : t("systemPositions.customType") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
              <a-popconfirm
                v-if="canDelete && !record.isSystem"
                :title="t('systemPositions.deleteConfirm')"
                :ok-text="t('common.delete')"
                :cancel-text="t('common.cancel')"
                @confirm="handleDelete(record.id)"
              >
                <a-button type="link" danger>{{ t("common.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item :label="t('systemPositions.positionName')" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.code')" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
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
import { computed, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useCrudPage } from "@/composables/useCrudPage";
import {
  createPosition,
  deletePosition,
  getPositionDetail,
  getPositionsPaged,
  updatePosition
} from "@/services/api";
import type { PositionListItem, PositionDetail, PositionCreateRequest, PositionUpdateRequest } from "@/types/api";

const { t } = useI18n();
const formRef = ref<FormInstance>();
const tableColumnsDef = computed(() => ([
  { title: t("systemPositions.colPositionName"), dataIndex: "name", key: "name" },
  { title: t("systemPositions.colCode"), dataIndex: "code", key: "code" },
  { title: t("systemPositions.colDescription"), dataIndex: "description", key: "description" },
  { title: t("systemPositions.colStatus"), key: "status" },
  { title: t("systemPositions.colType"), key: "system" },
  { title: t("systemPositions.colSortOrder"), dataIndex: "sortOrder", key: "sortOrder" },
  { title: t("systemPositions.colActions"), key: "actions", view: { canHide: false } }
]));

const {
  dataSource,
  loading,
  keyword,
  pagination,
  formVisible,
  formMode,
  submitting,
  formModel,
  formRules,
  tableViewController,
  tableColumns,
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
} = useCrudPage<PositionListItem, PositionDetail, PositionCreateRequest, PositionUpdateRequest>({
  tableKey: "system.positions",
  columns: tableColumnsDef,
  permissions: {
    create: "positions:create",
    update: "positions:update",
    delete: "positions:delete"
  },
  api: {
    list: getPositionsPaged,
    detail: getPositionDetail,
    create: createPosition,
    update: updatePosition,
    delete: deletePosition
  },
  formRef,
  defaultFormModel: () => ({
    name: "",
    code: "",
    description: "",
    isActive: true,
    sortOrder: 0
  }),
  formRules: {
    name: [{ required: true, message: t("systemPositions.nameRequired") }],
    code: [{ required: true, message: t("systemPositions.codeRequired") }]
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
  mapDetailToForm: (detail, model) => {
    model.name = detail.name;
    model.code = detail.code;
    model.description = detail.description ?? "";
    model.isActive = detail.isActive;
    model.sortOrder = detail.sortOrder;
  }
});
</script>
