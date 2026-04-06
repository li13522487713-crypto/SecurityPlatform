<template>
  <div>
    <CrudPageLayout
      v-model:keyword="keyword"
      :title="t('systemUsers.pageTitle')"
      :search-placeholder="t('systemUsers.searchPlaceholder')"
      :drawer-open="formVisible"
      :drawer-title="formMode === 'create' ? t('systemUsers.drawerCreateTitle') : t('systemUsers.drawerEditTitle')"
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
        <a-button v-if="canCreate" type="primary" @click="handleOpenCreate">
          <template #icon><UserAddOutlined /></template>
          {{ t("systemUsers.addUser") }}
        </a-button>
        <a-button :loading="exporting" @click="handleExport">
          <template #icon><ExportOutlined /></template>
          {{ t("systemUsers.exportUsers") }}
        </a-button>
        <a-button v-if="canCreate" @click="importModalVisible = true">
          <template #icon><ImportOutlined /></template>
          {{ t("systemUsers.importUsers") }}
        </a-button>
      </template>
      <template #toolbar-right>
        <TableViewToolbar :controller="tableViewController" />
      </template>

      <template #table>
        <a-row :gutter="16" style="height: 100%">
          <a-col :xs="24" :sm="24" :md="6" :lg="5" class="dept-tree-col">
            <a-card size="small" :bordered="false" class="tree-card" :body-style="{ padding: '12px' }">
              <template #extra>
                <a-button
                  v-if="canManageDepartments"
                  type="link"
                  size="small"
                  @click="$router.push('/settings/org/departments')"
                >
                  {{ t("systemUsers.manageDepartments") }}
                </a-button>
              </template>
              <div style="margin-bottom: 12px">
                <a-input
                  v-model:value="treeKeyword"
                  :placeholder="t('systemUsers.treeSearchPlaceholder')"
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
                  class="dept-tree"
                  @select="handleTreeSelect"
                />
              </a-skeleton>
            </a-card>
          </a-col>
          <a-col :xs="24" :sm="24" :md="18" :lg="19" class="users-table-col">
            <a-table
              :columns="tableColumns"
              :data-source="dataSource"
              :pagination="pagination"
              :loading="loading"
              :size="tableSize"
              :scroll="{ x: 'max-content' }"
              row-key="id"
              @change="onTableChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'status'">
                  <StatusSwitch
                    v-model="record.isActive"
                    :api="(value) => handleStatusChange(record.id, value)"
                  />
                </template>
                <template v-else-if="column.key === 'actions'">
                  <a-space>
                    <a-button
                      v-if="canUpdate || canAssignRoles || canAssignDepartments || canAssignPositions"
                      type="link"
                      @click="handleOpenEdit(record.id)"
                    >
                      {{ t("common.edit") }}
                    </a-button>
                    <a-popconfirm
                      v-if="canDelete"
                      :title="t('systemUsers.deleteConfirm')"
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
          </a-col>
        </a-row>
      </template>

      <template #form>
        <a-tabs v-model:active-key="activeTab">
          <a-tab-pane key="basic" :tab="t('systemUsers.basicTab')">
            <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
              <a-form-item v-if="formMode === 'create'" :label="t('systemUsers.username')" name="username">
                <a-input v-model:value="formModel.username" />
              </a-form-item>
              <a-form-item v-if="formMode === 'create'" :label="t('systemUsers.password')" name="password">
                <a-input-password v-model:value="formModel.password" />
              </a-form-item>
              <a-form-item :label="t('systemUsers.displayName')" name="displayName">
                <a-input v-model:value="formModel.displayName" />
              </a-form-item>
              <a-form-item :label="t('systemUsers.email')" name="email">
                <a-input v-model:value="formModel.email" />
              </a-form-item>
              <a-form-item :label="t('systemUsers.phoneNumber')" name="phoneNumber">
                <a-input v-model:value="formModel.phoneNumber" />
              </a-form-item>
              <a-form-item :label="t('systemUsers.status')" name="isActive">
                <a-switch v-model:checked="formModel.isActive" />
              </a-form-item>
            </a-form>
          </a-tab-pane>
          <a-tab-pane v-if="canAssignRoles" key="roles" :tab="t('systemUsers.rolesTab')">
            <a-form layout="vertical">
              <a-form-item :label="t('systemUsers.rolesTab')">
                <a-select
                  v-model:value="formModel.roleIds"
                  mode="multiple"
                  :placeholder="t('systemUsers.selectRole')"
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
          <a-tab-pane v-if="canAssignDepartments" key="departments" :tab="t('systemUsers.departmentsTab')">
            <a-form layout="vertical">
              <a-form-item :label="t('systemUsers.departmentsTab')">
                <a-select
                  v-model:value="formModel.departmentIds"
                  mode="multiple"
                  :placeholder="t('systemUsers.selectDepartment')"
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
          <a-tab-pane v-if="canAssignPositions" key="positions" :tab="t('systemUsers.positionsTab')">
            <a-form layout="vertical">
              <a-form-item :label="t('systemUsers.positionsTab')">
                <a-select
                  v-model:value="formModel.positionIds"
                  mode="multiple"
                  :placeholder="t('systemUsers.selectPosition')"
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

    <a-modal
      v-model:open="importModalVisible"
      :title="t('systemUsers.importModalTitle')"
      :confirm-loading="importing"
      :ok-text="t('common.startImport')"
      :cancel-text="t('common.cancel')"
      width="560px"
      @cancel="handleImportCancel"
      @ok="handleImport"
    >
      <div class="import-modal-body">
        <a-alert
          :message="t('systemUsers.importHelpTitle')"
          :description="t('systemUsers.importHelpDescription')"
          type="info"
          show-icon
          style="margin-bottom: 16px"
        />
        <div style="margin-bottom: 12px">
          <a-button type="link" style="padding: 0" @click="downloadImportTemplate">
            {{ t("systemUsers.downloadTemplate") }}
          </a-button>
        </div>
        <a-upload accept=".xlsx" :before-upload="beforeUpload" :max-count="1" :show-upload-list="true">
          <a-button>{{ t("common.selectFile") }}</a-button>
        </a-upload>

        <div v-if="importResult" style="margin-top: 16px">
          <a-result
            :status="importResult.failureCount === 0 ? 'success' : 'warning'"
            :title="
              t('systemUsers.importCompletedTitle', {
                success: importResult.successCount,
                failure: importResult.failureCount
              })
            "
          />
          <a-table
            v-if="importResult.errors.length > 0"
            :data-source="importResult.errors"
            :pagination="false"
            size="small"
            :columns="importResultColumns"
          />
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { ExportOutlined, ImportOutlined, UserAddOutlined } from "@ant-design/icons-vue";
import { CrudPageLayout, TableViewToolbar } from "@atlas/shared-ui";
import StatusSwitch from "@/components/common/StatusSwitch.vue";
import { useCrudPage, getAuthProfile, hasPermission as checkPermission } from "@atlas/shared-core";
import { useSelectOptions } from "@/composables/useSelectOptions";
import { useExcelExport, type ImportResult } from "@/composables/useExcelExport";
import {
  createUser,
  deleteUser,
  getDepartmentsAll,
  getDepartmentsPaged,
  getPositionsPaged,
  getRolesPaged,
  getUserDetail,
  getUsersPaged,
  updateUser
} from "@/services/api-users";
import { tableViewApi } from "@/services/api-table-views";
import type {
  DepartmentListItem,
  PositionListItem,
  RoleListItem,
  UserCreateRequest,
  UserDetail,
  UserListItem,
  UserUpdateRequest
} from "@atlas/shared-core";

