<template>
  <div class="app-users-page">
    <a-page-header title="应用成员" sub-title="管理应用级成员与角色绑定" />

    <a-card class="mt12">
      <template #extra>
        <a-space>
          <a-input-search
            v-model:value="keyword"
            style="width: 260px"
            placeholder="按用户名/显示名搜索成员"
            allow-clear
            @search="handleSearch"
          />
          <a-button
            v-if="canManageMembers"
            type="primary"
            :loading="loading"
            @click="openAddMemberModal"
          >
            添加成员
          </a-button>
        </a-space>
      </template>

      <a-table
        row-key="userId"
        :columns="columns"
        :data-source="members"
        :loading="loading"
        :pagination="pagination"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.isActive ? 'green' : 'default'">
              {{ record.isActive ? "启用" : "禁用" }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'roles'">
            <a-space wrap>
              <a-tag v-for="roleName in record.roleNames" :key="roleName" color="blue">
                {{ roleName }}
              </a-tag>
              <span v-if="record.roleNames.length === 0" class="placeholder">未绑定角色</span>
            </a-space>
          </template>
          <template v-else-if="column.key === 'joinedAt'">
            {{ formatDateTime(record.joinedAt) }}
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space v-if="canManageMembers">
              <a-button v-if="canViewAppRoles" type="link" @click="openEditRolesModal(record)">
                分配角色
              </a-button>
              <a-popconfirm
                title="确认移除该成员吗？"
                ok-text="移除"
                cancel-text="取消"
                @confirm="removeMember(record.userId)"
              >
                <a-button type="link" danger>
                  移除
                </a-button>
              </a-popconfirm>
            </a-space>
            <span v-else class="placeholder">-</span>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="addMemberModalOpen"
      title="添加应用成员"
      :confirm-loading="submitting"
      ok-text="确认添加"
      cancel-text="取消"
      @ok="submitAddMembers"
    >
      <a-alert
        message="用户下拉默认展示 20 条，并支持远程搜索"
        type="info"
        show-icon
        style="margin-bottom: 12px"
      />
      <a-form layout="vertical">
        <a-form-item label="成员用户" required>
          <a-select
            v-model:value="addForm.userIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="userOptions"
            :loading="userOptionsLoading"
            placeholder="请选择成员用户（可搜索）"
            @focus="() => loadUserOptions()"
            @search="handleUserSearch"
          />
        </a-form-item>
        <a-form-item v-if="canViewAppRoles" label="应用角色（可选）">
          <a-select
            v-model:value="addForm.roleIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="roleOptions"
            :loading="roleOptionsLoading"
            placeholder="请选择应用角色（可搜索）"
            @focus="() => loadRoleOptions()"
            @search="handleRoleSearch"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="editRolesModalOpen"
      title="分配应用角色"
      :confirm-loading="submitting"
      ok-text="保存"
      cancel-text="取消"
      @ok="submitUpdateMemberRoles"
    >
      <a-form layout="vertical">
        <a-form-item label="成员用户">
          <a-input :value="editingMemberDisplayName" disabled />
        </a-form-item>
        <a-form-item v-if="canViewAppRoles" label="应用角色">
          <a-select
            v-model:value="editRolesForm.roleIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="roleOptions"
            :loading="roleOptionsLoading"
            placeholder="请选择应用角色（可搜索）"
            @focus="() => loadRoleOptions()"
            @search="handleRoleSearch"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useRoute } from "vue-router";
import { debounce, formatDateTime } from "@/utils/common";
import { useUserStore } from "@/stores/user";
import { isAdminRole } from "@/utils/auth";
import { getUsersPaged } from "@/services/api-users";
import {
  addTenantAppMembers,
  getTenantAppMembersPaged,
  getTenantAppRolesPaged,
  removeTenantAppMember,
  updateTenantAppMemberRoles
} from "@/services/api-app-members";
import type { PagedRequest } from "@/types/api";
import type { TenantAppMemberListItem } from "@/types/platform-v2";

type SelectOption = { label: string; value: string };

const route = useRoute();
const userStore = useUserStore();
const appId = computed(() => String(route.params.appId ?? ""));

const loading = ref(false);
const submitting = ref(false);
const members = ref<TenantAppMemberListItem[]>([]);
const keyword = ref("");
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total) => `共 ${total} 条`
});

const addMemberModalOpen = ref(false);
const editRolesModalOpen = ref(false);
const editingUserId = ref<string>("");
const editingMemberDisplayName = ref<string>("");

const userOptions = ref<SelectOption[]>([]);
const roleOptions = ref<SelectOption[]>([]);
const userOptionsLoading = ref(false);
const roleOptionsLoading = ref(false);

const addForm = reactive({
  userIds: [] as string[],
  roleIds: [] as string[]
});

const editRolesForm = reactive({
  roleIds: [] as string[]
});

const canManageMembers = computed(() => {
  return userStore.permissions.includes("apps:members:update") || isAdminRole(userStore.profile);
});

const canViewAppRoles = computed(() => {
  return (
    userStore.permissions.includes("apps:roles:view") ||
    userStore.permissions.includes("apps:roles:update") ||
    isAdminRole(userStore.profile)
  );
});

