<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    title="角色管理"
    search-placeholder="搜索角色名称/编码"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? '新增角色' : '编辑角色'"
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
      <a-button v-if="canCreate" type="primary" @click="openCreate">新增角色</a-button>
    </template>
    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
      <a-popconfirm
        v-if="canDelete"
        title="确认批量删除所选角色？"
        ok-text="删除"
        cancel-text="取消"
        @confirm="handleBatchDelete"
      >
        <a-button danger :disabled="!selectedRowKeys.length">批量删除</a-button>
      </a-popconfirm>
    </template>

    <template #filter>
      <a-select
        v-model:value="systemFilter"
        :options="systemOptions"
        style="width: 160px"
        @change="handleSearch"
      />
    </template>

    <template #table>
      <MasterDetailLayout :detail-visible="isDetailVisible" :master-width="700">
        <template #master>
          <a-table
            :columns="tableColumns"
            :data-source="dataSource"
            :pagination="pagination"
            :loading="loading"
            :row-selection="rowSelection"
            :size="tableSize"
            row-key="id"
            @change="onTableChange"
            :custom-row="customRow"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'isSystem'">
                <a-tag v-if="record.isSystem" color="blue">系统</a-tag>
                <span v-else>-</span>
              </template>
              <template v-if="column.key === 'actions'">
                <a-space @click.stop>
                  <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
                  <a-button
                    v-if="canAssignPermissions || canAssignMenus"
                    type="link"
                    @click="openAssign(record)"
                  >
                    权限
                  </a-button>
                  <a-popconfirm
                    v-if="canDelete"
                    title="确认删除该角色？"
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
        <template #detail>
          <RoleAssignPanel
            v-if="selectedItem"
            :role-id="selectedItem.id"
            :role-code="selectedItem.code"
            :role-name="selectedItem.name"
            :can-assign-permissions="canAssignPermissions"
            :can-assign-menus="canAssignMenus"
            @success="handleAssignSuccess"
          />
        </template>
      </MasterDetailLayout>
    </template>
    
    <template #form>
      <div class="form-wrapper">
        <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
          <a-form-item label="角色名称" name="name">
            <a-input v-model:value="formModel.name" placeholder="请输入角色名称" />
          </a-form-item>
          <a-form-item label="角色编码" name="code">
            <a-input v-model:value="formModel.code" placeholder="请输入角色编码" :disabled="formMode === 'edit'" />
          </a-form-item>
          <a-form-item label="描述" name="description">
            <a-textarea v-model:value="formModel.description" placeholder="请输入角色描述" :rows="4" />
          </a-form-item>
        </a-form>
      </div>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import MasterDetailLayout from "@/components/layout/MasterDetailLayout.vue";
import RoleAssignPanel from "@/components/system/roles/RoleAssignPanel.vue";
import { useCrudPage } from "@/composables/useCrudPage";
import { useMasterDetail } from "@/composables/useMasterDetail";
import {
  createRole,
  deleteRole,
  getRoleDetail,
  getRolesPaged,
  updateRole
} from "@/services/api";
import type { RoleListItem, RoleDetail, RoleCreateRequest, RoleUpdateRequest } from "@/types/api";

const systemFilter = ref<"all" | "system" | "custom">("all");
const systemOptions = [
  { label: "全部角色", value: "all" },
  { label: "系统内置", value: "system" },
  { label: "自定义", value: "custom" }
];

const selectedRowKeys = ref<string[]>([]);

const rowSelection = computed(() => {
  if (!crud.canDelete) return undefined;
  return {
    selectedRowKeys: selectedRowKeys.value,
    onChange: (keys: (string | number)[]) => {
      selectedRowKeys.value = keys.map((key) => key.toString());
    }
  };
});

const formRef = ref<FormInstance>();

const crud = useCrudPage<RoleListItem, RoleDetail, RoleCreateRequest, RoleUpdateRequest>({
  tableKey: "system.roles",
  columns: [
    { title: "角色名称", dataIndex: "name", key: "name" },
    { title: "角色编码", dataIndex: "code", key: "code" },
    { title: "描述", dataIndex: "description", key: "description" },
    { title: "系统内置", dataIndex: "isSystem", key: "isSystem" },
    { title: "操作", key: "actions", view: { canHide: false } }
  ],
  permissions: {
    create: "roles:create",
    update: "roles:update",
    delete: "roles:delete",
    assignPermissions: "roles:assign-permissions",
    assignMenus: "roles:assign-menus"
  },
  api: {
    list: getRolesPaged,
    detail: getRoleDetail,
    create: createRole,
    update: updateRole,
    delete: deleteRole
  },
  formRef,
  defaultFormModel: () => ({
    name: "",
    code: "",
    description: ""
  }),
  formRules: {
    name: [{ required: true, message: "请输入角色名称" }],
    code: [{ required: true, message: "请输入角色编码" }]
  },
  buildListParams: (base) => ({
    ...base,
    isSystem: systemFilter.value === "all" ? undefined : systemFilter.value === "system"
  }),
  buildCreatePayload: (model) => ({
    name: model.name,
    code: model.code,
    description: model.description || undefined
  }),
  buildUpdatePayload: (model) => ({
    name: model.name,
    description: model.description || undefined
  }),
  mapRecordToForm: (record, model) => {
    model.name = record.name;
    model.code = record.code;
    model.description = record.description ?? "";
  }
});

const {
  dataSource, loading, keyword, pagination,
  formVisible, formMode, submitting, formModel, formRules,
  tableViewController, tableColumns, tableSize,
  canCreate, canUpdate, canDelete,
  onTableChange, openCreate, openEdit, closeForm, submitForm, handleDelete
} = crud;

const canAssignPermissions = crud.hasPermissionFor("assignPermissions");
const canAssignMenus = crud.hasPermissionFor("assignMenus");

const { selectedItem, isDetailVisible, selectItem } = useMasterDetail<RoleListItem>();

const customRow = (record: RoleListItem) => {
  return {
    onClick: () => {
      selectItem(record);
    },
    style: {
      cursor: 'pointer',
      backgroundColor: (selectedItem.value && selectedItem.value.id === record.id) ? 'var(--color-primary-bg)' : undefined
    }
  };
};

const openAssign = (record: RoleListItem) => {
  selectItem(record);
};

const handleAssignSuccess = () => {};

// --- Search/Reset ---
const handleSearch = () => {
  selectedRowKeys.value = [];
  crud.handleSearch();
};

const handleReset = () => {
  systemFilter.value = "all";
  selectedRowKeys.value = [];
  crud.keyword.value = "";
  crud.handleSearch();
};

const handleBatchDelete = async () => {
  if (!selectedRowKeys.value.length) {
    message.warning("请先选择角色");
    return;
  }
  try {
    await Promise.all(selectedRowKeys.value.map((id) => deleteRole(id)));
    message.success(`已删除 ${selectedRowKeys.value.length} 个角色`);
    selectedRowKeys.value = [];
    crud.fetchData();
  } catch (error) {
    message.error((error as Error).message || "批量删除失败");
  }
};
</script>
