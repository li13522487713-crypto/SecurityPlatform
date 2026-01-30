<template>
  <a-card title="员工管理" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          placeholder="搜索用户名/姓名/邮箱"
          allow-clear
          @press-enter="fetchData"
        />
        <a-button v-if="canCreate" type="primary" @click="openCreate">新增员工</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="record.isActive ? 'green' : 'red'">
            {{ record.isActive ? "启用" : "停用" }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canUpdate" type="link" @click="openEdit(record.id)">编辑</a-button>
            <a-button v-if="canAssignRoles" type="link" @click="openRoles(record.id)">角色</a-button>
            <a-button v-if="canAssignDepartments" type="link" @click="openDepartments(record.id)">部门</a-button>
            <a-button v-if="canAssignPositions" type="link" @click="openPositions(record.id)">职位</a-button>
            <a-popconfirm
              v-if="canDelete"
              title="确认删除该员工？"
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

    <a-modal
      v-model:open="formVisible"
      :title="formMode === 'create' ? '新增员工' : '编辑员工'"
      @ok="submitForm"
      @cancel="closeForm"
      destroy-on-close
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item v-if="formMode === 'create'" label="用户名" name="username">
          <a-input v-model:value="formModel.username" />
        </a-form-item>
        <a-form-item v-if="formMode === 'create'" label="密码" name="password">
          <a-input-password v-model:value="formModel.password" />
        </a-form-item>
        <a-form-item label="姓名" name="displayName">
          <a-input v-model:value="formModel.displayName" />
        </a-form-item>
        <a-form-item label="邮箱" name="email">
          <a-input v-model:value="formModel.email" />
        </a-form-item>
        <a-form-item label="手机号" name="phoneNumber">
          <a-input v-model:value="formModel.phoneNumber" />
        </a-form-item>
        <a-form-item label="状态" name="isActive">
          <a-switch v-model:checked="formModel.isActive" />
        </a-form-item>
        <a-form-item v-if="formMode === 'create' && canAssignRoles" label="角色" name="roleIds">
          <a-select
            v-model:value="formModel.roleIds"
            mode="multiple"
            placeholder="选择角色"
            :options="roleOptions"
          />
        </a-form-item>
        <a-form-item v-if="formMode === 'create' && canAssignDepartments" label="部门" name="departmentIds">
          <a-select
            v-model:value="formModel.departmentIds"
            mode="multiple"
            placeholder="选择部门"
            :options="departmentOptions"
          />
        </a-form-item>
        <a-form-item v-if="formMode === 'create' && canAssignPositions" label="职位" name="positionIds">
          <a-select
            v-model:value="formModel.positionIds"
            mode="multiple"
            placeholder="选择职位"
            :options="positionOptions"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal v-model:open="rolesVisible" title="设置角色" @ok="submitRoles" @cancel="closeRoles" destroy-on-close>
      <a-form layout="vertical">
        <a-form-item label="角色">
          <a-select v-model:value="rolesModel.roleIds" mode="multiple" :options="roleOptions" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal v-model:open="departmentsVisible" title="设置部门" @ok="submitDepartments" @cancel="closeDepartments" destroy-on-close>
      <a-form layout="vertical">
        <a-form-item label="部门">
          <a-select v-model:value="departmentsModel.departmentIds" mode="multiple" :options="departmentOptions" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal v-model:open="positionsVisible" title="设置职位" @ok="submitPositions" @cancel="closePositions" destroy-on-close>
      <a-form layout="vertical">
        <a-form-item label="职位">
          <a-select v-model:value="positionsModel.positionIds" mode="multiple" :options="positionOptions" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig, FormInstance, Rule } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  createUser,
  deleteUser,
  getDepartmentsAll,
  getPositionsAll,
  getRolesPaged,
  getUserDetail,
  getUsersPaged,
  updateUser,
  updateUserDepartments,
  updateUserPositions,
  updateUserRoles
} from "@/services/api";
import type {
  DepartmentListItem,
  RoleListItem,
  UserDetail,
  UserListItem,
  UserCreateRequest,
  UserUpdateRequest,
  PositionListItem
} from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

type FormMode = "create" | "edit";

interface SelectOption {
  label: string;
  value: number;
}

const columns = [
  { title: "用户名", dataIndex: "username" },
  { title: "姓名", dataIndex: "displayName" },
  { title: "邮箱", dataIndex: "email" },
  { title: "手机号", dataIndex: "phoneNumber" },
  { title: "状态", dataIndex: "isActive", key: "status" },
  { title: "最近登录", dataIndex: "lastLoginAt" },
  { title: "操作", key: "actions" }
];

const dataSource = ref<UserListItem[]>([]);
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
const formModel = reactive<UserCreateRequest & UserUpdateRequest>({
  username: "",
  password: "",
  displayName: "",
  email: "",
  phoneNumber: "",
  isActive: true,
  roleIds: [],
  departmentIds: [],
  positionIds: []
});

const formRules: Record<string, Rule[]> = {
  username: [{ required: true, message: "请输入用户名" }],
  password: [{ required: true, message: "请输入密码" }],
  displayName: [{ required: true, message: "请输入姓名" }]
};

const rolesVisible = ref(false);
const departmentsVisible = ref(false);
const positionsVisible = ref(false);
const selectedUserId = ref<string | null>(null);
const rolesModel = reactive({ roleIds: [] as number[] });
const departmentsModel = reactive({ departmentIds: [] as number[] });
const positionsModel = reactive({ positionIds: [] as number[] });

