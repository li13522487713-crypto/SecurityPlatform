<template>
  <CrudPageLayout
    title="员工管理"
    v-model:keyword="keyword"
    search-placeholder="搜索用户名/姓名/邮箱"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? '新增员工' : '编辑员工'"
    :drawer-width="640"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="resetFilters"
    @close-form="closeForm"
    @submit="handleSubmit"
  >
    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" @click="handleOpenCreate">新增员工</a-button>
      <a-button @click="handleExport" :loading="exporting">导出</a-button>
      <a-button v-if="canCreate" @click="importModalVisible = true">导入</a-button>
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
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button v-if="canUpdate || canAssignRoles || canAssignDepartments || canAssignPositions" type="link" @click="handleOpenEdit(record.id)">编辑</a-button>
              <a-popconfirm
                v-if="canDelete"
                title="确认删除该员工？"
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
      <a-tabs v-model:activeKey="activeTab">
        <a-tab-pane key="basic" tab="基本信息">
          <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
            <a-form-item v-if="formMode === 'create'" label="用户名" name="username">
              <a-input v-model:value="formModel.username" />
            </a-form-item>
            <a-form-item v-if="formMode === 'create'" label="密码" name="password">
              <a-input-password v-model:value="formModel.password" />
            </a-form-item>
            <a-form-item label="姓名" name="displayName">
              <a-input v-model:value="formModel.displayName" />
            </a-form-item>
            <a-form-item label="邮箱" name="email">
              <a-input v-model:value="formModel.email" />
            </a-form-item>
            <a-form-item label="手机号" name="phoneNumber">
              <a-input v-model:value="formModel.phoneNumber" />
            </a-form-item>
            <a-form-item label="状态" name="isActive">
              <a-switch v-model:checked="formModel.isActive" />
            </a-form-item>
          </a-form>
        </a-tab-pane>
        <a-tab-pane v-if="canAssignRoles" key="roles" tab="角色">
          <a-form layout="vertical">
            <a-form-item label="角色">
              <a-select
                v-model:value="formModel.roleIds"
                mode="multiple"
                placeholder="选择角色"
                :options="roleOptions"
                :loading="roleLoading"
                :filter-option="false"
                show-search
                @search="handleRoleSearch"
                @focus="() => loadRoleOptions()"
              />
            </a-form-item>
          </a-form>
        </a-tab-pane>
        <a-tab-pane v-if="canAssignDepartments" key="departments" tab="部门">
          <a-form layout="vertical">
            <a-form-item label="部门">
              <a-select
                v-model:value="formModel.departmentIds"
                mode="multiple"
                placeholder="选择部门"
                :options="departmentOptions"
                :loading="departmentLoading"
                :filter-option="false"
                show-search
                @search="handleDepartmentSearch"
                @focus="() => loadDepartmentOptions()"
              />
            </a-form-item>
          </a-form>
        </a-tab-pane>
        <a-tab-pane v-if="canAssignPositions" key="positions" tab="职位">
          <a-form layout="vertical">
            <a-form-item label="职位">
              <a-select
                v-model:value="formModel.positionIds"
                mode="multiple"
                placeholder="选择职位"
                :options="positionOptions"
                :loading="positionLoading"
                :filter-option="false"
                show-search
                @search="handlePositionSearch"
                @focus="() => loadPositionOptions()"
              />
            </a-form-item>
          </a-form>
        </a-tab-pane>
      </a-tabs>
    </template>
  </CrudPageLayout>

  <!-- 导入用户弹窗 -->
  <a-modal
    v-model:open="importModalVisible"
    title="批量导入用户"
    @cancel="handleImportCancel"
    :confirm-loading="importing"
    @ok="handleImport"
    ok-text="开始导入"
    cancel-text="取消"
    width="560px"
  >
    <div class="import-modal-body">
      <a-alert
        message="导入说明"
        description="请先下载导入模板，按模板填写用户信息后上传。支持 .xlsx 格式，单次最多 1000 行。"
        type="info"
        show-icon
        style="margin-bottom: 16px"
      />
      <div style="margin-bottom: 12px">
        <a-button type="link" style="padding: 0" @click="downloadImportTemplate">
          下载导入模板
        </a-button>
      </div>
      <a-upload
        accept=".xlsx"
        :before-upload="(file: File) => { importFile = file; return false; }"
        :max-count="1"
        :show-upload-list="true"
      >
        <a-button>选择文件</a-button>
      </a-upload>

      <div v-if="importResult" style="margin-top: 16px">
        <a-result
          :status="importResult.failureCount === 0 ? 'success' : 'warning'"
          :title="`导入完成：成功 ${importResult.successCount} 条，失败 ${importResult.failureCount} 条`"
        />
        <a-table
          v-if="importResult.errors.length > 0"
          :data-source="importResult.errors"
          :pagination="false"
          size="small"
          :columns="[
            { title: '行号', dataIndex: 'row', key: 'row', width: 70 },
            { title: '字段', dataIndex: 'field', key: 'field', width: 100 },
            { title: '错误信息', dataIndex: 'message', key: 'message' }
          ]"
        />
      </div>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useCrudPage } from "@/composables/useCrudPage";
