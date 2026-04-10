<template>
  <CrudPageLayout
    data-testid="app-users-page"
    v-model:keyword="keyword"
    :title="t('systemUsers.pageTitle')"
    :search-placeholder="t('systemUsers.searchPlaceholder')"
    :drawer-open="drawerVisible"
    :drawer-title="drawerTitle"
    :drawer-width="600"
    :submit-loading="submitting"
    @update:drawer-open="drawerVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeDrawer"
    @submit="handleSubmit"
  >
    <template #toolbar-actions />

    <template #table>
      <a-row :gutter="16" class="app-users-layout">
        <a-col :span="5">
          <div class="app-users-tree-card">
            <a-input
              v-model:value="treeKeyword"
              :placeholder="t('systemUsers.treeSearch')"
              allow-clear
              class="app-users-tree-search"
            />
            <a-tree
              :tree-data="deptTreeData"
              :selected-keys="selectedDeptKeys"
              :expanded-keys="expandedDeptKeys"
              :auto-expand-parent="true"
              @select="handleDeptSelect"
            />
          </div>
        </a-col>
        <a-col :span="19" class="app-users-main-col">
          <div class="app-users-right-header">
            <div class="app-users-right-header-text">
              <div class="app-users-dept-title">{{ selectedDeptName }}</div>
              <div class="app-users-dept-stat">{{ memberStatText }}</div>
            </div>
            <a-space>
              <a-button
                v-if="canCreate"
                data-testid="app-users-add-existing"
                class="app-users-outline-btn"
                @click="openAddExistingMember"
              >
                <template #icon><UserAddOutlined /></template>
                {{ t("systemUsers.batchImport") }}
              </a-button>
              <a-button
                v-if="canCreate"
                type="primary"
                data-testid="app-users-create"
                class="app-users-create-btn"
                @click="openCreateUser"
              >
                <template #icon><PlusOutlined /></template>
                {{ t("systemUsers.addUser") }}
              </a-button>
            </a-space>
          </div>
          <a-table
            data-testid="app-users-table"
            :columns="columns"
            :data-source="tableData"
            :loading="loading"
            :pagination="pagination"
            row-key="userId"
            @change="handleTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'userInfo'">
                <div style="display: flex; align-items: center; gap: 12px">
                  <a-avatar :size="40" style="background-color: #4f39f6; flex-shrink: 0">
                    {{ (record.displayName || record.username || "?").charAt(0) }}
                  </a-avatar>
                  <div>
                    <div style="font-weight: 600; color: #101828">
                      {{ record.displayName || record.username }}
                    </div>
                    <div style="font-size: 12px; color: #6a7282">{{ record.username }}</div>
                  </div>
                </div>
              </template>
              <template v-else-if="column.key === 'contact'">
                <div style="display: flex; flex-direction: column; gap: 4px">
                  <div
                    v-if="record.email"
                    style="display: flex; align-items: center; gap: 6px; color: #364153"
                  >
                    <MailOutlined style="color: #9ca3af" />
                    <span>{{ record.email }}</span>
                  </div>
                  <div
                    v-if="record.phoneNumber"
                    style="display: flex; align-items: center; gap: 6px; color: #364153"
                  >
                    <PhoneOutlined style="color: #9ca3af" />
                    <span>{{ record.phoneNumber }}</span>
                  </div>
                  <span v-if="!record.email && !record.phoneNumber" style="color: #9ca3af">-</span>
                </div>
              </template>
              <template v-else-if="column.key === 'isActive'">
                <a-tag :color="record.isActive ? 'green' : 'red'">
                  {{ record.isActive ? t("common.statusEnabled") : t("common.statusDisabled") }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'roles'">
                <a-tag v-for="name in record.roleNames ?? []" :key="name" color="blue" class="app-users-role-tag">
                  {{ name }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'actions'">
                <a-space>
                  <a-button
                    v-if="canUpdate"
                    type="link"
                    size="small"
                    :data-testid="`app-users-edit-${record.userId}`"
                    @click="openEditMember(record)"
                  >
                    {{ t("common.edit") }}
                  </a-button>
                  <a-button
                    v-if="canUpdate"
                    type="link"
                    size="small"
                    :data-testid="`app-users-reset-password-${record.userId}`"
                    @click="openResetPassword(record)"
                  >
                    {{ t("systemUsers.resetPassword") }}
                  </a-button>
                  <a-popconfirm
                    v-if="canDelete"
                    :title="t('systemUsers.removeMemberConfirm')"
                    @confirm="handleRemoveMember(record.userId)"
                  >
                    <a-button type="link" size="small" danger :data-testid="`app-users-remove-${record.userId}`">
                      {{ t("systemUsers.removeMember") }}
                    </a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-col>
      </a-row>
    </template>

    <template #form>
      <!-- Create new user form -->
      <template v-if="formMode === 'create'">
        <a-form ref="createFormRef" :model="createForm" :rules="createRules" layout="vertical">
          <a-form-item :label="t('systemUsers.username')" name="username">
            <a-input v-model:value="createForm.username" data-testid="app-users-form-username" />
          </a-form-item>
          <a-form-item :label="t('systemUsers.password')" name="password">
            <a-input-password v-model:value="createForm.password" data-testid="app-users-form-password" />
          </a-form-item>
          <a-form-item :label="t('systemUsers.displayName')" name="displayName">
            <a-input v-model:value="createForm.displayName" data-testid="app-users-form-display-name" />
          </a-form-item>
          <a-form-item :label="t('systemUsers.email')" name="email">
            <a-input v-model:value="createForm.email" />
          </a-form-item>
          <a-form-item :label="t('systemUsers.phoneNumber')" name="phoneNumber">
            <a-input v-model:value="createForm.phoneNumber" />
          </a-form-item>
          <a-form-item :label="t('systemUsers.status')" name="isActive">
            <a-switch v-model:checked="createForm.isActive" />
          </a-form-item>
          <a-divider>{{ t("systemUsers.assignSections") }}</a-divider>
          <a-form-item :label="t('systemUsers.roles')">
            <a-select
              v-model:value="createForm.roleIds"
              mode="multiple"
              :options="roleOptions"
              :loading="roleOptionsLoading"
              :filter-option="false"
              show-search
              @search="handleRoleSearch"
              @focus="() => loadRoleOptions()"
            />
          </a-form-item>
          <a-form-item :label="t('systemUsers.departments')">
            <a-select
              v-model:value="createForm.departmentIds"
              mode="multiple"
              :options="deptOptions"
              :loading="deptOptionsLoading"
              :filter-option="false"
              show-search
              @search="handleDeptOptionSearch"
              @focus="() => loadDeptOptions()"
            />
          </a-form-item>
          <a-form-item :label="t('systemUsers.positions')">
            <a-select
              v-model:value="createForm.positionIds"
              mode="multiple"
              :options="positionOptions"
              :loading="positionOptionsLoading"
              :filter-option="false"
              show-search
              @search="handlePositionSearch"
              @focus="() => loadPositionOptions()"
            />
          </a-form-item>
        </a-form>
      </template>

      <!-- Add existing members form -->
      <template v-else-if="formMode === 'addExisting'">
        <a-form ref="addExistingFormRef" :model="addExistingForm" :rules="addExistingRules" layout="vertical">
          <a-form-item :label="t('systemUsers.selectUsers')" name="userIds">
            <a-select
              v-model:value="addExistingForm.userIds"
              mode="multiple"
              :options="tenantUserOptions"
              :loading="tenantUserLoading"
              :filter-option="false"
              show-search
              :placeholder="t('systemUsers.searchTenantUsers')"
              @search="handleTenantUserSearch"
              @focus="() => loadTenantUsers()"
            />
          </a-form-item>
          <a-form-item :label="t('systemUsers.roles')" name="roleIds">
            <a-select
              v-model:value="addExistingForm.roleIds"
              mode="multiple"
              :options="roleOptions"
              :loading="roleOptionsLoading"
              :filter-option="false"
              show-search
              @search="handleRoleSearch"
              @focus="() => loadRoleOptions()"
            />
          </a-form-item>
        </a-form>
      </template>

      <!-- Edit member form -->
      <template v-else-if="formMode === 'edit'">
        <a-tabs v-model:activeKey="editTab">
          <a-tab-pane key="profile" :tab="t('systemUsers.tabProfile')">
            <a-form ref="editFormRef" :model="editForm" :rules="editRules" layout="vertical">
              <a-form-item :label="t('systemUsers.displayName')" name="displayName">
                <a-input v-model:value="editForm.displayName" data-testid="app-users-edit-display-name" />
              </a-form-item>
              <a-form-item :label="t('systemUsers.email')" name="email">
                <a-input v-model:value="editForm.email" />
              </a-form-item>
              <a-form-item :label="t('systemUsers.phoneNumber')" name="phoneNumber">
                <a-input v-model:value="editForm.phoneNumber" />
              </a-form-item>
              <a-form-item :label="t('systemUsers.status')" name="isActive">
                <a-switch v-model:checked="editForm.isActive" />
              </a-form-item>
            </a-form>
          </a-tab-pane>
          <a-tab-pane key="roles" :tab="t('systemUsers.tabRoles')">
            <a-select
              v-model:value="editRoleIds"
              mode="multiple"
              :options="roleOptions"
              :loading="roleOptionsLoading"
              :filter-option="false"
              show-search
              style="width: 100%"
              @search="handleRoleSearch"
              @focus="() => loadRoleOptions()"
            />
          </a-tab-pane>
          <a-tab-pane key="departments" :tab="t('systemUsers.tabDepartments')">
            <a-select
              v-model:value="editDeptIds"
              mode="multiple"
              :options="deptOptions"
              :loading="deptOptionsLoading"
              :filter-option="false"
              show-search
              style="width: 100%"
              @search="handleDeptOptionSearch"
              @focus="() => loadDeptOptions()"
            />
          </a-tab-pane>
          <a-tab-pane key="positions" :tab="t('systemUsers.tabPositions')">
            <a-select
              v-model:value="editPositionIds"
              mode="multiple"
              :options="positionOptions"
              :loading="positionOptionsLoading"
              :filter-option="false"
              show-search
              style="width: 100%"
              @search="handlePositionSearch"
              @focus="() => loadPositionOptions()"
            />
          </a-tab-pane>
        </a-tabs>
      </template>

      <!-- Reset password form -->
      <template v-else-if="formMode === 'resetPassword'">
        <a-form ref="resetPwdFormRef" :model="resetPwdForm" :rules="resetPwdRules" layout="vertical">
          <a-form-item :label="t('systemUsers.newPassword')" name="newPassword">
            <a-input-password v-model:value="resetPwdForm.newPassword" data-testid="app-users-reset-password-input" />
          </a-form-item>
        </a-form>
      </template>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { MailOutlined, PhoneOutlined, PlusOutlined, UserAddOutlined } from "@ant-design/icons-vue";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { CrudPageLayout } from "@atlas/shared-ui";
import { debounce } from "@atlas/shared-core";
import type { SelectOption } from "@atlas/shared-core";
import { usePermission } from "@/composables/usePermission";
import { APP_PERMISSIONS } from "@/constants/permissions";
import { useAppContext } from "@/composables/useAppContext";
import {
  getMembersPaged,
  getMemberDetail,
  createMemberUser,
  addMembers,
  updateMemberRoles,
  updateMemberProfile,
  resetMemberPassword,
  removeMember,
  getRolesPaged,
  getDepartmentsPaged,
  getPositionsPaged,
} from "@/services/api-org-management";
import { searchTenantUsers } from "@/services/api-users";
import type { TenantAppMemberListItem, AppDepartmentListItem } from "@/types/organization";

type FormMode = "create" | "addExisting" | "edit" | "resetPassword";

const { t } = useI18n();
const { appId } = useAppContext();
const { hasPermission } = usePermission();
const isMounted = ref(false);

const canCreate = hasPermission(APP_PERMISSIONS.APP_MEMBERS_UPDATE);
const canUpdate = hasPermission(APP_PERMISSIONS.APP_MEMBERS_UPDATE);
const canDelete = hasPermission(APP_PERMISSIONS.APP_MEMBERS_UPDATE);

const keyword = ref("");
const treeKeyword = ref("");
const loading = ref(false);
const tableData = ref<TenantAppMemberListItem[]>([]);
const selectedDeptId = ref<string | undefined>(undefined);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t("crud.totalItems", { total }),
});

