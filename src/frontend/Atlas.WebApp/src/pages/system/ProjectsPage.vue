<template>
  <a-card title="项目管理" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          placeholder="搜索项目名称/编码"
          allow-clear
          @press-enter="fetchData"
        />
        <a-button v-if="canCreate" type="primary" @click="openCreate">新增项目</a-button>
      </a-space>
      <TableViewToolbar :controller="tableViewController" />
    </div>

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

    <a-drawer
      v-model:open="formVisible"
      :title="formMode === 'create' ? '新增项目' : '编辑项目'"
      placement="right"
      width="560"
      @close="closeForm"
      destroy-on-close
    >
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
      <template #footer>
        <a-space>
          <a-button @click="closeForm">取消</a-button>
          <a-button type="primary" @click="submitForm">保存</a-button>
        </a-space>
      </template>
    </a-drawer>

    <a-drawer
      v-model:open="assignVisible"
      title="项目分配"
      placement="right"
      width="720"
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
            @focus="loadDepartmentOptions"
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
            @focus="loadPositionOptions"
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
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useTableView } from "@/composables/useTableView";
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
  ProjectCreateRequest,
  ProjectListItem,
  ProjectUpdateRequest,
  UserListItem
} from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

type FormMode = "create" | "edit";

const baseColumns = [
  { title: "项目编码", dataIndex: "code", key: "code" },
  { title: "项目名称", dataIndex: "name", key: "name" },
  { title: "状态", key: "isActive" },
  { title: "排序", dataIndex: "sortOrder", key: "sortOrder" },
  { title: "描述", dataIndex: "description", key: "description" },
  { title: "操作", key: "actions", view: { canHide: false } }
];

const dataSource = ref<ProjectListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const formVisible = ref(false);
const formMode = ref<FormMode>("create");
const formRef = ref<FormInstance>();
const formModel = reactive<ProjectCreateRequest & ProjectUpdateRequest>({
  code: "",
  name: "",
  isActive: true,
  description: "",
  sortOrder: 0
});

const formRules: Record<string, Rule[]> = {
  code: [{ required: true, message: "请输入项目编码" }],
  name: [{ required: true, message: "请输入项目名称" }]
};

const selectedId = ref<string | null>(null);
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
const departmentOptions = ref<{ label: string; value: number }[]>([]);
const positionOptions = ref<{ label: string; value: number }[]>([]);
const departmentLoading = ref(false);
const positionLoading = ref(false);

const profile = getAuthProfile();
const canCreate = hasPermission(profile, "projects:create");
const canUpdate = hasPermission(profile, "projects:update");
const canDelete = hasPermission(profile, "projects:delete");
const canAssignUsers = hasPermission(profile, "projects:assign-users");
const canAssignDepartments = hasPermission(profile, "projects:assign-departments");
const canAssignPositions = hasPermission(profile, "projects:assign-positions");
const canAssign = canAssignUsers || canAssignDepartments || canAssignPositions;

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getProjectsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || "查询失败");
  } finally {
    loading.value = false;
  }
};

const { controller: tableViewController, tableColumns, tableSize } = useTableView<ProjectListItem>({
  tableKey: "system.projects",
  columns: baseColumns,
  pagination,
  onRefresh: fetchData
});

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const resetForm = () => {
  formModel.code = "";
  formModel.name = "";
  formModel.isActive = true;
  formModel.description = "";
  formModel.sortOrder = 0;
};

const openCreate = () => {
  formMode.value = "create";
  selectedId.value = null;
  resetForm();
  formVisible.value = true;
};

const openEdit = (record: ProjectListItem) => {
  formMode.value = "edit";
  selectedId.value = record.id;
  formModel.code = record.code;
  formModel.name = record.name;
  formModel.isActive = record.isActive;
  formModel.description = record.description ?? "";
  formModel.sortOrder = record.sortOrder;
  formVisible.value = true;
};

const closeForm = () => {
  formVisible.value = false;
};

const submitForm = async () => {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;

  try {
    if (formMode.value === "create") {
      await createProject({
        code: formModel.code,
        name: formModel.name,
        isActive: formModel.isActive,
        description: formModel.description || undefined,
        sortOrder: formModel.sortOrder
      });
      message.success("创建成功");
    } else if (selectedId.value) {
      await updateProject(selectedId.value, {
        name: formModel.name,
        isActive: formModel.isActive,
        description: formModel.description || undefined,
        sortOrder: formModel.sortOrder
      });
      message.success("更新成功");
    }
    formVisible.value = false;
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "提交失败");
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteProject(id);
    message.success("删除成功");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "删除失败");
  }
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

const debounce = (handler: (value: string) => void, delay = 300) => {
  let timer: number | undefined;
  return (value: string) => {
    if (timer) window.clearTimeout(timer);
    timer = window.setTimeout(() => handler(value), delay);
  };
};

const handleDepartmentSearch = debounce((value: string) => {
  void loadDepartmentOptions(value);
});

const handlePositionSearch = debounce((value: string) => {
  void loadPositionOptions(value);
});

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

const openAssign = async (record: ProjectListItem) => {
  selectedId.value = record.id;
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
  if (!selectedId.value) return;
  try {
    if (canAssignUsers) {
      await updateProjectUsers(selectedId.value, { userIds: assignModel.userIds });
    }
    if (canAssignDepartments) {
      await updateProjectDepartments(selectedId.value, { departmentIds: assignModel.departmentIds });
    }
    if (canAssignPositions) {
      await updateProjectPositions(selectedId.value, { positionIds: assignModel.positionIds });
    }
    message.success("分配成功");
    assignVisible.value = false;
  } catch (error) {
    message.error((error as Error).message || "分配失败");
  }
};

onMounted(fetchData);
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

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
