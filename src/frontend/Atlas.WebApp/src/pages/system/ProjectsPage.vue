<template>
  <CrudPageLayout
    title="项目管理"
    v-model:keyword="keyword"
    search-placeholder="搜索项目名称/编码"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? '新增项目' : '编辑项目'"
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
      <a-button v-if="canCreate" type="primary" @click="openCreate">新增项目</a-button>
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
            <a-tag v-if="record.isActive" color="green">启用</a-tag>
            <a-tag v-else color="red">停用</a-tag>
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
              <a-button v-if="canAssign" type="link" @click="openAssign(record)">分配</a-button>
              <a-popconfirm
                v-if="canDelete"
                title="确认删除该项目？"
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
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="项目编码" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item label="项目名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="状态">
          <a-switch v-model:checked="formModel.isActive" checked-children="启用" un-checked-children="停用" />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="描述">
          <a-input v-model:value="formModel.description" />
        </a-form-item>
      </a-form>
    </template>

    <template #extra-drawers>
      <a-drawer
        v-model:open="assignVisible"
        title="项目分配"
        placement="right"
        :width="720"
        @close="closeAssign"
        destroy-on-close
      >
        <a-tabs>
          <a-tab-pane v-if="canAssignUsers" key="users" tab="人员">
            <div class="assign-toolbar">
              <a-space>
                <a-input v-model:value="userKeyword" placeholder="搜索员工账号/姓名" allow-clear />
                <a-button @click="searchUsers">查询</a-button>
              </a-space>
            </div>
            <a-select
              v-model:value="assignModel.userIds"
              mode="multiple"
              style="width: 100%"
              placeholder="选择人员"
              :options="userOptions"
              :loading="userLoading"
              :filter-option="false"
              show-search
              @search="handleUserSearch"
            />
            <div class="assign-footer">
              <a-button :disabled="!hasMoreUsers" :loading="userLoading" @click="loadMoreUsers">
                加载更多
              </a-button>
              <span class="assign-hint">已加载 {{ userOptions.length }} / {{ userTotal }} 条</span>
            </div>
          </a-tab-pane>
          <a-tab-pane v-if="canAssignDepartments" key="departments" tab="部门">
            <a-select
              v-model:value="assignModel.departmentIds"
              mode="multiple"
              style="width: 100%"
              placeholder="选择部门"
              :options="departmentOptions"
              :loading="departmentLoading"
              show-search
              :filter-option="false"
              @search="handleDepartmentSearch"
              @focus="() => loadDepartmentOptions()"
            />
          </a-tab-pane>
          <a-tab-pane v-if="canAssignPositions" key="positions" tab="岗位">
            <a-select
              v-model:value="assignModel.positionIds"
              mode="multiple"
              style="width: 100%"
              placeholder="选择岗位"
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
            <a-button @click="closeAssign">取消</a-button>
            <a-button type="primary" @click="submitAssign">保存</a-button>
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
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useCrudPage } from "@/composables/useCrudPage";
import {
  createProject,
  deleteProject,
  getDepartmentsPaged,
  getPositionsPaged,
  getProjectDetail,
  getProjectsPaged,
  getUsersPaged,
  updateProject,
  updateProjectDepartments,
  updateProjectPositions,
  updateProjectUsers
} from "@/services/api";
import type {
  DepartmentListItem,
  PositionListItem,
  ProjectListItem,
  ProjectDetail,
  ProjectCreateRequest,
  ProjectUpdateRequest,
  UserListItem
} from "@/types/api";
import { debounce, type SelectOption } from "@/utils/common";

const formRef = ref<FormInstance>();

const crud = useCrudPage<ProjectListItem, ProjectDetail, ProjectCreateRequest, ProjectUpdateRequest>({
  tableKey: "system.projects",
  columns: [
    { title: "项目编码", dataIndex: "code", key: "code" },
    { title: "项目名称", dataIndex: "name", key: "name" },
    { title: "状态", key: "isActive" },
    { title: "排序", dataIndex: "sortOrder", key: "sortOrder" },
    { title: "描述", dataIndex: "description", key: "description" },
    { title: "操作", key: "actions", view: { canHide: false } }
  ],
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
  formRef,
  defaultFormModel: () => ({
    code: "",
    name: "",
    isActive: true,
    description: "",
    sortOrder: 0
  }),
  formRules: {
    code: [{ required: true, message: "请输入项目编码" }],
    name: [{ required: true, message: "请输入项目名称" }]
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
  dataSource, loading, keyword, pagination,
  formVisible, formMode, submitting, formModel, formRules,
  tableViewController, tableColumns, tableSize,
  canCreate, canUpdate, canDelete,
  onTableChange, handleSearch, resetFilters,
  openCreate, openEdit, closeForm, submitForm, handleDelete
} = crud;

const canAssignUsers = crud.hasPermissionFor("assignUsers");
const canAssignDepartments = crud.hasPermissionFor("assignDepartments");
const canAssignPositions = crud.hasPermissionFor("assignPositions");
const canAssign = canAssignUsers || canAssignDepartments || canAssignPositions;

// --- Assignment Drawer ---
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
      label: `${item.displayName}（${item.username}）`,
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
  if (!hasMoreUsers.value) return;
  userPageIndex.value += 1;
  await loadUsers(true);
};

const handleUserSearch = async (value: string) => {
  userKeyword.value = value;
  await searchUsers();
};

const loadDepartmentOptions = async (keyword?: string) => {
  departmentLoading.value = true;
  try {
    const result = await getDepartmentsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keyword?.trim() || undefined
    });
    departmentOptions.value = result.items.map((item: DepartmentListItem) => ({
      label: item.name,
      value: Number(item.id)
    }));
  } catch (error) {
    message.error((error as Error).message || "加载部门失败");
  } finally {
    departmentLoading.value = false;
  }
};

const loadPositionOptions = async (keyword?: string) => {
  positionLoading.value = true;
  try {
    const result = await getPositionsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keyword?.trim() || undefined
    });
    positionOptions.value = result.items.map((item: PositionListItem) => ({
      label: `${item.name}（${item.code}）`,
      value: Number(item.id)
    }));
  } catch (error) {
    message.error((error as Error).message || "加载岗位失败");
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
  } catch (error) {
    message.error((error as Error).message || "加载分配信息失败");
  }
};

const closeAssign = () => {
  assignVisible.value = false;
};

const submitAssign = async () => {
  const id = crud.selectedId.value;
  if (!id) return;
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
    message.success("分配成功");
    assignVisible.value = false;
  } catch (error) {
    message.error((error as Error).message || "分配失败");
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
