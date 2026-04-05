<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('projectPage.pageTitle')"
    :search-placeholder="t('projectPage.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? t('projectPage.drawerCreateTitle') : t('projectPage.drawerEditTitle')"
    :drawer-width="560"
    :submit-loading="submitting"
    :submit-disabled="submitting"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="resetFilters"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" @click="openCreate">{{ t("projectPage.addProject") }}</a-button>
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
          <template v-if="column.key === 'isActive'">
            <a-tag v-if="record.isActive" color="green">{{ t("common.statusEnabled") }}</a-tag>
            <a-tag v-else color="red">{{ t("common.statusDisabled") }}</a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
              <a-button v-if="canAssign" type="link" @click="openAssign(record)">{{ t("projectPage.assign") }}</a-button>
              <a-popconfirm
                v-if="canDelete"
                :title="t('projectPage.deleteConfirm')"
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

    <template #form>
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item :label="t('projectPage.code')" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item :label="t('projectPage.name')" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item :label="t('projectPage.status')">
          <a-switch
            v-model:checked="formModel.isActive"
            :checked-children="t('common.statusEnabled')"
            :un-checked-children="t('common.statusDisabled')"
          />
        </a-form-item>
        <a-form-item :label="t('projectPage.sortOrder')">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item :label="t('projectPage.description')">
          <a-input v-model:value="formModel.description" />
        </a-form-item>
      </a-form>
    </template>

    <template #extra-drawers>
      <a-drawer
        v-model:open="assignVisible"
        :title="t('projectPage.assignDrawerTitle')"
        placement="right"
        :width="720"
        destroy-on-close
        @close="closeAssign"
      >
        <a-tabs>
          <a-tab-pane v-if="canAssignUsers" key="users" :tab="t('projectPage.usersTab')">
            <div class="assign-toolbar">
              <a-space>
                <a-input v-model:value="userKeyword" :placeholder="t('projectPage.searchUsersPlaceholder')" allow-clear />
                <a-button @click="searchUsers">{{ t("common.search") }}</a-button>
              </a-space>
            </div>
            <a-select
              v-model:value="assignModel.userIds"
              mode="multiple"
              style="width: 100%"
              :placeholder="t('projectPage.selectUsers')"
              :options="userOptions"
              :loading="userLoading"
              :filter-option="false"
              show-search
              @search="handleUserSearch"
            />
            <div class="assign-footer">
              <a-button :disabled="!hasMoreUsers" :loading="userLoading" @click="loadMoreUsers">
                {{ t("common.loadMore") }}
              </a-button>
              <span class="assign-hint">
                {{ t("projectPage.loadedUsersSummary", { loaded: userOptions.length, total: userTotal }) }}
              </span>
            </div>
          </a-tab-pane>
          <a-tab-pane v-if="canAssignDepartments" key="departments" :tab="t('projectPage.departmentsTab')">
            <a-select
              v-model:value="assignModel.departmentIds"
              mode="multiple"
              style="width: 100%"
              :placeholder="t('projectPage.selectDepartments')"
              :options="departmentOptions"
              :loading="departmentLoading"
              show-search
              :filter-option="false"
              @search="handleDepartmentSearch"
              @focus="() => loadDepartmentOptions()"
            />
          </a-tab-pane>
          <a-tab-pane v-if="canAssignPositions" key="positions" :tab="t('projectPage.positionsTab')">
            <a-select
              v-model:value="assignModel.positionIds"
              mode="multiple"
              style="width: 100%"
              :placeholder="t('projectPage.selectPositions')"
              :options="positionOptions"
              :loading="positionLoading"
              show-search
              :filter-option="false"
              @search="handlePositionSearch"
              @focus="() => loadPositionOptions()"
            />
          </a-tab-pane>
        </a-tabs>
        <template #footer>
          <a-space>
            <a-button @click="closeAssign">{{ t("common.cancel") }}</a-button>
            <a-button type="primary" @click="submitAssign">{{ t("common.save") }}</a-button>
          </a-space>
        </template>
      </a-drawer>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useCrudPage, debounce, type SelectOption } from "@atlas/shared-core";
import { CrudPageLayout, TableViewToolbar } from "@atlas/shared-ui";
import {
  createProject,
  deleteProject,
  getProjectDetail,
  getProjectsPaged,
  updateProject,
  updateProjectDepartments,
  updateProjectPositions,
  updateProjectUsers
} from "@/services/api-system";
import { getDepartmentsPaged, getPositionsPaged, getUsersPaged } from "@/services/api-users";
import { tableViewApi } from "@/services/api-table-views";
import type {
  DepartmentListItem,
  PositionListItem,
  ProjectCreateRequest,
  ProjectDetail,
  ProjectListItem,
  ProjectUpdateRequest,
  UserListItem
} from "@atlas/shared-core";

const { t } = useI18n();
const formRef = ref<FormInstance>();

const projectColumns = computed(() => [
  { title: t("projectPage.code"), dataIndex: "code", key: "code" },
  { title: t("projectPage.name"), dataIndex: "name", key: "name" },
  { title: t("projectPage.status"), key: "isActive" },
  { title: t("projectPage.sortOrder"), dataIndex: "sortOrder", key: "sortOrder" },
  { title: t("projectPage.description"), dataIndex: "description", key: "description" },
  { title: t("projectPage.actions"), key: "actions", view: { canHide: false } }
]);

