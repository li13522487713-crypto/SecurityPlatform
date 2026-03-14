<template>
  <CrudPageLayout
    title="员工管理"
    v-model:keyword="keyword"
    search-placeholder="搜索用户名/姓名/邮箱"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? '新增员工' : '编辑员工'"
    :drawer-width="640"
    :submit-loading="submitting"
    :submit-disabled="submitting"
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
      <a-row :gutter="16" style="height: 100%">
        <a-col :span="5" style="height: 100%; border-right: 1px solid var(--color-border); padding-right: 16px;">
          <div style="margin-bottom: 12px">
            <a-input
              v-model:value="treeKeyword"
              placeholder="搜索部门"
              allow-clear
              size="small"
            />
          </div>
          <a-skeleton :loading="treeLoading" active>
             <a-tree
              :tree-data="treeData"
              :selected-keys="selectedTreeKeys"
              :expanded-keys="expandedTreeKeys"
              :auto-expand-parent="true"
              @select="handleTreeSelect"
              style="height: calc(100vh - 250px); overflow-y: auto;"
            />
          </a-skeleton>
        </a-col>
        <a-col :span="19">
          <a-table
            :columns="tableColumns"
            :data-source="dataSource"
            :pagination="pagination"
            :loading="loading"
            :size="tableSize"
            row-key="id"
            @change="onTableChange"
          >
            <!-- 保持原有的 columns 渲染 -->
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'status'">
                <StatusSwitch
                  v-model="record.isActive"
                  :api="(val) => handleStatusChange(record.id, val)"
                />
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
        </a-col>
      </a-row>
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
import { ref, computed, onMounted } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import StatusSwitch from "@/components/common/StatusSwitch.vue";
import { useCrudPage } from "@/composables/useCrudPage";
import { useSelectOptions } from "@/composables/useSelectOptions";
import { useExcelExport } from "@/composables/useExcelExport";
import type { ImportResult } from "@/composables/useExcelExport";
import {
  createUser,
  deleteUser,
  getDepartmentsPaged,
  getDepartmentsAll,
  getPositionsPaged,
  getRolesPaged,
  getUserDetail,
  getUsersPaged,
  updateUser
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
import { getAuthProfile, hasPermission } from "@/utils/auth";

const activeTab = ref("basic");
const submitting = ref(false);

const { exporting, importing, exportUsers, downloadImportTemplate, importUsers } = useExcelExport();
const importModalVisible = ref(false);
const importFile = ref<File | null>(null);
const importResult = ref<ImportResult | null>(null);

const treeKeyword = ref("");
const treeLoading = ref(false);
const allDepartments = ref<DepartmentListItem[]>([]);
const selectedDepartmentId = ref<number | null>(null);

const profile = getAuthProfile();

interface TreeNode {
  key: string;
  title: string;
  children?: TreeNode[];
}

const buildTree = (items: DepartmentListItem[]) => {
  const nodeMap = new Map<string, TreeNode>();
  const rootNodes: TreeNode[] = [];

  items.forEach((item) => {
    nodeMap.set(String(item.id), { key: String(item.id), title: item.name, children: [] });
  });

  items.forEach((item) => {
    const node = nodeMap.get(String(item.id));
    if (!node) return;
    if (item.parentId) {
      const parent = nodeMap.get(String(item.parentId));
      if (parent) {
        parent.children = parent.children ?? [];
        parent.children.push(node);
        return;
      }
    }
    rootNodes.push(node);
  });
  return rootNodes;
};

const filterTree = (nodes: TreeNode[], keywordValue: string): TreeNode[] => {
  if (!keywordValue) return nodes;
  const matcher = keywordValue.trim();
  if (!matcher) return nodes;
  const result: TreeNode[] = [];
  nodes.forEach((node) => {
    const children = node.children ? filterTree(node.children, matcher) : [];
    if (node.title.includes(matcher) || children.length > 0) {
      result.push({ ...node, children });
    }
  });
  return result;
};

const treeData = computed(() => {
  const rootNodes = buildTree(allDepartments.value);
  const fullTree = [{ key: 'all', title: '全部部门', children: rootNodes }];
  return filterTree(fullTree, treeKeyword.value);
});

const selectedTreeKeys = computed(() => {
  if (selectedDepartmentId.value === null) return ['all'];
  return [selectedDepartmentId.value.toString()];
});

const expandedTreeKeys = computed(() => {
  if (!treeKeyword.value.trim()) return ['all'];
  return ['all', ...allDepartments.value.map((item) => String(item.id))];
});

const loadAllDepartments = async () => {
  treeLoading.value = true;
  try {
    allDepartments.value = await getDepartmentsAll();
  } catch (error) {
    message.error((error as Error).message || "加载部门树失败");
  } finally {
    treeLoading.value = false;
  }
};

const handleTreeSelect = (keys: (string | number)[]) => {
  if (!keys.length || keys[0] === 'all') {
    selectedDepartmentId.value = null;
  } else {
    selectedDepartmentId.value = Number(keys[0]);
  }
  crud.handleSearch();
};


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

onMounted(() => {
  loadAllDepartments();
});

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
    list: (req) => getUsersPaged({ ...req, departmentId: selectedDepartmentId.value || undefined }),
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
    isActive: model.isActive,
    roleIds: model.roleIds,
    departmentIds: model.departmentIds,
    positionIds: model.positionIds
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
  if (submitting.value) {
    return;
  }
  submitting.value = true;
  try {
    await crud.submitForm();
  } finally {
    submitting.value = false;
  }
};

const handleStatusChange = async (id: string, isActive: boolean) => {
  // 从 dataSource 找到原记录
  const user = dataSource.value.find(u => u.id === id);
  if (!user) return;
  // 构造只更新状态的 payload，其他保留原值
  // 此处假设接口可以使用 PATCH 或直接 PUT 现有记录（前端可能需要传其他必填项）
  // 检查 API，updateUser 接受 UserUpdateRequest，我们需要把当前显示的值发回去，或者后端支持部分更新
  await updateUser(id, {
    displayName: user.displayName,
    email: user.email || undefined,
    phoneNumber: user.phoneNumber || undefined,
    isActive: isActive
  });
};
</script>

<style scoped>
.import-modal-body {
  padding: 10px 0;
}
:deep(.ant-tree-node-content-wrapper) {
  text-overflow: ellipsis;
  overflow: hidden;
  white-space: nowrap;
}
</style>