import { useSelectOptions } from "@/composables/useSelectOptions";
import { useExcelExport } from "@/composables/useExcelExport";
import type { ImportResult } from "@/composables/useExcelExport";
import {
  createUser,
  deleteUser,
  getDepartmentsPaged,
  getPositionsPaged,
  getRolesPaged,
  getUserDetail,
  getUsersPaged,
  updateUser,
  updateUserDepartments,
  updateUserPositions,
  updateUserRoles
} from "@/services/api";
import type {
  DepartmentListItem,
  RoleListItem,
  UserDetail,
  UserListItem,
  UserCreateRequest,
  UserUpdateRequest,
  PositionListItem
} from "@/types/api";

const activeTab = ref("basic");

const { exporting, importing, exportUsers, downloadImportTemplate, importUsers } = useExcelExport();
const importModalVisible = ref(false);
const importFile = ref<File | null>(null);
const importResult = ref<ImportResult | null>(null);

const handleExport = () => exportUsers(keyword.value);

const handleImportFileChange = (info: { file: File }) => {
  importFile.value = info.file;
};

const handleImport = async () => {
  if (!importFile.value) {
    message.warning("请选择要导入的文件");
    return;
  }
  importResult.value = await importUsers(importFile.value);
  if (importResult.value) {
    if (importResult.value.failureCount === 0) {
      message.success(`导入成功，共 ${importResult.value.successCount} 条`);
      importModalVisible.value = false;
    } else {
      message.warning(
        `导入完成：成功 ${importResult.value.successCount} 条，失败 ${importResult.value.failureCount} 条`
      );
    }
    fetchData();
  }
};

const handleImportCancel = () => {
  importModalVisible.value = false;
  importFile.value = null;
  importResult.value = null;
};

// Select options via reusable composable
const roles = useSelectOptions<RoleListItem>({
  fetcher: getRolesPaged,
  mapItem: (role) => ({ label: `${role.name} (${role.code})`, value: Number(role.id) }),
  errorLabel: "加载角色"
});

const departments = useSelectOptions<DepartmentListItem>({
  fetcher: getDepartmentsPaged,
  mapItem: (dept) => ({ label: dept.name, value: Number(dept.id) }),
  errorLabel: "加载部门"
});

const positions = useSelectOptions<PositionListItem>({
  fetcher: getPositionsPaged,
  mapItem: (pos) => ({ label: `${pos.name} (${pos.code})`, value: Number(pos.id) }),
  errorLabel: "加载职位"
});

const roleOptions = roles.options;
const departmentOptions = departments.options;
const positionOptions = positions.options;
const roleLoading = roles.loading;
const departmentLoading = departments.loading;
const positionLoading = positions.loading;
const handleRoleSearch = roles.search;
const handleDepartmentSearch = departments.search;
const handlePositionSearch = positions.search;
const loadRoleOptions = roles.load;
const loadDepartmentOptions = departments.load;
const loadPositionOptions = positions.load;

const formRef = ref<FormInstance>();

