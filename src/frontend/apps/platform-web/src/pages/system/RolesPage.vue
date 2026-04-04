<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('systemRoles.pageTitle')"
    :search-placeholder="t('systemRoles.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? t('systemRoles.drawerCreateTitle') : t('systemRoles.drawerEditTitle')"
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
      <a-button v-if="canCreate" type="primary" @click="openCreate">
        <template #icon><PlusOutlined /></template>
        {{ t("systemRoles.addRole") }}
      </a-button>
    </template>
    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
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
            :data-source="dataSource"
            :pagination="pagination"
            :loading="loading"
            :row-selection="rowSelection"
            :size="tableSize"
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
                  <a-button
                    v-if="canAssignPermissions || canAssignMenus || canManageDataScope"
                    type="link"
                    @click="openAssign(record)"
                  >
                    {{ t("systemRoles.assignPermissions") }}
                  </a-button>
                  <a-popconfirm
                    v-if="canDelete"
                    :title="t('systemRoles.deleteConfirm')"
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
        </template>
        <template #detail>
          <RoleAssignPanel
            v-if="selectedItem"
            :role-id="selectedItem.id"
            :role-code="selectedItem.code"
            :role-name="selectedItem.name"
            :can-assign-permissions="canAssignPermissions"
            :can-assign-menus="canAssignMenus"
            :can-manage-data-scope="canManageDataScope"
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
import { computed, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { CrudPageLayout, TableViewToolbar } from "@atlas/shared-ui";
import MasterDetailLayout from "@/components/layout/MasterDetailLayout.vue";
import RoleAssignPanel from "@/components/system/roles/RoleAssignPanel.vue";
import { useCrudPage } from "@atlas/shared-core";
import { useMasterDetail } from "@/composables/useMasterDetail";
import { createRole, deleteRole, getRoleDetail, getRolesPaged, updateRole } from "@/services/api-users";
import { tableViewApi } from "@/services/api-table-views";
import { PlusOutlined } from "@ant-design/icons-vue";
import type {
  RoleListItem,
  RoleDetail,
  RoleCreateRequest,
  RoleUpdateRequest,
  RoleQueryRequest
} from "@atlas/shared-core";

const { t } = useI18n();

const systemFilter = ref<"all" | "system" | "custom">("all");
const systemOptions = computed(() => [
  { label: t("systemRoles.filterAll"), value: "all" },
  { label: t("systemRoles.filterSystem"), value: "system" },
  { label: t("systemRoles.filterCustom"), value: "custom" }
]);

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
const tableColumnsDef = computed(() => [
  { title: t("systemRoles.colRoleName"), dataIndex: "name", key: "name" },
  { title: t("systemRoles.colRoleCode"), dataIndex: "code", key: "code" },
  { title: t("systemRoles.colDescription"), dataIndex: "description", key: "description" },
  { title: t("systemRoles.colSystem"), dataIndex: "isSystem", key: "isSystem" },
  { title: t("systemRoles.colActions"), key: "actions", view: { canHide: false } }
]);

const crud = useCrudPage<RoleListItem, RoleDetail, RoleCreateRequest, RoleUpdateRequest, RoleQueryRequest>({
  tableKey: "system.roles",
  columns: tableColumnsDef,
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
    name: [{ required: true, message: t("systemRoles.nameRequired") }],
    code: [{ required: true, message: t("systemRoles.codeRequired") }]
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
  },
  tableViewApi,
  translate: (key, params) => t(key, (params ?? {}) as Record<string, unknown>)
});

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
  openCreate,
  openEdit,
  closeForm,
  submitForm,
  handleDelete
} = crud;

const canAssignPermissions = crud.hasPermissionFor("assignPermissions");
const canAssignMenus = crud.hasPermissionFor("assignMenus");
const canManageDataScope = canUpdate;

const { selectedItem, isDetailVisible, selectItem } = useMasterDetail<RoleListItem>();

const customRow = (record: RoleListItem) => {
  return {
    onClick: () => {
      void selectItem(record);
    },
    style: {
      cursor: "pointer",
      backgroundColor:
        selectedItem.value && selectedItem.value.id === record.id ? "var(--color-primary-bg)" : undefined
    }
  };
};

const openAssign = (record: RoleListItem) => {
  void selectItem(record);
};

const handleAssignSuccess = () => {
  crud.fetchData();
};

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
    message.warning(t("systemRoles.selectRoleWarning"));
    return;
  }
  try {
    await Promise.all(selectedRowKeys.value.map((id) => deleteRole(id)));
    message.success(t("systemRoles.batchDeleteSuccess", { count: selectedRowKeys.value.length }));
    selectedRowKeys.value = [];
    crud.fetchData();
  } catch (error) {
    message.error((error as Error).message || t("systemRoles.batchDeleteFailed"));
  }
};
</script>

<style scoped>
.form-wrapper {
  padding-right: 8px;
}
</style>