const columns = computed(() => [
  { title: t("systemUsers.colDisplayName"), key: "userInfo", width: 200 },
  { title: t("systemUsers.colEmail"), key: "contact", width: 220 },
  { title: t("systemUsers.colStatus"), key: "isActive", width: 80 },
  { title: t("systemUsers.colRoles"), key: "roles", width: 200 },
  { title: t("systemUsers.colActions"), key: "actions", width: 200, fixed: "right" as const },
]);

// Department tree
const allDepts = ref<AppDepartmentListItem[]>([]);
const selectedDeptKeys = computed(() => (selectedDeptId.value ? [selectedDeptId.value] : []));
const expandedDeptKeys = computed(() => {
  if (!treeKeyword.value.trim()) return [];
  return allDepts.value.map((d) => d.id);
});

interface TreeNode {
  key: string;
  title: string;
  children?: TreeNode[];
}

const buildDeptTree = (items: AppDepartmentListItem[]): TreeNode[] => {
  const map = new Map<string, TreeNode>();
  const roots: TreeNode[] = [];
  items.forEach((item) => map.set(item.id, { key: item.id, title: item.name, children: [] }));
  items.forEach((item) => {
    const node = map.get(item.id)!;
    if (item.parentId) {
      const parent = map.get(item.parentId);
      if (parent) {
        parent.children!.push(node);
        return;
      }
    }
    roots.push(node);
  });
  return roots;
};

