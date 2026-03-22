<template>
  <div class="app-users-page">
    <a-page-header :title="t('appsUsers.pageTitle')" :sub-title="t('appsUsers.pageSubtitle')" />

    <a-card class="mt12">
      <template #extra>
        <a-space>
          <a-input-search
            v-model:value="keyword"
            style="width: 260px"
            :placeholder="t('appsUsers.searchPlaceholder')"
            allow-clear
            @search="handleSearch"
          />
          <a-button
            v-if="canManageMembers"
            type="primary"
            :loading="loading"
            @click="openAddMemberModal"
          >
            {{ t("appsUsers.addMember") }}
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
              {{ record.isActive ? t("appsUsers.statusEnabled") : t("appsUsers.statusDisabled") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'roles'">
            <a-space wrap>
              <a-tag v-for="roleName in record.roleNames" :key="roleName" color="blue">
                {{ roleName }}
              </a-tag>
              <span v-if="record.roleNames.length === 0" class="placeholder">{{ t("appsUsers.noRoles") }}</span>
            </a-space>
          </template>
          <template v-else-if="column.key === 'joinedAt'">
            {{ formatDateTime(record.joinedAt) }}
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space v-if="canManageMembers">
              <a-button v-if="canViewAppRoles" type="link" @click="openEditRolesModal(record)">
                {{ t("appsUsers.assignRoles") }}
              </a-button>
              <a-popconfirm
                :title="t('appsUsers.removeConfirm')"
                :ok-text="t('appsUsers.removeOk')"
                :cancel-text="t('common.cancel')"
                @confirm="removeMember(record.userId)"
              >
                <a-button type="link" danger>
                  {{ t("appsUsers.removeOk") }}
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
      :title="t('appsUsers.modalAddTitle')"
      :confirm-loading="submitting"
      :ok-text="t('appsUsers.modalAddOk')"
      :cancel-text="t('common.cancel')"
      @ok="submitAddMembers"
    >
      <a-alert
        :message="t('appsUsers.selectHint')"
        type="info"
        show-icon
        style="margin-bottom: 12px"
      />
      <a-form layout="vertical">
        <a-form-item :label="t('appsUsers.labelMembers')" required>
          <a-select
            v-model:value="addForm.userIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="userOptions"
            :loading="userOptionsLoading"
            :placeholder="t('appsUsers.memberPlaceholder')"
            @focus="() => loadUserOptions()"
            @search="handleUserSearch"
          />
        </a-form-item>
        <a-form-item v-if="canViewAppRoles" :label="t('appsUsers.labelAppRoles')">
          <a-select
            v-model:value="addForm.roleIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="roleOptions"
            :loading="roleOptionsLoading"
            :placeholder="t('appsUsers.rolePlaceholder')"
            @focus="() => loadRoleOptions()"
            @search="handleRoleSearch"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="editRolesModalOpen"
      :title="t('appsUsers.modalRolesTitle')"
      :confirm-loading="submitting"
      :ok-text="t('common.save')"
      :cancel-text="t('common.cancel')"
      @ok="submitUpdateMemberRoles"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('appsUsers.labelMemberUser')">
          <a-input :value="editingMemberDisplayName" disabled />
        </a-form-item>
        <a-form-item v-if="canViewAppRoles" :label="t('appsUsers.labelRoles')">
          <a-select
            v-model:value="editRolesForm.roleIds"
            mode="multiple"
            show-search
            :filter-option="false"
            :options="roleOptions"
            :loading="roleOptionsLoading"
            :placeholder="t('appsUsers.rolePlaceholder')"
            @focus="() => loadRoleOptions()"
            @search="handleRoleSearch"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

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

const { t } = useI18n();
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
  showTotal: (total) => t("crud.totalItems", { total })
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
  { title: t("appsUsers.colUsername"), dataIndex: "username", key: "username", width: 180 },
  { title: t("appsUsers.colDisplayName"), dataIndex: "displayName", key: "displayName", width: 220 },
  { title: t("appsUsers.colStatus"), key: "status", width: 100 },
  { title: t("appsUsers.colRoles"), key: "roles", width: 280 },
  { title: t("appsUsers.colJoinedAt"), key: "joinedAt", width: 180 },
  { title: t("appsUsers.colActions"), key: "actions", width: 180, fixed: "right" }
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
    const result  = await getTenantAppMembersPaged(appId.value, request);

    if (!isMounted.value) return;
    members.value = result.items;
    pagination.total = result.total;
    pagination.current = result.pageIndex;
    pagination.pageSize = result.pageSize;
  } catch (error) {
    members.value = [];
    pagination.total = 0;
    message.error((error as Error).message || t("appsUsers.loadMembersFailed"));
  } finally {
    loading.value = false;
  }
}

async function loadUserOptions(keywordText?: string) {
  userOptionsLoading.value = true;
  try {
    const result  = await getUsersPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordText?.trim() || undefined
    });

    if (!isMounted.value) return;
    userOptions.value = result.items.map((item) => ({
      label: `${item.displayName} (${item.username})`,
      value: item.id
    }));
  } catch (error) {
    message.error((error as Error).message || t("appsUsers.loadUsersFailed"));
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
    const result  = await getTenantAppRolesPaged(appId.value, {
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordText?.trim() || undefined
    });

    if (!isMounted.value) return;
    roleOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.code})`,
      value: item.id
    }));
  } catch (error) {
    message.error((error as Error).message || t("appsUsers.loadRolesFailed"));
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
    message.warning(t("appsUsers.pickOneUser"));
    return;
  }

  submitting.value = true;
  try {
    await addTenantAppMembers(appId.value, {
      userIds: addForm.userIds.map((id) => Number(id)),
      roleIds: addForm.roleIds.map((id) => Number(id))
    });

    if (!isMounted.value) return;
    message.success(t("appsUsers.addSuccess"));
    addMemberModalOpen.value = false;
    await loadMembers();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("appsUsers.addFailed"));
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

    if (!isMounted.value) return;
    message.success(t("appsUsers.rolesUpdated"));
    editRolesModalOpen.value = false;
    await loadMembers();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("appsUsers.rolesUpdateFailed"));
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

    if (!isMounted.value) return;
    message.success(t("appsUsers.removed"));
    await loadMembers();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("appsUsers.removeFailed"));
  }
}

onMounted(() => {
  void loadAppAndMembers();
});

async function loadAppAndMembers() {
  if (!appId.value) {
    return;
  }
  void loadMembers();
}
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
