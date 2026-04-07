<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('systemRoles.pageTitle')"
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
    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" @click="openCreate">
        <template #icon><PlusOutlined /></template>
        {{ t("systemRoles.addRole") }}
      </a-button>
    </template>
    <template #toolbar-right>
      <a-popconfirm
        v-if="canDelete"
        :title="t('systemRoles.batchDeleteConfirm')"
        :ok-text="t('common.delete')"
        :cancel-text="t('common.cancel')"
        @confirm="handleBatchDelete"
      >
        <a-button danger :disabled="!selectedRowKeys.length">{{ t("systemRoles.batchDelete") }}</a-button>
      </a-popconfirm>
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
      <MasterDetailLayout :detail-visible="isDetailVisible" :master-width="700">
        <template #master>
          <a-table
            :columns="tableColumns"
            :data-source="tableData"
            :pagination="pagination"
            :loading="loading"
            :row-selection="rowSelection"
            :custom-row="customRow"
            row-key="id"
            @change="onTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'isSystem'">
                <a-tag v-if="record.isSystem" color="blue">{{ t("systemRoles.systemTag") }}</a-tag>
                <span v-else>-</span>
              </template>
              <template v-if="column.key === 'actions'">
                <a-space @click.stop>
                  <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
                  <a-button v-if="canUpdate" type="link" @click="openAssign(record)">
                    {{ t("systemRoles.assignPermissions") }}
                  </a-button>
                  <a-popconfirm
                    v-if="canDelete"
                    :title="t('systemRoles.deleteConfirm')"
                    :ok-text="t('common.delete')"
                    :cancel-text="t('common.cancel')"
                    @confirm="handleDeleteRole(record.id)"
                  >
                    <a-button type="link" danger>{{ t("common.delete") }}</a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </template>
        <template #detail>
          <AppRoleAssignPanel
            v-if="selectedItem"
            :app-id="appId ?? ''"
            :role-id="selectedItem.id"
            :role-code="selectedItem.code"
            :role-name="selectedItem.name"
            :can-assign-permissions="canUpdate"
            :can-manage-data-scope="canUpdate"
            @success="handleAssignSuccess"
          />
        </template>
      </MasterDetailLayout>
    </template>

    <template #form>
      <div class="form-wrapper">
        <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
          <a-form-item :label="t('systemRoles.roleName')" name="name">
            <a-input v-model:value="formModel.name" :placeholder="t('systemRoles.roleNamePlaceholder')" />
          </a-form-item>
          <a-form-item :label="t('systemRoles.roleCode')" name="code">
            <a-input
              v-model:value="formModel.code"
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
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { PlusOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import { CrudPageLayout } from "@atlas/shared-ui";
import MasterDetailLayout from "@/components/layout/MasterDetailLayout.vue";
import AppRoleAssignPanel from "@/components/system/roles/AppRoleAssignPanel.vue";
import { useMasterDetail } from "@/composables/useMasterDetail";
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
const selectedRowKeys = ref<string[]>([]);

const systemOptions = computed(() => [
  { label: t("systemRoles.filterAll"), value: "all" },
  { label: t("systemRoles.filterSystem"), value: "system" },
  { label: t("systemRoles.filterCustom"), value: "custom" }
]);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t("crud.totalItems", { total })
});

const tableColumns = computed(() => [
  { title: t("systemRoles.colRoleName"), dataIndex: "name", key: "name" },
  { title: t("systemRoles.colRoleCode"), dataIndex: "code", key: "code" },
  { title: t("systemRoles.colMemberCount"), dataIndex: "memberCount", key: "memberCount", width: 100 },
  { title: t("systemRoles.colSystem"), key: "isSystem", width: 100 },
  { title: t("systemRoles.colActions"), key: "actions", width: 250 }
]);

const rowSelection = computed(() => {
  if (!canDelete) return undefined;
  return {
    selectedRowKeys: selectedRowKeys.value,
    onChange: (keys: (string | number)[]) => {
      selectedRowKeys.value = keys.map(String);
    }
  };
});

// Master-detail
const { selectedItem, isDetailVisible, selectItem } = useMasterDetail<TenantAppRoleListItem>();

const customRow = (record: TenantAppRoleListItem) => ({
  onClick: () => { void selectItem(record); },
  style: {
    cursor: "pointer",
    backgroundColor: selectedItem.value?.id === record.id ? "var(--color-primary-bg)" : undefined
  }
});

// Form state
const formVisible = ref(false);
const formMode = ref<FormMode>("create");
const formRef = ref<FormInstance>();
const submitting = ref(false);
const currentRoleId = ref<string | null>(null);
const formModel = reactive({ name: "", code: "", description: "" });
const formRules = {
  name: [{ required: true, message: t("systemRoles.nameRequired") }],
  code: [{ required: true, message: t("systemRoles.codeRequired") }]
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
      isSystem: systemFilter.value === "all" ? undefined : systemFilter.value === "system"
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
  selectedRowKeys.value = [];
  pagination.current = 1;
  void fetchData();
}

function handleReset() {
  keyword.value = "";
  systemFilter.value = "all";
  selectedRowKeys.value = [];
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
  void selectItem(record);
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
        description: formModel.description || undefined
      });
      message.success(t("crud.createSuccess"));
    } else if (currentRoleId.value) {
      await updateRole(id, currentRoleId.value, {
        name: formModel.name,
        description: formModel.description || undefined
      });
      message.success(t("crud.updateSuccess"));
    }
    closeForm();
    void fetchData();
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
    void fetchData();
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("crud.deleteFailed"));
  }
}

async function handleBatchDelete() {
  const id = appId.value;
  if (!id || !selectedRowKeys.value.length) return;

  try {
    await Promise.all(selectedRowKeys.value.map((roleId) => deleteRole(id, roleId)));
    message.success(t("systemRoles.batchDeleteSuccess", { count: selectedRowKeys.value.length }));
    selectedRowKeys.value = [];
    void fetchData();
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("systemRoles.batchDeleteFailed"));
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
.form-wrapper {
  padding-right: 8px;
}
</style>