const filterTree = (nodes: TreeNode[], kw: string): TreeNode[] => {
  if (!kw.trim()) return nodes;
  const m = kw.trim().toLowerCase();
  const result: TreeNode[] = [];
  nodes.forEach((n) => {
    const children = n.children ? filterTree(n.children, kw) : [];
    if (n.title.toLowerCase().includes(m) || children.length > 0) {
      result.push({ ...n, children });
    }
  });
  return result;
};

const deptTreeData = computed(() => filterTree(buildDeptTree(allDepts.value), treeKeyword.value));

const selectedDeptName = computed(() => {
  if (!selectedDeptId.value) return t("systemUsers.allDepartments");
  const d = allDepts.value.find((x) => x.id === selectedDeptId.value);
  return d?.name ?? t("systemUsers.allDepartments");
});

const memberStatText = computed(() => {
  const total = Number(pagination.total ?? 0);
  return selectedDeptId.value
    ? t("systemUsers.formalMembersUnderDepartment", { count: total })
    : t("systemUsers.formalMembersAll", { count: total });
});

const handleDeptSelect = (keys: (string | number)[]) => {
  selectedDeptId.value = keys.length ? String(keys[0]) : undefined;
  pagination.current = 1;
  void fetchData();
};

// Drawer state
const drawerVisible = ref(false);
const submitting = ref(false);
const formMode = ref<FormMode>("create");
const editTab = ref("profile");
const currentUserId = ref<string | null>(null);