const { t } = useI18n();
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
  if (!keywordValue.trim()) return nodes;
  const matcher = keywordValue.trim();
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
  const fullTree = [{ key: "all", title: t("systemUsers.allDepartments"), children: rootNodes }];
  return filterTree(fullTree, treeKeyword.value);
});

const selectedTreeKeys = computed(() => {
  if (selectedDepartmentId.value === null) return ["all"];
  return [selectedDepartmentId.value.toString()];
});

const expandedTreeKeys = computed(() => {
  if (!treeKeyword.value.trim()) return ["all"];
  return ["all", ...allDepartments.value.map((item) => String(item.id))];
});

const isMounted = ref(false);

const loadAllDepartments = async () => {
  treeLoading.value = true;
  try {
    const result = await getDepartmentsAll();
    if (!isMounted.value) return;
    allDepartments.value = result;
  } catch (error) {
    if (!isMounted.value) return;
    message.error((error as Error).message || t("systemUsers.loadDepartmentTreeFailed"));
  } finally {
    if (isMounted.value) {
      treeLoading.value = false;
    }
  }
};

const handleTreeSelect = (keys: (string | number)[]) => {
  if (!keys.length || keys[0] === "all") {
    selectedDepartmentId.value = null;
  } else {
    selectedDepartmentId.value = Number(keys[0]);
  }
  crud.handleSearch();
};

const handleExport = () => exportUsers(keyword.value);

const handleImport = async () => {
  if (!importFile.value) {
    message.warning(t("systemUsers.chooseImportFile"));
    return;
  }
  const result = await importUsers(importFile.value);
  if (!isMounted.value) return;
  importResult.value = result;
  if (!importResult.value) {
    return;
  }

  if (importResult.value.failureCount === 0) {
    message.success(t("systemUsers.importSuccessMessage", { count: importResult.value.successCount }));
    importModalVisible.value = false;
  } else {
    message.warning(
      t("systemUsers.importCompletedMessage", {
        success: importResult.value.successCount,
        failure: importResult.value.failureCount
      })
    );
  }
  await fetchData();
};