const roleOptions = ref<SelectOption[]>([]);
const departmentOptions = ref<SelectOption[]>([]);
const positionOptions = ref<SelectOption[]>([]);
const profile = getAuthProfile();
const canCreate = hasPermission(profile, "users:create");
const canUpdate = hasPermission(profile, "users:update");
const canDelete = hasPermission(profile, "users:delete");
const canAssignRoles = hasPermission(profile, "users:assign-roles");
const canAssignDepartments = hasPermission(profile, "users:assign-departments");
const canAssignPositions = hasPermission(profile, "users:assign-positions");

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getUsersPaged({
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

const fetchSelectOptions = async () => {
  try {
    const [rolesResult, departmentsResult, positionsResult] = await Promise.all([
      getRolesPaged({ pageIndex: 1, pageSize: 200 }),
      getDepartmentsAll(),
      getPositionsAll()
    ]);
    roleOptions.value = rolesResult.items.map((role: RoleListItem) => ({
      label: `${role.name} (${role.code})`,
      value: Number(role.id)
    }));
    departmentOptions.value = departmentsResult.map((dept: DepartmentListItem) => ({
      label: dept.name,
      value: Number(dept.id)
    }));
    positionOptions.value = positionsResult.map((position: PositionListItem) => ({
      label: `${position.name} (${position.code})`,
      value: Number(position.id)
    }));
  } catch (error) {
    message.error((error as Error).message || "加载选项失败");
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const resetForm = () => {
  formModel.username = "";
  formModel.password = "";
  formModel.displayName = "";
  formModel.email = "";
  formModel.phoneNumber = "";
  formModel.isActive = true;
  formModel.roleIds = [];
  formModel.departmentIds = [];
  formModel.positionIds = [];
};

const openCreate = () => {
  formMode.value = "create";
  resetForm();
  formVisible.value = true;
};

const openEdit = async (id: string) => {
  formMode.value = "edit";
  resetForm();
  try {
    const detail: UserDetail = await getUserDetail(id);
    selectedUserId.value = detail.id;
    formModel.displayName = detail.displayName;
    formModel.email = detail.email ?? "";
    formModel.phoneNumber = detail.phoneNumber ?? "";
    formModel.isActive = detail.isActive;
    formVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || "加载详情失败");
  }
};

const closeForm = () => {
  formVisible.value = false;
};

const submitForm = async () => {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;

  try {
    if (formMode.value === "create") {
      await createUser({
        username: formModel.username,
        password: formModel.password,
        displayName: formModel.displayName,
        email: formModel.email || undefined,
        phoneNumber: formModel.phoneNumber || undefined,
        isActive: formModel.isActive,
        roleIds: formModel.roleIds,
        departmentIds: formModel.departmentIds,
        positionIds: formModel.positionIds
      });
      message.success("创建成功");
    } else if (selectedUserId.value) {
      await updateUser(selectedUserId.value, {
        displayName: formModel.displayName,
        email: formModel.email || undefined,
        phoneNumber: formModel.phoneNumber || undefined,
        isActive: formModel.isActive
      });
      message.success("更新成功");
    }
    formVisible.value = false;
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "提交失败");
  }
};

const openRoles = async (id: string) => {
  try {
    const detail = await getUserDetail(id);
    selectedUserId.value = detail.id;
    rolesModel.roleIds = detail.roleIds.slice();
    rolesVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || "加载角色失败");
  }
};

const closeRoles = () => {
  rolesVisible.value = false;
};

const submitRoles = async () => {
  if (!selectedUserId.value) return;
  try {
    await updateUserRoles(selectedUserId.value, { roleIds: rolesModel.roleIds });
    message.success("角色已更新");
    rolesVisible.value = false;
  } catch (error) {
    message.error((error as Error).message || "更新角色失败");
  }
};

const openDepartments = async (id: string) => {
  try {
    const detail = await getUserDetail(id);
    selectedUserId.value = detail.id;
    departmentsModel.departmentIds = detail.departmentIds.slice();
    departmentsVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || "加载部门失败");
  }
};

const closeDepartments = () => {
  departmentsVisible.value = false;
};

const submitDepartments = async () => {
  if (!selectedUserId.value) return;
  try {
    await updateUserDepartments(selectedUserId.value, { departmentIds: departmentsModel.departmentIds });
    message.success("部门已更新");
    departmentsVisible.value = false;
  } catch (error) {
    message.error((error as Error).message || "更新部门失败");
  }
};

const openPositions = async (id: string) => {
  try {
    const detail = await getUserDetail(id);
    selectedUserId.value = detail.id;
    positionsModel.positionIds = detail.positionIds.slice();
    positionsVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || "加载职位失败");
  }
};

const closePositions = () => {
  positionsVisible.value = false;
};

const submitPositions = async () => {
  if (!selectedUserId.value) return;
  try {
    await updateUserPositions(selectedUserId.value, { positionIds: positionsModel.positionIds });
    message.success("职位已更新");
    positionsVisible.value = false;
  } catch (error) {
    message.error((error as Error).message || "更新职位失败");
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteUser(id);
    message.success("删除成功");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "删除失败");
  }
};

onMounted(() => {
  fetchSelectOptions();
  fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
