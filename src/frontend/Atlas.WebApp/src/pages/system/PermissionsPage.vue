<template>
  <CrudPageLayout
    title="权限管理"
    v-model:keyword="keyword"
    search-placeholder="搜索权限名称/编码"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? '新增权限' : '编辑权限'"
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
      <a-button v-if="canCreate" type="primary" @click="openCreate">新增权限</a-button>
    </template>
    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
      <a-button :disabled="!selectedRowKeys.length" @click="handleBatchCopy">批量复制编码</a-button>
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
              <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="权限名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="权限编码" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item label="类型" name="type">
          <a-select v-model:value="formModel.type" :options="typeOptions" />
        </a-form-item>
        <a-form-item label="描述" name="description">
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
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useCrudPage } from "@/composables/useCrudPage";
import { createPermission, getPermissionsPaged, updatePermission } from "@/services/api";
import type { PermissionListItem, PermissionCreateRequest, PermissionUpdateRequest } from "@/types/api";

const typeOptions = [
  { label: "Api", value: "Api" },
  { label: "Menu", value: "Menu" },
  { label: "Application", value: "Application" },
  { label: "Page", value: "Page" },
  { label: "Action", value: "Action" }
];
const typeFilter = ref<"all" | "Api" | "Menu" | "Application" | "Page" | "Action">("all");
const typeFilterOptions = [
  { label: "全部类型", value: "all" },
  { label: "Api", value: "Api" },
  { label: "Menu", value: "Menu" },
  { label: "Application", value: "Application" },
  { label: "Page", value: "Page" },
  { label: "Action", value: "Action" }
];

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

const crud = useCrudPage<PermissionListItem, PermissionListItem, PermissionCreateRequest, PermissionUpdateRequest>({
  tableKey: "system.permissions",
  columns: [
    { title: "权限名称", dataIndex: "name", key: "name" },
    { title: "权限编码", dataIndex: "code", key: "code" },
    { title: "类型", dataIndex: "type", key: "type" },
    { title: "描述", dataIndex: "description", key: "description" },
    { title: "操作", key: "actions", view: { canHide: false } }
  ],
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
    name: [{ required: true, message: "请输入权限名称" }],
    code: [{ required: true, message: "请输入权限编码" }],
    type: [{ required: true, message: "请选择类型" }]
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
    message.warning("请先选择权限");
    return;
  }
  const content = selectedRows.value.map((item) => item.code).join("\n");
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(content);
      message.success("已复制权限编码");
      return;
    }
    throw new Error("Clipboard API not available");
  } catch {
    message.warning("浏览器不支持自动复制，请手动复制");
    message.info(content);
  }
};
</script>
