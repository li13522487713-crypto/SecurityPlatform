<template>
  <a-card title="权限管理" class="page-card">
    <div class="toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          placeholder="搜索权限名称/编码"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-button @click="handleSearch">查询</a-button>
        <a-button @click="resetFilters">重置</a-button>
        <a-button v-if="canCreate" type="primary" @click="openCreate">新增权限</a-button>
      </a-space>
      <a-space wrap>
        <TableViewToolbar :controller="tableViewController" />
        <a-button :disabled="!selectedRowKeys.length" @click="handleBatchCopy">批量复制编码</a-button>
      </a-space>
    </div>

    <div class="filter-bar">
      <a-space wrap>
        <span class="filter-label">高级筛选</span>
        <a-select
          v-model:value="typeFilter"
          :options="typeFilterOptions"
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
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="formVisible"
      :title="formMode === 'create' ? '新增权限' : '编辑权限'"
      placement="right"
      width="520"
      @close="closeForm"
      destroy-on-close
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="权限名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="权限编码" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item label="类型" name="type">
          <a-select v-model:value="formModel.type" :options="typeOptions" />
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
import { createPermission, getPermissionsPaged, updatePermission } from "@/services/api";
import type { PermissionCreateRequest, PermissionListItem, PermissionUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

type FormMode = "create" | "edit";

const baseColumns = [
  { title: "权限名称", dataIndex: "name", key: "name" },
  { title: "权限编码", dataIndex: "code", key: "code" },
  { title: "类型", dataIndex: "type", key: "type" },
  { title: "描述", dataIndex: "description", key: "description" },
  { title: "操作", key: "actions", view: { canHide: false } }
];

const typeOptions = [
  { label: "Api", value: "Api" },
  { label: "Menu", value: "Menu" }
];
const typeFilter = ref<"all" | "Api" | "Menu">("all");
const typeFilterOptions = [
  { label: "全部类型", value: "all" },
  { label: "Api", value: "Api" },
  { label: "Menu", value: "Menu" }
];

const dataSource = ref<PermissionListItem[]>([]);
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
const formModel = reactive<PermissionCreateRequest & PermissionUpdateRequest>({
  name: "",
  code: "",
  type: "Api",
  description: ""
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: "请输入权限名称" }],
  code: [{ required: true, message: "请输入权限编码" }],
  type: [{ required: true, message: "请选择类型" }]
};

const selectedId = ref<string | null>(null);
const profile = getAuthProfile();
const canCreate = hasPermission(profile, "permissions:create");
const canUpdate = hasPermission(profile, "permissions:update");
const selectedRowKeys = ref<string[]>([]);
const selectedRows = ref<PermissionListItem[]>([]);

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: (string | number)[], rows: PermissionListItem[]) => {
    selectedRowKeys.value = keys.map((key) => key.toString());
    selectedRows.value = rows;
  }
}));

const fetchData = async () => {
  loading.value = true;
  try {
    const type = typeFilter.value === "all" ? undefined : typeFilter.value;
    const result = await getPermissionsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined,
      type
    });
    dataSource.value = result.items;
    pagination.total = result.total;
    selectedRowKeys.value = [];
    selectedRows.value = [];
  } catch (error) {
    message.error((error as Error).message || "查询失败");
  } finally {
    loading.value = false;
  }
};

const { controller: tableViewController, tableColumns, tableSize } = useTableView<PermissionListItem>({
  tableKey: "system.permissions",
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
  typeFilter.value = "all";
  handleSearch();
};

const resetForm = () => {
  formModel.name = "";
  formModel.code = "";
  formModel.type = "Api";
  formModel.description = "";
};

const openCreate = () => {
  formMode.value = "create";
  selectedId.value = null;
  resetForm();
  formVisible.value = true;
};

const openEdit = (record: PermissionListItem) => {
  formMode.value = "edit";
  selectedId.value = record.id;
  formModel.name = record.name;
  formModel.code = record.code;
  formModel.type = record.type;
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
      await createPermission({
        name: formModel.name,
        code: formModel.code,
        type: formModel.type,
        description: formModel.description || undefined
      });
      message.success("创建成功");
    } else if (selectedId.value) {
      await updatePermission(selectedId.value, {
        name: formModel.name,
        type: formModel.type,
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

const handleBatchCopy = async () => {
  if (!selectedRows.value.length) {
    message.warning("请先选择权限");
    return;
  }
  const content = selectedRows.value.map((item) => item.code).join("\n");
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(content);
      message.success("已复制权限编码");
      return;
    }
    throw new Error("Clipboard API not available");
  } catch {
    message.warning("浏览器不支持自动复制，请手动复制");
    message.info(content);
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
