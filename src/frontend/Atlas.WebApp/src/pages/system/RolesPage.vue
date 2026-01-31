<template>
  <a-card title="角色管理" class="page-card">
    <div class="toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          placeholder="搜索角色名称/编码"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-button @click="handleSearch">查询</a-button>
        <a-button @click="resetFilters">重置</a-button>
        <a-button v-if="canCreate" type="primary" @click="openCreate">新增角色</a-button>
      </a-space>
      <a-space wrap>
        <TableViewToolbar :controller="tableViewController" />
        <a-popconfirm
          v-if="canDelete"
          title="确认批量删除所选角色？"
          ok-text="删除"
          cancel-text="取消"
          @confirm="handleBatchDelete"
        >
          <a-button danger :disabled="!selectedRowKeys.length">批量删除</a-button>
        </a-popconfirm>
      </a-space>
    </div>

    <div class="filter-bar">
      <a-space wrap>
        <span class="filter-label">高级筛选</span>
        <a-select
          v-model:value="systemFilter"
          :options="systemOptions"
          style="width: 160px"
          @change="handleSearch"
        />
      </a-space>
    </div>

    <a-table
      :columns="tableColumns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      :row-selection="rowSelection"
      :size="tableSize"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isSystem'">
          <a-tag v-if="record.isSystem" color="blue">系统</a-tag>
          <span v-else>-</span>
        </template>
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
            <a-popconfirm
              v-if="canDelete"
              title="确认删除该角色？"
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
      :title="formMode === 'create' ? '新增角色' : '编辑角色'"
      placement="right"
      width="520"
      @close="closeForm"
      destroy-on-close
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="角色名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="角色编码" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item label="描述" name="description">
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
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig, FormInstance } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useTableView } from "@/composables/useTableView";
import { createRole, deleteRole, getRolesPaged, updateRole } from "@/services/api";
import type { RoleCreateRequest, RoleListItem, RoleUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

type FormMode = "create" | "edit";

const baseColumns = [
  { title: "角色名称", dataIndex: "name", key: "name" },
  { title: "角色编码", dataIndex: "code", key: "code" },
  { title: "描述", dataIndex: "description", key: "description" },
  { title: "系统内置", dataIndex: "isSystem", key: "isSystem" },
  { title: "操作", key: "actions", view: { canHide: false } }
];

const dataSource = ref<RoleListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const systemFilter = ref<"all" | "system" | "custom">("all");
const systemOptions = [
  { label: "全部角色", value: "all" },
  { label: "系统内置", value: "system" },
  { label: "自定义", value: "custom" }
];
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const formVisible = ref(false);
const formMode = ref<FormMode>("create");
const formRef = ref<FormInstance>();
const formModel = reactive<RoleCreateRequest & RoleUpdateRequest>({
  name: "",
  code: "",
  description: ""
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: "请输入角色名称" }],
  code: [{ required: true, message: "请输入角色编码" }]
};

const selectedId = ref<string | null>(null);
const profile = getAuthProfile();
const canCreate = hasPermission(profile, "roles:create");
const canUpdate = hasPermission(profile, "roles:update");
const canDelete = hasPermission(profile, "roles:delete");
const selectedRowKeys = ref<string[]>([]);

const rowSelection = computed(() => {
  if (!canDelete) return undefined;
  return {
    selectedRowKeys: selectedRowKeys.value,
    onChange: (keys: (string | number)[]) => {
      selectedRowKeys.value = keys.map((key) => key.toString());
    }
  };
});

const fetchData = async () => {
  loading.value = true;
  try {
    const isSystem = systemFilter.value === "all"
      ? undefined
      : systemFilter.value === "system";
    const result = await getRolesPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined,
      isSystem
    });
    dataSource.value = result.items;
    pagination.total = result.total;
    selectedRowKeys.value = [];
  } catch (error) {
    message.error((error as Error).message || "查询失败");
  } finally {
    loading.value = false;
  }
};

const { controller: tableViewController, tableColumns, tableSize } = useTableView<RoleListItem>({
  tableKey: "system.roles",
  columns: baseColumns,
  pagination,
  onRefresh: fetchData
});

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const resetFilters = () => {
  keyword.value = "";
  systemFilter.value = "all";
  handleSearch();
};

const resetForm = () => {
  formModel.name = "";
  formModel.code = "";
  formModel.description = "";
};

const openCreate = () => {
  formMode.value = "create";
  selectedId.value = null;
  resetForm();
  formVisible.value = true;
};

const openEdit = (record: RoleListItem) => {
  formMode.value = "edit";
  selectedId.value = record.id;
  formModel.name = record.name;
  formModel.code = record.code;
  formModel.description = record.description ?? "";
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
      await createRole({
        name: formModel.name,
        code: formModel.code,
        description: formModel.description || undefined
      });
      message.success("创建成功");
    } else if (selectedId.value) {
      await updateRole(selectedId.value, {
        name: formModel.name,
        description: formModel.description || undefined
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
    await deleteRole(id);
    message.success("删除成功");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "删除失败");
  }
};

const handleBatchDelete = async () => {
  if (!selectedRowKeys.value.length) {
    message.warning("请先选择角色");
    return;
  }

  try {
    await Promise.all(selectedRowKeys.value.map((id) => deleteRole(id)));
    message.success(`已删除 ${selectedRowKeys.value.length} 个角色`);
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "批量删除失败");
  }
};

onMounted(fetchData);
</script>

<style scoped>
.toolbar {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.filter-bar {
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.filter-label {
  color: #8c8c8c;
}
</style>