const drawerTitle = computed(() => {
  switch (formMode.value) {
    case "create":
      return t("systemUsers.drawerCreateTitle");
    case "addExisting":
      return t("systemUsers.drawerAddExistingTitle");
    case "edit":
      return t("systemUsers.drawerEditTitle");
    case "resetPassword":
      return t("systemUsers.drawerResetPasswordTitle");
    default:
      return "";
  }
});

// Create form
const createFormRef = ref<FormInstance>();
const createForm = reactive({
  username: "",
  password: "",
  displayName: "",
  email: "",
  phoneNumber: "",
  isActive: true,
  roleIds: [] as string[],
  departmentIds: [] as string[],
  positionIds: [] as string[],
});
const createRules = {
  username: [{ required: true, message: t("systemUsers.usernameRequired") }],
  password: [{ required: true, message: t("systemUsers.passwordRequired"), min: 8 }],
  displayName: [{ required: true, message: t("systemUsers.displayNameRequired") }],
};

// Add existing form
const addExistingFormRef = ref<FormInstance>();
const addExistingForm = reactive({
  userIds: [] as string[],
  roleIds: [] as string[],
});
const addExistingRules = {
  userIds: [{ required: true, message: t("systemUsers.selectUsersRequired"), type: "array" as const }],
  roleIds: [{ required: true, message: t("systemUsers.selectRolesRequired"), type: "array" as const }],
};