const columns = computed<TableColumnsType<TenantAppMemberListItem>>(() => [
  {
    title: "用户名",
    dataIndex: "username",
    key: "username",
    width: 180
  },
  {
    title: "显示名",
    dataIndex: "displayName",
    key: "displayName",
    width: 220
  },
  {
    title: "状态",
    key: "status",
    width: 100
  },
  {
    title: "角色",
    key: "roles",
    width: 280
  },
  {
    title: "加入时间",
    key: "joinedAt",
    width: 180
  },
  {
    title: "操作",
    key: "actions",
    width: 180,
    fixed: "right"
  }
]);

async function loadMembers() {
  if (!appId.value) {
    return;
  }

  loading.value = true;
  try {
    const request: PagedRequest = {
      pageIndex: Number(pagination.current ?? 1),
      pageSize: Number(pagination.pageSize ?? 10),
      keyword: keyword.value.trim() || undefined
    };
    const result = await getTenantAppMembersPaged(appId.value, request);
    members.value = result.items;
    pagination.total = result.total;
    pagination.current = result.pageIndex;
    pagination.pageSize = result.pageSize;
  } catch (error) {
    members.value = [];
    pagination.total = 0;
    message.error((error as Error).message || "加载应用成员失败");
  } finally {
    loading.value = false;
  }
}

async function loadUserOptions(keywordText?: string) {
  userOptionsLoading.value = true;
  try {
    const result = await getUsersPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordText?.trim() || undefined
    });
    userOptions.value = result.items.map((item) => ({
      label: `${item.displayName} (${item.username})`,
      value: item.id
    }));
  } catch (error) {
    message.error((error as Error).message || "加载用户选项失败");
  } finally {
    userOptionsLoading.value = false;
  }
}

async function loadRoleOptions(keywordText?: string) {
  if (!appId.value) {
    return;
  }
  if (!canViewAppRoles.value) {
    roleOptions.value = [];
    return;
  }

  roleOptionsLoading.value = true;
  try {
    const result = await getTenantAppRolesPaged(appId.value, {
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordText?.trim() || undefined
    });
    roleOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.code})`,
      value: item.id
    }));
  } catch (error) {
    message.error((error as Error).message || "加载应用角色失败");
  } finally {
    roleOptionsLoading.value = false;
  }
}

const handleUserSearch = debounce((value?: string) => {
  void loadUserOptions(value);
}, 300);

const handleRoleSearch = debounce((value?: string) => {
  void loadRoleOptions(value);
}, 300);

function handleTableChange(page: TablePaginationConfig) {
  pagination.current = page.current ?? 1;
  pagination.pageSize = page.pageSize ?? 10;
  void loadMembers();
}

function handleSearch() {
  pagination.current = 1;
  void loadMembers();
}

function resetAddForm() {
  addForm.userIds = [];
  addForm.roleIds = [];
}

function openAddMemberModal() {
  resetAddForm();
  addMemberModalOpen.value = true;
  void loadUserOptions();
  if (canViewAppRoles.value) {
    void loadRoleOptions();
  }
}

async function submitAddMembers() {
  if (!appId.value) {
    return;
  }

  if (addForm.userIds.length === 0) {
    message.warning("请至少选择一名成员用户");
    return;
  }

  submitting.value = true;
  try {
    await addTenantAppMembers(appId.value, {
      userIds: addForm.userIds.map((id) => Number(id)),
      roleIds: addForm.roleIds.map((id) => Number(id))
    });
    message.success("添加成员成功");
    addMemberModalOpen.value = false;
    await loadMembers();
  } catch (error) {
    message.error((error as Error).message || "添加成员失败");
  } finally {
    submitting.value = false;
  }
}

function openEditRolesModal(record: TenantAppMemberListItem) {
  if (!canViewAppRoles.value) {
    return;
  }

  editingUserId.value = record.userId;
  editingMemberDisplayName.value = `${record.displayName} (${record.username})`;
  editRolesForm.roleIds = [...record.roleIds];
  editRolesModalOpen.value = true;
  void loadRoleOptions();
}

async function submitUpdateMemberRoles() {
  if (!appId.value || !editingUserId.value) {
    return;
  }

  submitting.value = true;
  try {
    await updateTenantAppMemberRoles(appId.value, editingUserId.value, {
      roleIds: editRolesForm.roleIds.map((id) => Number(id))
    });
    message.success("成员角色已更新");
    editRolesModalOpen.value = false;
    await loadMembers();
  } catch (error) {
    message.error((error as Error).message || "更新成员角色失败");
  } finally {
    submitting.value = false;
  }
}

async function removeMember(userId: string) {
  if (!appId.value) {
    return;
  }

  try {
    await removeTenantAppMember(appId.value, userId);
    message.success("成员已移除");
    await loadMembers();
  } catch (error) {
    message.error((error as Error).message || "移除成员失败");
  }
}

onMounted(() => {
  void loadMembers();
});
</script>

<style scoped>
.app-users-page {
  padding: 8px;
}

.mt12 {
  margin-top: 12px;
}

.placeholder {
  color: #999;
}
</style>