const beforeUpload = (file: File) => {
  importFile.value = file;
  return false;
};

onMounted(() => {
  isMounted.value = true;
  void loadAllDepartments();
});

onUnmounted(() => {
  isMounted.value = false;
});

const handleImportCancel = () => {
  importModalVisible.value = false;
  importFile.value = null;
  importResult.value = null;
};

const roles = useSelectOptions<RoleListItem>({
  fetcher: getRolesPaged,
  mapItem: (role) => ({ label: `${role.name} (${role.code})`, value: Number(role.id) }),
  errorLabel: t("systemUsers.loadRolesLabel")
});

const departments = useSelectOptions<DepartmentListItem>({
  fetcher: getDepartmentsPaged,
  mapItem: (department) => ({ label: department.name, value: Number(department.id) }),
  errorLabel: t("systemUsers.loadDepartmentsLabel")
});

const positions = useSelectOptions<PositionListItem>({
  fetcher: getPositionsPaged,
  mapItem: (position) => ({ label: `${position.name} (${position.code})`, value: Number(position.id) }),
  errorLabel: t("systemUsers.loadPositionsLabel")
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

const tableColumnsDef = computed(() => [
  { title: t("systemUsers.username"), dataIndex: "username", key: "username" },
  { title: t("systemUsers.displayName"), dataIndex: "displayName", key: "displayName" },
  { title: t("systemUsers.email"), dataIndex: "email", key: "email" },
  { title: t("systemUsers.phoneNumber"), dataIndex: "phoneNumber", key: "phoneNumber" },
  { title: t("systemUsers.status"), dataIndex: "isActive", key: "status" },
  { title: t("systemUsers.lastLoginAt"), dataIndex: "lastLoginAt", key: "lastLoginAt" },
  { title: t("systemUsers.actions"), key: "actions", view: { canHide: false } }
]);

const importResultColumns = computed(() => [
  { title: t("systemUsers.importResultRow"), dataIndex: "row", key: "row", width: 70 },
  { title: t("systemUsers.importResultField"), dataIndex: "field", key: "field", width: 100 },
  { title: t("systemUsers.importResultMessage"), dataIndex: "message", key: "message" }
]);

const formRef = ref<FormInstance>();

const crud = useCrudPage<UserListItem, UserDetail, UserCreateRequest, UserUpdateRequest>({
  tableKey: "system.users",
  columns: tableColumnsDef,
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
  tableViewApi,
  translate: (key: string, params?: Record<string, unknown>) =>
    String(t(key, (params ?? {}) as Record<string, string | number | boolean>)),
  defaultFormModel: () => ({
    username: "",
    password: "",
    displayName: "",
    email: "",
    phoneNumber: "",
    isActive: true,
    roleIds: [] as number[],
    departmentIds: [] as number[],
    positionIds: [] as number[]
  }),
  formRules: {
    username: [{ required: true, message: t("systemUsers.usernameRequired") }],
    password: [{ required: true, message: t("systemUsers.passwordRequired") }],
    displayName: [{ required: true, message: t("systemUsers.displayNameRequired") }]
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
  buildListParams: (base, _advanced) => ({
    ...base,
    departmentId: selectedDepartmentId.value ?? undefined
  }),
  autoFetch: true
});

const {
  dataSource,
  loading,
  keyword,
  pagination,
  formVisible,
  formMode,
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
  closeForm,
  handleDelete,
  fetchData
} = crud;

const canAssignRoles = crud.hasPermissionFor("assignRoles");
const canAssignDepartments = crud.hasPermissionFor("assignDepartments");
const canAssignPositions = crud.hasPermissionFor("assignPositions");

const authProfile = getAuthProfile();
const canManageDepartments = checkPermission(authProfile, "departments:create")
  || checkPermission(authProfile, "departments:update");

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
  const user = dataSource.value.find((item) => item.id === id);
  if (!user) {
    return;
  }

  await updateUser(id, {
    displayName: user.displayName,
    email: user.email || undefined,
    phoneNumber: user.phoneNumber || undefined,
    isActive
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

.dept-tree-col {
  height: 100%;
}

.tree-card {
  height: 100%;
  border-right: 1px solid var(--color-border, #f0f0f0);
}

.dept-tree {
  height: calc(100vh - 310px);
  overflow-y: auto;
}

.users-table-col {
  height: 100%;
}

@media screen and (max-width: 768px) {
  .dept-tree-col {
    border-right: none;
    border-bottom: 1px solid var(--color-border, #f0f0f0);
    padding-right: 0;
    margin-bottom: 16px;
    padding-bottom: 16px;
    height: auto;
  }

  .dept-tree {
    height: 200px;
  }
}
</style>
