<template>
  <CrudPageLayout
    title="职位管理"
    v-model:keyword="keyword"
    search-placeholder="搜索职位名称/编码"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? '新增职位' : '编辑职位'"
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
      <a-button v-if="canCreate" type="primary" @click="openCreate">新增职位</a-button>
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
              {{ record.isActive ? "启用" : "停用" }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'system'">
            <a-tag :color="record.isSystem ? 'blue' : 'default'">
              {{ record.isSystem ? "系统内置" : "自定义" }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
              <a-popconfirm
                v-if="canDelete && !record.isSystem"
                title="确认删除该职位？"
                ok-text="删除"
                cancel-text="取消"
                @confirm="handleDelete(record.id)"
              >
                <a-button type="link" danger>删除</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="职位名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="编码" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="formModel.description" :rows="3" />
        </a-form-item>
        <a-form-item label="状态" name="isActive">
          <a-switch v-model:checked="formModel.isActive" />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { ref } from "vue";
import type { FormInstance } from "ant-design-vue";
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

const formRef = ref<FormInstance>();

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
  columns: [
    { title: "职位名称", dataIndex: "name", key: "name" },
    { title: "编码", dataIndex: "code", key: "code" },
    { title: "描述", dataIndex: "description", key: "description" },
    { title: "状态", key: "status" },
    { title: "类型", key: "system" },
    { title: "排序", dataIndex: "sortOrder", key: "sortOrder" },
    { title: "操作", key: "actions", view: { canHide: false } }
  ],
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
    name: [{ required: true, message: "请输入职位名称" }],
    code: [{ required: true, message: "请输入编码" }]
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
