<template>
  <CrudPageLayout
    data-testid="app-roles-page"
    v-model:keyword="keyword"
    :title="t('systemRoles.pageTitle')"
    :subtitle="t('systemRoles.pageSubtitle')"
    :search-placeholder="t('systemRoles.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? t('systemRoles.drawerCreateTitle') : t('systemRoles.drawerEditTitle')"
    :drawer-width="520"
    :submit-loading="submitting"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #card-extra>
      <a-button
        v-if="canCreate"
        type="primary"
        class="roles-add-btn"
        data-testid="app-roles-create"
        @click="openCreate"
      >
        <template #icon><PlusOutlined /></template>
        {{ t("systemRoles.addRole") }}
      </a-button>
    </template>

    <template #search-filters>
      <a-form-item>
        <a-select
          v-model:value="systemFilter"
          :options="systemOptions"
          style="width: 160px"
          @change="handleSearch"
        />
      </a-form-item>
    </template>

    <template #table>
      <a-table
        data-testid="app-roles-table"
        :columns="tableColumns"
        :data-source="tableData"
        :pagination="pagination"
        :loading="loading"
        row-key="id"
        :scroll="{ x: 'max-content' }"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'roleName'">
            <a-space align="center" :size="8" wrap>
              <span class="role-icon-wrap" aria-hidden="true">
                <SafetyCertificateOutlined />
              </span>
              <span class="role-name-text">{{ record.name }}</span>
              <a-tag v-if="record.isSystem" color="blue">{{ t("systemRoles.systemTag") }}</a-tag>
            </a-space>
          </template>
          <template v-else-if="column.key === 'description'">
            <span class="role-description-text">
              {{ record.description?.trim() ? record.description : t("common.none") }}
            </span>
          </template>
          <template v-else-if="column.key === 'linkedUsers'">
            <a-space :size="6">
              <TeamOutlined class="role-linked-users-icon" aria-hidden="true" />
              <span>{{ t("systemRoles.linkedUsersCount", { count: record.memberCount }) }}</span>
            </a-space>
          </template>
          <template v-else-if="column.key === 'roleStatus'">
            <a-tag v-if="record.isSystem" color="blue">{{ t("systemRoles.systemTag") }}</a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space @click.stop>
              <a-button
                v-if="canUpdate"
                type="link"
                :data-testid="`app-roles-edit-${record.id}`"
                @click="openEdit(record)"
              >
                {{ t("common.edit") }}
              </a-button>
              <a-button
                v-if="canUpdate"
                type="link"
                :data-testid="`app-roles-assign-${record.id}`"
                @click="openAssign(record)"
              >
                {{ t("systemRoles.assignPermissions") }}
              </a-button>
              <a-popconfirm
                v-if="canDelete"
                :title="t('systemRoles.deleteConfirm')"
                :ok-text="t('common.delete')"
                :cancel-text="t('common.cancel')"
                @confirm="handleDeleteRole(record.id)"
              >
                <a-button type="link" danger :data-testid="`app-roles-delete-${record.id}`">
                  {{ t("common.delete") }}
                </a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <div class="form-wrapper">
        <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
          <a-form-item :label="t('systemRoles.roleName')" name="name">
            <a-input
              v-model:value="formModel.name"
              data-testid="app-roles-form-name"
              :placeholder="t('systemRoles.roleNamePlaceholder')"
            />
          </a-form-item>
          <a-form-item :label="t('systemRoles.roleCode')" name="code">
            <a-input
              v-model:value="formModel.code"
              data-testid="app-roles-form-code"
              :placeholder="t('systemRoles.roleCodePlaceholder')"
              :disabled="formMode === 'edit'"
            />
          </a-form-item>
          <a-form-item :label="t('systemRoles.description')" name="description">
            <a-textarea
              v-model:value="formModel.description"
              :placeholder="t('systemRoles.descriptionPlaceholder')"
              :rows="4"
            />
          </a-form-item>
        </a-form>
      </div>
    </template>

    <template #extra-drawers>
      <a-drawer
        v-model:open="assignDrawerOpen"
        :title="t('systemRoles.assignPermissions')"
        placement="right"
        :width="assignDrawerWidth"
        destroy-on-close
        :footer="null"
        data-testid="app-roles-assign-drawer"
        @close="closeAssignDrawer"
      >
        <AppRoleAssignPanel
          v-if="assignRole && appId"
          :app-id="appId"
          :role-id="assignRole.id"
          :role-code="assignRole.code"
          :role-name="assignRole.name"
          :can-assign-permissions="canUpdate"
          :can-manage-data-scope="canUpdate"
          @success="handleAssignSuccess"
        />
      </a-drawer>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import type { ColumnType } from "ant-design-vue/es/table";