const crud = useCrudPage<ProjectListItem, ProjectDetail, ProjectCreateRequest, ProjectUpdateRequest>({
  tableKey: "system.projects",
  columns: projectColumns,
  permissions: {
    create: "projects:create",
    update: "projects:update",
    delete: "projects:delete",
    assignUsers: "projects:assign-users",
    assignDepartments: "projects:assign-departments",
    assignPositions: "projects:assign-positions"
  },
  api: {
    list: getProjectsPaged,
    detail: getProjectDetail,
    create: createProject,
    update: updateProject,
    delete: deleteProject
  },
  tableViewApi,
  formRef,
  defaultFormModel: () => ({
    code: "",
    name: "",
    isActive: true,
    description: "",
    sortOrder: 0
  }),
  formRules: {
    code: [{ required: true, message: t("projectPage.codeRequired") }],
    name: [{ required: true, message: t("projectPage.nameRequired") }]
  },
  buildCreatePayload: (model) => ({
    code: model.code,
    name: model.name,
    isActive: model.isActive,
    description: model.description || undefined,
    sortOrder: model.sortOrder
  }),
  buildUpdatePayload: (model) => ({
    name: model.name,
    isActive: model.isActive,
    description: model.description || undefined,
    sortOrder: model.sortOrder
  }),
  mapRecordToForm: (record, model) => {
    model.code = record.code;
    model.name = record.name;
    model.isActive = record.isActive;
    model.description = record.description ?? "";
    model.sortOrder = record.sortOrder;
  }
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
  handleSearch,
  resetFilters,
  openCreate,
  openEdit,
  closeForm,
  submitForm,
  handleDelete
} = crud;

const canAssignUsers = crud.hasPermissionFor("assignUsers");
const canAssignDepartments = crud.hasPermissionFor("assignDepartments");
const canAssignPositions = crud.hasPermissionFor("assignPositions");
const canAssign = canAssignUsers || canAssignDepartments || canAssignPositions;

const assignVisible = ref(false);
const assignModel = reactive<{ userIds: number[]; departmentIds: number[]; positionIds: number[] }>({
  userIds: [],
  departmentIds: [],
  positionIds: []
});

const userKeyword = ref("");
const userOptions = ref<{ label: string; value: number }[]>([]);
const userLoading = ref(false);
const userPageIndex = ref(1);
const userPageSize = ref(20);
const userTotal = ref(0);
const departmentOptions = ref<SelectOption[]>([]);
const positionOptions = ref<SelectOption[]>([]);
const departmentLoading = ref(false);
const positionLoading = ref(false);

const hasMoreUsers = computed(() => userOptions.value.length < userTotal.value);

const loadUsers = async (append: boolean) => {
  userLoading.value = true;
  try {
    const result = await getUsersPaged({
      pageIndex: userPageIndex.value,
      pageSize: userPageSize.value,
      keyword: userKeyword.value || undefined
    });
    const options = result.items.map((item: UserListItem) => ({
      label: `${item.displayName} (${item.username})`,
      value: Number(item.id)
    }));
    userTotal.value = result.total;
    userOptions.value = append ? [...userOptions.value, ...options] : options;
  } finally {
    userLoading.value = false;
  }
};

const searchUsers = async () => {
  userPageIndex.value = 1;
  await loadUsers(false);
};

const loadMoreUsers = async () => {
  if (!hasMoreUsers.value) {
    return;
  }
  userPageIndex.value += 1;
  await loadUsers(true);
};

const handleUserSearch = async (value: string) => {
  userKeyword.value = value;
  await searchUsers();
};

const loadDepartmentOptions = async (keywordValue?: string) => {
  departmentLoading.value = true;
  try {
    const result = await getDepartmentsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordValue?.trim() || undefined
    });
    departmentOptions.value = result.items.map((item: DepartmentListItem) => ({
      label: item.name,
      value: Number(item.id)
    }));
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("projectPage.loadDepartmentsFailed"));
  } finally {
    departmentLoading.value = false;
  }
};

const loadPositionOptions = async (keywordValue?: string) => {
  positionLoading.value = true;
  try {
    const result = await getPositionsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordValue?.trim() || undefined
    });
    positionOptions.value = result.items.map((item: PositionListItem) => ({
      label: `${item.name} (${item.code})`,
      value: Number(item.id)
    }));
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("projectPage.loadPositionsFailed"));
  } finally {
    positionLoading.value = false;
  }
};

const handleDepartmentSearch = debounce((value: string) => {
  void loadDepartmentOptions(value);
});

const handlePositionSearch = debounce((value: string) => {
  void loadPositionOptions(value);
});

const openAssign = async (record: ProjectListItem) => {
  crud.selectedId.value = record.id;
  assignVisible.value = true;
  await Promise.all([loadDepartmentOptions(), loadPositionOptions()]);
  await searchUsers();

  try {
    const detail = await getProjectDetail(record.id);
    assignModel.userIds = detail.userIds ?? [];
    assignModel.departmentIds = detail.departmentIds ?? [];
    assignModel.positionIds = detail.positionIds ?? [];
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("projectPage.loadAssignmentsFailed"));
  }
};

const closeAssign = () => {
  assignVisible.value = false;
};

const submitAssign = async () => {
  const id = crud.selectedId.value;
  if (!id) {
    return;
  }

  try {
    if (canAssignUsers) {
      await updateProjectUsers(id, { userIds: assignModel.userIds });
    }
    if (canAssignDepartments) {
      await updateProjectDepartments(id, { departmentIds: assignModel.departmentIds });
    }
    if (canAssignPositions) {
      await updateProjectPositions(id, { positionIds: assignModel.positionIds });
    }
    message.success(t("projectPage.assignSuccess"));
    assignVisible.value = false;
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("projectPage.assignFailed"));
  }
};
</script>

<style scoped>
.assign-toolbar {
  margin-bottom: 12px;
}

.assign-footer {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 12px;
}

.assign-hint {
  color: rgba(0, 0, 0, 0.45);
}
</style>
