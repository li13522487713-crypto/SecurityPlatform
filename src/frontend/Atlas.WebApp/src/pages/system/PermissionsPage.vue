<template>
  <a-card title="权限管理" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          placeholder="搜索权限名称/编码"
          allow-clear
          @press-enter="fetchData"
        />
        <a-button v-if="canCreate" type="primary" @click="openCreate">新增权限</a-button>
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
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="formVisible"
      :title="formMode === 'create' ? '新增权限' : '编辑权限'"
      @ok="submitForm"
      @cancel="closeForm"
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
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig, FormInstance } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import { createPermission, getPermissionsPaged, updatePermission } from "@/services/api";
import type { PermissionCreateRequest, PermissionListItem, PermissionUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

type FormMode = "create" | "edit";

const columns = [
  { title: "权限名称", dataIndex: "name" },
  { title: "权限编码", dataIndex: "code" },
  { title: "类型", dataIndex: "type" },
  { title: "描述", dataIndex: "description" },
  { title: "操作", key: "actions" }
];

const typeOptions = [
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

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getPermissionsPaged({
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

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
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

onMounted(fetchData);
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