// Edit form
const editFormRef = ref<FormInstance>();
const editForm = reactive({
  displayName: "",
  email: "",
  phoneNumber: "",
  isActive: true,
});
const editRules = {
  displayName: [{ required: true, message: t("systemUsers.displayNameRequired") }],
};
const editRoleIds = ref<string[]>([]);
const editDeptIds = ref<string[]>([]);
const editPositionIds = ref<string[]>([]);

// Reset password form
const resetPwdFormRef = ref<FormInstance>();
const resetPwdForm = reactive({ newPassword: "" });
const resetPwdRules = {
  newPassword: [{ required: true, message: t("systemUsers.newPasswordRequired"), min: 8 }],
};

// Select options
const roleOptions = ref<SelectOption[]>([]);
const roleOptionsLoading = ref(false);
const deptOptions = ref<SelectOption[]>([]);
const deptOptionsLoading = ref(false);
const positionOptions = ref<SelectOption[]>([]);
const positionOptionsLoading = ref(false);
const tenantUserOptions = ref<SelectOption[]>([]);
const tenantUserLoading = ref(false);

const loadRoleOptions = async (kw?: string) => {
  const id = appId.value;
  if (!id) return;
  roleOptionsLoading.value = true;
  try {
    const result = await getRolesPaged(id, { pageIndex: 1, pageSize: 20, keyword: kw?.trim() });
    if (!isMounted.value) return;
    roleOptions.value = result.items.map((r) => ({ label: r.name, value: r.id }));
  } finally {
    if (isMounted.value) roleOptionsLoading.value = false;
  }
};

const loadDeptOptions = async (kw?: string) => {
  const id = appId.value;
  if (!id) return;
  deptOptionsLoading.value = true;
  try {
    const result = await getDepartmentsPaged(id, { pageIndex: 1, pageSize: 20, keyword: kw?.trim() });
    if (!isMounted.value) return;
    deptOptions.value = result.items.map((d) => ({ label: d.name, value: d.id }));
  } finally {
    if (isMounted.value) deptOptionsLoading.value = false;
  }
};

const loadPositionOptions = async (kw?: string) => {
  const id = appId.value;
  if (!id) return;
  positionOptionsLoading.value = true;
  try {
    const result = await getPositionsPaged(id, { pageIndex: 1, pageSize: 20, keyword: kw?.trim() });
    if (!isMounted.value) return;
    positionOptions.value = result.items.map((p) => ({ label: p.name, value: p.id }));
  } finally {
    if (isMounted.value) positionOptionsLoading.value = false;
  }
};

