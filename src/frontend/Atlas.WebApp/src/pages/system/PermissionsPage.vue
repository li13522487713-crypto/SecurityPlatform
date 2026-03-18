<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('systemPermissions.pageTitle')"
    :search-placeholder="t('systemPermissions.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? t('systemPermissions.drawerCreateTitle') : t('systemPermissions.drawerEditTitle')"
    :drawer-width="520"
    :submit-loading="submitting"
    :submit-disabled="submitting"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" @click="openCreate">{{ t("systemPermissions.addPermission") }}</a-button>
    </template>
    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
      <a-button :disabled="!selectedRowKeys.length" @click="handleBatchCopy">{{ t("systemPermissions.batchCopyCodes") }}</a-button>
    </template>

    <template #filter>
      <a-select
        v-model:value="typeFilter"
        :options="typeFilterOptions"
        style="width: 160px"
        @change="handleSearch"
      />
    </template>

    <template #table>
      <a-table
        :columns="tableColumns"
        :data-source="dataSource"
        :pagination="pagination"
        :loading="loading"
        :row-selection="rowSelection"
        :size="tableSize"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item :label="t('systemPermissions.permissionName')" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item :label="t('systemPermissions.permissionCode')" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item :label="t('systemPermissions.type')" name="type">
          <a-select v-model:value="formModel.type" :options="typeOptions" />
        </a-form-item>
        <a-form-item :label="t('systemPermissions.description')" name="description">
          <a-input v-model:value="formModel.description" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useCrudPage } from "@/composables/useCrudPage";
import { createPermission, getPermissionsPaged, updatePermission } from "@/services/api";
import type { PermissionListItem, PermissionCreateRequest, PermissionUpdateRequest } from "@/types/api";

const { t } = useI18n();
const typeOptions = computed(() => ([
  { label: "Api", value: "Api" },
  { label: "Menu", value: "Menu" },
  { label: "Application", value: "Application" },
  { label: "Page", value: "Page" },
  { label: "Action", value: "Action" }
]));
const typeFilter = ref<"all" | "Api" | "Menu" | "Application" | "Page" | "Action">("all");
const typeFilterOptions = computed(() => ([
  { label: t("systemPermissions.filterAllTypes"), value: "all" },
  { label: "Api", value: "Api" },
  { label: "Menu", value: "Menu" },
  { label: "Application", value: "Application" },
  { label: "Page", value: "Page" },
  { label: "Action", value: "Action" }
]));

const selectedRowKeys = ref<string[]>([]);
const selectedRows = ref<PermissionListItem[]>([]);

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: (string | number)[], rows: PermissionListItem[]) => {
    selectedRowKeys.value = keys.map((key) => key.toString());
    selectedRows.value = rows;
  }
}));

const formRef = ref<FormInstance>();
const tableColumnsDef = computed(() => ([
  { title: t("systemPermissions.colPermissionName"), dataIndex: "name", key: "name" },
  { title: t("systemPermissions.colPermissionCode"), dataIndex: "code", key: "code" },
  { title: t("systemPermissions.colType"), dataIndex: "type", key: "type" },
  { title: t("systemPermissions.colDescription"), dataIndex: "description", key: "description" },
  { title: t("systemPermissions.colActions"), key: "actions", view: { canHide: false } }
]));

const crud = useCrudPage<PermissionListItem, PermissionListItem, PermissionCreateRequest, PermissionUpdateRequest>({
  tableKey: "system.permissions",
  columns: tableColumnsDef,
  permissions: {
    create: "permissions:create",
    update: "permissions:update"
  },
  api: {
    list: getPermissionsPaged,
    create: createPermission,
    update: updatePermission
  },
  formRef,
  defaultFormModel: () => ({
    name: "",
    code: "",
    type: "Api",
    description: ""
  }),
  formRules: {
    name: [{ required: true, message: t("systemPermissions.nameRequired") }],
    code: [{ required: true, message: t("systemPermissions.codeRequired") }],
    type: [{ required: true, message: t("systemPermissions.typeRequired") }]
  },
  buildListParams: (base) => ({
    ...base,
    type: typeFilter.value === "all" ? undefined : typeFilter.value
  }),
  buildCreatePayload: (model) => ({
    name: model.name,
    code: model.code,
    type: model.type,
    description: model.description || undefined
  }),
  buildUpdatePayload: (model) => ({
    name: model.name,
    type: model.type,
    description: model.description || undefined
  }),
  mapRecordToForm: (record, model) => {
    model.name = record.name;
    model.code = record.code;
    model.type = record.type;
    model.description = record.description ?? "";
  }
});

const {
  dataSource, loading, keyword, pagination,
  formVisible, formMode, submitting, formModel, formRules,
  tableViewController, tableColumns, tableSize,
  canCreate, canUpdate,
  onTableChange, openCreate, openEdit, closeForm, submitForm
} = crud;

const handleSearch = () => {
  selectedRowKeys.value = [];
  selectedRows.value = [];
  crud.handleSearch();
};

const handleReset = () => {
  typeFilter.value = "all";
  selectedRowKeys.value = [];
  selectedRows.value = [];
  crud.keyword.value = "";
  crud.handleSearch();
};

const handleBatchCopy = async () => {
  if (!selectedRows.value.length) {
    message.warning(t("systemPermissions.selectPermissionWarning"));
    return;
  }
  const content = selectedRows.value.map((item) => item.code).join("\n");
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(content);
      message.success(t("systemPermissions.copySuccess"));
      return;
    }
    throw new Error("Clipboard API not available");
  } catch {
    message.warning(t("systemPermissions.copyNotSupported"));
    message.info(content);
  }
};
</script>