import { message } from "ant-design-vue";
import { PlusOutlined, SafetyCertificateOutlined, TeamOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import { CrudPageLayout } from "@atlas/shared-ui";
import AppRoleAssignPanel from "@/components/system/roles/AppRoleAssignPanel.vue";
import { usePermission } from "@/composables/usePermission";
import { APP_PERMISSIONS } from "@/constants/permissions";
import { useAppContext } from "@/composables/useAppContext";
import {
  getRolesPaged,
  createRole,
  updateRole,
  deleteRole,
  type RoleQueryRequest,
} from "@/services/api-org-management";
import type { TenantAppRoleListItem } from "@/types/organization";

type FormMode = "create" | "edit";

const { t } = useI18n();
const { appId } = useAppContext();
const { hasPermission } = usePermission();

const canCreate = hasPermission(APP_PERMISSIONS.APP_ROLES_UPDATE);
const canUpdate = hasPermission(APP_PERMISSIONS.APP_ROLES_UPDATE);
const canDelete = hasPermission(APP_PERMISSIONS.APP_ROLES_UPDATE);

const keyword = ref("");
const loading = ref(false);
const tableData = ref<TenantAppRoleListItem[]>([]);
const systemFilter = ref<"all" | "system" | "custom">("all");

const assignDrawerOpen = ref(false);
const assignRole = ref<TenantAppRoleListItem | null>(null);
const assignDrawerWidth = "min(960px, 100vw)";

const systemOptions = computed(() => [
  { label: t("systemRoles.filterAll"), value: "all" },
  { label: t("systemRoles.filterSystem"), value: "system" },
  { label: t("systemRoles.filterCustom"), value: "custom" },
]);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t("crud.totalItems", { total }),
});

const tableColumns = computed((): ColumnType<TenantAppRoleListItem>[] => [
  {
    title: t("systemRoles.colRoleName"),
    key: "roleName",
    dataIndex: "name",
    ellipsis: true,
  },
  {
    title: t("systemRoles.description"),
    key: "description",
    dataIndex: "description",
    ellipsis: true,
  },
  {
    title: t("systemRoles.colMemberCount"),
    key: "linkedUsers",
    dataIndex: "memberCount",
    width: 140,
  },
  {
    title: t("systemUsers.colStatus"),
    key: "roleStatus",
    width: 120,
  },
  {
    title: t("systemRoles.colActions"),
    key: "actions",
    width: 260,
    fixed: "right",
  },
]);

const formVisible = ref(false);
const formMode = ref<FormMode>("create");
const formRef = ref<FormInstance>();
const submitting = ref(false);
const currentRoleId = ref<string | null>(null);
const formModel = reactive({ name: "", code: "", description: "" });
const formRules = {
  name: [{ required: true, message: t("systemRoles.nameRequired") }],
  code: [{ required: true, message: t("systemRoles.codeRequired") }],
};

async function fetchData() {
  const id = appId.value;
  if (!id) return;

  loading.value = true;
  try {
    const params: RoleQueryRequest = {
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value?.trim() || undefined,
      isSystem: systemFilter.value === "all" ? undefined : systemFilter.value === "system",
    };
    const result = await getRolesPaged(id, params);
    tableData.value = result.items;
    pagination.total = result.total;
  } catch {
    message.error(t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
}

function onTableChange(pag: TablePaginationConfig) {
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
  systemFilter.value = "all";
  pagination.current = 1;
  void fetchData();
}

function openCreate() {
  formMode.value = "create";
  currentRoleId.value = null;
  formModel.name = "";
  formModel.code = "";
  formModel.description = "";
  formVisible.value = true;
}

function openEdit(record: TenantAppRoleListItem) {
  formMode.value = "edit";
  currentRoleId.value = record.id;
  formModel.name = record.name;
  formModel.code = record.code;
  formModel.description = record.description ?? "";
  formVisible.value = true;
}

function openAssign(record: TenantAppRoleListItem) {
  assignRole.value = record;
  assignDrawerOpen.value = true;
}

function closeAssignDrawer() {
  assignDrawerOpen.value = false;
  assignRole.value = null;
}

function closeForm() {
  formVisible.value = false;
}

async function submitForm() {
  const id = appId.value;
  if (!id) return;

  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  submitting.value = true;
  try {
    if (formMode.value === "create") {
      await createRole(id, {
        code: formModel.code,
        name: formModel.name,
        description: formModel.description || undefined,
      });
      message.success(t("crud.createSuccess"));
    } else if (currentRoleId.value) {
      await updateRole(id, currentRoleId.value, {
        name: formModel.name,
        description: formModel.description || undefined,
      });
      message.success(t("crud.updateSuccess"));
    }
    closeForm();
    await fetchData();
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("crud.operationFailed"));
  } finally {
    submitting.value = false;
  }
}

async function handleDeleteRole(roleId: string) {
  const id = appId.value;
  if (!id) return;

  try {
    await deleteRole(id, roleId);
    message.success(t("crud.deleteSuccess"));
    await fetchData();
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("crud.deleteFailed"));
  }
}

function handleAssignSuccess() {
  void fetchData();
}

onMounted(() => {
  void fetchData();
});
</script>

<style scoped>
.roles-add-btn {
  background: #722ed1 !important;
  border-color: #722ed1 !important;
}

.roles-add-btn:hover,
.roles-add-btn:focus {
  background: #9254de !important;
  border-color: #9254de !important;
}

.role-icon-wrap {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: var(--color-primary-bg, #f9f0ff);
  color: #722ed1;
  font-size: 16px;
}

.role-name-text {
  font-weight: 600;
  color: var(--color-text, rgba(0, 0, 0, 0.88));
}

.role-description-text {
  color: var(--color-text-secondary, rgba(0, 0, 0, 0.45));
}

.role-linked-users-icon {
  color: var(--color-text-secondary, rgba(0, 0, 0, 0.45));
}

.form-wrapper {
  padding-right: 8px;
}
</style>