const loadTenantUsers = async (kw?: string) => {
  tenantUserLoading.value = true;
  try {
    const result = await searchTenantUsers(kw?.trim() ?? "", 20);
    if (!isMounted.value) return;
    tenantUserOptions.value = result.items.map((u) => ({
      label: `${u.displayName} (${u.username})`,
      value: u.id,
    }));
  } finally {
    if (isMounted.value) tenantUserLoading.value = false;
  }
};

const handleRoleSearch = debounce((v: string) => void loadRoleOptions(v));
const handleDeptOptionSearch = debounce((v: string) => void loadDeptOptions(v));
const handlePositionSearch = debounce((v: string) => void loadPositionOptions(v));
const handleTenantUserSearch = debounce((v: string) => void loadTenantUsers(v));

// Data fetching
async function fetchData() {
  const id = appId.value;
  if (!id) return;

  loading.value = true;
  try {
    const result = await getMembersPaged(id, {
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value?.trim() || undefined,
      departmentId: selectedDeptId.value,
    });
    tableData.value = result.items;
    pagination.total = Number(result.total ?? 0);
  } catch {
    message.error(t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
}

async function loadDeptTree() {
  const id = appId.value;
  if (!id) return;
  try {
    const { getDepartmentsAll } = await import("@/services/api-org-management");
    allDepts.value = await getDepartmentsAll(id);
  } catch {
    /* ignore */
  }
}

function handleTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 10;
  void fetchData();
}

function handleSearch() {
  pagination.current = 1;
  void fetchData();
}

function handleReset() {
  keyword.value = "";
  selectedDeptId.value = undefined;
  pagination.current = 1;
  void fetchData();
}

function openCreateUser() {
  formMode.value = "create";
  createForm.username = "";
  createForm.password = "";
  createForm.displayName = "";
  createForm.email = "";
  createForm.phoneNumber = "";
  createForm.isActive = true;
  createForm.roleIds = [];
  createForm.departmentIds = [];
  createForm.positionIds = [];
  drawerVisible.value = true;
}

function openAddExistingMember() {
  formMode.value = "addExisting";
  addExistingForm.userIds = [];
  addExistingForm.roleIds = [];
  drawerVisible.value = true;
}

async function openEditMember(record: TenantAppMemberListItem) {
  const id = appId.value;
  if (!id) return;

  formMode.value = "edit";
  editTab.value = "profile";
  currentUserId.value = record.userId;

  try {
    const detail = await getMemberDetail(id, record.userId);
    editForm.displayName = detail.displayName;
    editForm.email = detail.email ?? "";
    editForm.phoneNumber = detail.phoneNumber ?? "";
    editForm.isActive = detail.isActive;
    editRoleIds.value = [...detail.roleIds];
    editDeptIds.value = [...detail.departmentIds];
    editPositionIds.value = [...detail.positionIds];

    ensureOptionsExist(roleOptions, detail.roleIds, detail.roleNames);
    ensureOptionsExist(deptOptions, detail.departmentIds, detail.departmentNames);
    ensureOptionsExist(positionOptions, detail.positionIds, detail.positionNames);

    drawerVisible.value = true;
  } catch {
    message.error(t("crud.queryFailed"));
  }
}

function ensureOptionsExist(optionsRef: import("vue").Ref<SelectOption[]>, ids: string[], names: string[]) {
  ids.forEach((id, idx) => {
    if (!optionsRef.value.some((o) => o.value === id)) {
      optionsRef.value.push({ label: names[idx] ?? id, value: id });
    }
  });
}

function openResetPassword(record: TenantAppMemberListItem) {
  formMode.value = "resetPassword";
  currentUserId.value = record.userId;
  resetPwdForm.newPassword = "";
  drawerVisible.value = true;
}

function closeDrawer() {
  drawerVisible.value = false;
}

async function handleSubmit() {
  const id = appId.value;
  if (!id) return;

  submitting.value = true;
  try {
    if (formMode.value === "create") {
      await createFormRef.value?.validate();
      await createMemberUser(id, {
        username: createForm.username,
        password: createForm.password,
        displayName: createForm.displayName,
        email: createForm.email || undefined,
        phoneNumber: createForm.phoneNumber || undefined,
        isActive: createForm.isActive,
        roleIds: createForm.roleIds,
        departmentIds: createForm.departmentIds.length ? createForm.departmentIds : undefined,
        positionIds: createForm.positionIds.length ? createForm.positionIds : undefined,
      });
      message.success(t("crud.createSuccess"));
    } else if (formMode.value === "addExisting") {
      await addExistingFormRef.value?.validate();
      await addMembers(id, {
        userIds: addExistingForm.userIds,
        roleIds: addExistingForm.roleIds,
      });
      message.success(t("systemUsers.addExistingSuccess"));
    } else if (formMode.value === "edit" && currentUserId.value) {
      await editFormRef.value?.validate();
      await Promise.all([
        updateMemberProfile(id, currentUserId.value, {
          displayName: editForm.displayName,
          email: editForm.email || undefined,
          phoneNumber: editForm.phoneNumber || undefined,
          isActive: editForm.isActive,
        }),
        updateMemberRoles(id, currentUserId.value, {
          roleIds: editRoleIds.value,
          departmentIds: editDeptIds.value.length ? editDeptIds.value : undefined,
          positionIds: editPositionIds.value.length ? editPositionIds.value : undefined,
        }),
      ]);
      message.success(t("crud.updateSuccess"));
    } else if (formMode.value === "resetPassword" && currentUserId.value) {
      await resetPwdFormRef.value?.validate();
      await resetMemberPassword(id, currentUserId.value, {
        newPassword: resetPwdForm.newPassword,
      });
      message.success(t("systemUsers.resetPasswordSuccess"));
    }
    closeDrawer();
    void fetchData();
  } catch (e: unknown) {
    if (e instanceof Error && e.message) {
      message.error(e.message);
    }
  } finally {
    submitting.value = false;
  }
}

async function handleRemoveMember(userId: string) {
  const id = appId.value;
  if (!id) return;

  try {
    await removeMember(id, userId);
    message.success(t("systemUsers.removeMemberSuccess"));
    void fetchData();
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("crud.deleteFailed"));
  }
}

