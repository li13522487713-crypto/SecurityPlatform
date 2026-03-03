<template>
  <a-card title="租户数据源管理" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-button type="primary" :disabled="!canManage" @click="openCreate">新增数据源</a-button>
        <a-button :loading="loading" @click="loadData">刷新</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="items"
      :loading="loading"
      row-key="id"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isActive'">
          <a-tag :color="record.isActive ? 'green' : 'default'">{{ record.isActive ? "启用" : "停用" }}</a-tag>
        </template>
        <template v-else-if="column.key === 'updatedAt'">
          {{ record.updatedAt ? formatDateTime(record.updatedAt) : "-" }}
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" :disabled="!canManage" @click="openEdit(record)">编辑</a-button>
            <a-popconfirm
              title="确认删除该数据源？"
              ok-text="删除"
              cancel-text="取消"
              @confirm="handleDelete(record.id)"
            >
              <a-button type="link" danger :disabled="!canManage">删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>
  </a-card>

  <a-drawer
    v-model:open="formVisible"
    :title="formMode === 'create' ? '新增数据源' : '编辑数据源'"
    placement="right"
    width="560"
    :destroy-on-close="true"
    @close="closeForm"
  >
    <a-alert
      v-if="formMode === 'edit'"
      type="info"
      show-icon
      message="出于安全要求，编辑数据源时需要重新输入连接字符串。"
      style="margin-bottom: 16px"
    />
    <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
      <a-form-item label="租户ID" name="tenantIdValue">
        <a-input v-model:value="formModel.tenantIdValue" :disabled="formMode === 'edit'" />
      </a-form-item>
      <a-form-item label="数据源名称" name="name">
        <a-input v-model:value="formModel.name" />
      </a-form-item>
      <a-form-item label="数据库类型" name="dbType">
        <a-select v-model:value="formModel.dbType" :options="dbTypeOptions" />
      </a-form-item>
      <a-form-item label="连接字符串" name="connectionString">
        <a-textarea
          v-model:value="formModel.connectionString"
          :rows="4"
          placeholder="请输入完整连接字符串"
        />
      </a-form-item>
    </a-form>
    <template #footer>
      <a-space>
        <a-button :loading="testing" @click="handleTestConnection">测试连接</a-button>
        <a-button @click="closeForm">取消</a-button>
        <a-button type="primary" :loading="saving" :disabled="!canManage" @click="submitForm">保存</a-button>
      </a-space>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { FormInstance, Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import {
  createTenantDataSource,
  deleteTenantDataSource,
  getTenantDataSources,
  testTenantDataSourceConnection,
  updateTenantDataSource
} from "@/services/api";
import type {
  TenantDataSourceDto,
  TenantDataSourceCreateRequest,
  TenantDataSourceUpdateRequest
} from "@/types/api";
import { getAuthProfile, getTenantId, hasPermission } from "@/utils/auth";

const profile = getAuthProfile();
const canManage = computed(() => hasPermission(profile, "system:admin"));

const loading = ref(false);
const saving = ref(false);
const testing = ref(false);
const items = ref<TenantDataSourceDto[]>([]);

const formVisible = ref(false);
const formMode = ref<"create" | "edit">("create");
const editingId = ref<string>("");
const formRef = ref<FormInstance>();

const formModel = reactive({
  tenantIdValue: getTenantId() ?? "",
  name: "",
  dbType: "SQLite",
  connectionString: ""
});

const dbTypeOptions = [
  { label: "SQLite", value: "SQLite" },
  { label: "SqlServer", value: "SqlServer" },
  { label: "MySql", value: "MySql" },
  { label: "PostgreSql", value: "PostgreSql" }
];

const formRules: Record<string, Rule[]> = {
  tenantIdValue: [{ required: true, message: "请输入租户ID" }],
  name: [{ required: true, message: "请输入数据源名称" }],
  dbType: [{ required: true, message: "请选择数据库类型" }],
  connectionString: [{ required: true, message: "请输入连接字符串" }]
};

const columns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "租户ID", dataIndex: "tenantIdValue", key: "tenantIdValue" },
  { title: "数据库类型", dataIndex: "dbType", key: "dbType" },
  { title: "状态", key: "isActive" },
  { title: "更新时间", key: "updatedAt" },
  { title: "操作", key: "actions" }
];

const loadData = async () => {
  loading.value = true;
  try {
    items.value = await getTenantDataSources();
  } catch (error) {
    message.error(error instanceof Error ? error.message : "加载数据源失败");
  } finally {
    loading.value = false;
  }
};

const resetFormModel = () => {
  formModel.tenantIdValue = getTenantId() ?? "";
  formModel.name = "";
  formModel.dbType = "SQLite";
  formModel.connectionString = "";
};

const openCreate = () => {
  formMode.value = "create";
  editingId.value = "";
  resetFormModel();
  formVisible.value = true;
};

const openEdit = (record: TenantDataSourceDto) => {
  formMode.value = "edit";
  editingId.value = record.id;
  formModel.tenantIdValue = record.tenantIdValue;
  formModel.name = record.name;
  formModel.dbType = record.dbType;
  formModel.connectionString = "";
  formVisible.value = true;
};

const closeForm = () => {
  formVisible.value = false;
};

const handleTestConnection = async () => {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;

  testing.value = true;
  try {
    const result = await testTenantDataSourceConnection({
      connectionString: formModel.connectionString,
      dbType: formModel.dbType
    });
    if (result.success) {
      message.success("连接测试成功");
      return;
    }
    message.error(result.errorMessage || "连接测试失败");
  } catch (error) {
    message.error(error instanceof Error ? error.message : "连接测试失败");
  } finally {
    testing.value = false;
  }
};

const submitForm = async () => {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;

  saving.value = true;
  try {
    if (formMode.value === "create") {
      const payload: TenantDataSourceCreateRequest = {
        tenantIdValue: formModel.tenantIdValue,
        name: formModel.name,
        dbType: formModel.dbType,
        connectionString: formModel.connectionString
      };
      await createTenantDataSource(payload);
      message.success("数据源创建成功");
    } else {
      const payload: TenantDataSourceUpdateRequest = {
        name: formModel.name,
        dbType: formModel.dbType,
        connectionString: formModel.connectionString
      };
      await updateTenantDataSource(editingId.value, payload);
      message.success("数据源更新成功");
    }
    formVisible.value = false;
    await loadData();
  } catch (error) {
    message.error(error instanceof Error ? error.message : "保存数据源失败");
  } finally {
    saving.value = false;
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteTenantDataSource(id);
    message.success("数据源已删除");
    await loadData();
  } catch (error) {
    message.error(error instanceof Error ? error.message : "删除失败");
  }
};

const formatDateTime = (value: string) => {
  return new Date(value).toLocaleString("zh-CN");
};

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
}
</style>