const crud = useCrudPage<UserListItem, UserDetail, UserCreateRequest, UserUpdateRequest>({
  tableKey: "system.users",
  columns: [
    { title: "用户名", dataIndex: "username", key: "username" },
    { title: "姓名", dataIndex: "displayName", key: "displayName" },
    { title: "邮箱", dataIndex: "email", key: "email" },
    { title: "手机号", dataIndex: "phoneNumber", key: "phoneNumber" },
    { title: "状态", dataIndex: "isActive", key: "status" },
    { title: "最近登录", dataIndex: "lastLoginAt", key: "lastLoginAt" },
    { title: "操作", key: "actions", view: { canHide: false } }
  ],
  permissions: {
    create: "users:create",
    update: "users:update",
    delete: "users:delete",
    assignRoles: "users:assign-roles",
    assignDepartments: "users:assign-departments",
    assignPositions: "users:assign-positions"
  },
  api: {
    list: getUsersPaged,
    detail: getUserDetail,
    create: createUser,
    update: updateUser,
    delete: deleteUser
  },
  formRef,
  defaultFormModel: () => ({
    username: "",
    password: "",
    displayName: "",
    email: "",
    phoneNumber: "",
    isActive: true,
    roleIds: [],
    departmentIds: [],
    positionIds: []
  }),
  formRules: {
    username: [{ required: true, message: "请输入用户名" }],
    password: [{ required: true, message: "请输入密码" }],
    displayName: [{ required: true, message: "请输入姓名" }]
  },
  buildCreatePayload: (model) => ({
    username: model.username,
    password: model.password,
    displayName: model.displayName,
    email: model.email || undefined,
    phoneNumber: model.phoneNumber || undefined,
    isActive: model.isActive,
    roleIds: model.roleIds,
    departmentIds: model.departmentIds,
    positionIds: model.positionIds
  }),
  buildUpdatePayload: (model) => ({
    displayName: model.displayName,
    email: model.email || undefined,
    phoneNumber: model.phoneNumber || undefined,
    isActive: model.isActive
  }),
  mapDetailToForm: (detail, model) => {
    model.username = detail.username;
    model.displayName = detail.displayName;
    model.email = detail.email ?? "";
    model.phoneNumber = detail.phoneNumber ?? "";
    model.isActive = detail.isActive;
    model.roleIds = detail.roleIds.slice();
    model.departmentIds = detail.departmentIds.slice();
    model.positionIds = detail.positionIds.slice();
  },
  autoFetch: true
});

const {
  dataSource, loading, keyword, pagination,
  formVisible, formMode, formModel, formRules,
  selectedId,
  tableViewController, tableColumns, tableSize,
  canCreate, canUpdate, canDelete,
  onTableChange, handleSearch, resetFilters,
  closeForm, handleDelete, fetchData
} = crud;

const canAssignRoles = crud.hasPermissionFor("assignRoles");
const canAssignDepartments = crud.hasPermissionFor("assignDepartments");
const canAssignPositions = crud.hasPermissionFor("assignPositions");

const handleOpenCreate = () => {
  activeTab.value = "basic";
  crud.openCreate();
  void loadRoleOptions();
  void loadDepartmentOptions();
  void loadPositionOptions();
};

const handleOpenEdit = async (id: string) => {
  activeTab.value = "basic";
  void loadRoleOptions();
  void loadDepartmentOptions();
  void loadPositionOptions();
  await crud.openEdit(id);
};

const handleSubmit = async () => {
  if (formMode.value === "create") {
    await crud.submitForm();
  } else if (selectedId.value) {
    // Update basic info
    try {
      await updateUser(selectedId.value, {
        displayName: formModel.displayName,
        email: formModel.email || undefined,
        phoneNumber: formModel.phoneNumber || undefined,
        isActive: formModel.isActive
      });

      // Update assignments in parallel
      const promises: Promise<void>[] = [];
      if (canAssignRoles) {
        promises.push(updateUserRoles(selectedId.value, { roleIds: formModel.roleIds }));
      }
      if (canAssignDepartments) {
        promises.push(updateUserDepartments(selectedId.value, { departmentIds: formModel.departmentIds }));
      }
      if (canAssignPositions) {
        promises.push(updateUserPositions(selectedId.value, { positionIds: formModel.positionIds }));
      }
      if (promises.length) {
        await Promise.all(promises);
      }

      message.success("更新成功");
      formVisible.value = false;
      fetchData();
    } catch (error) {
      message.error((error as Error).message || "更新失败");
    }
  }
};
</script>