onMounted(() => {
  isMounted.value = true;
  void fetchData();
  void loadDeptTree();
  void loadRoleOptions();
  void loadDeptOptions();
  void loadPositionOptions();
});

onUnmounted(() => {
  isMounted.value = false;
});
</script>

<style scoped>
.app-users-layout {
  flex: 1;
  overflow: hidden;
}

.app-users-tree-card {
  height: 100%;
  overflow-y: auto;
  padding: 16px;
  background: #ffffff;
  border-radius: 12px;
  box-shadow: 0 1px 2px rgba(16, 24, 40, 0.06);
  border: 1px solid #e5e7eb;
}

.app-users-tree-search {
  margin-bottom: 12px;
}

.app-users-main-col {
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.app-users-right-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
  flex-shrink: 0;
}

.app-users-dept-title {
  font-size: 20px;
  font-weight: 700;
  color: #101828;
  line-height: 1.3;
}

.app-users-dept-stat {
  margin-top: 4px;
  font-size: 14px;
  color: #6a7282;
}

.app-users-outline-btn {
  border-color: #d0d5dd;
  color: #364153;
}

.app-users-create-btn {
  background-color: #4f39f6 !important;
  border-color: #4f39f6 !important;
}

.app-users-create-btn:hover {
  background-color: #4338ca !important;
  border-color: #4338ca !important;
}

.app-users-role-tag {
  margin: 2px;
}
</style>
