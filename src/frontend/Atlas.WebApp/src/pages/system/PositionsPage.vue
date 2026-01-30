<template>
  <a-card title="职位管理" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          placeholder="搜索职位名称/编码"
          allow-clear
          @press-enter="fetchData"
        />
        <a-button type="primary" @click="openCreate">新增职位</a-button>
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
        <template v-if="column.key === 'system'">
          <a-tag :color="record.isSystem ? 'blue' : 'default'">
            {{ record.isSystem ? "系统内置" : "自定义" }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" @click="openEdit(record)">编辑</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="formVisible"
      :title="formMode === 'create' ? '新增职位' : '编辑职位'"
      @ok="submitForm"
      @cancel="closeForm"
      destroy-on-close
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="职位名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="编码" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="formModel.description" rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig, FormInstance, Rule } from "ant-design-vue";
import { message } from "ant-design-vue";
import { createRole, getRolesPaged, updateRole } from "@/services/api";
import type { RoleCreateRequest, RoleListItem, RoleUpdateRequest } from "@/types/api";

type FormMode = "create" | "edit";

const columns = [
  { title: "职位名称", dataIndex: "name" },
  { title: "编码", dataIndex: "code" },
  { title: "描述", dataIndex: "description" },
  { title: "类型", key: "system" },
  { title: "操作", key: "actions" }
];

const dataSource = ref<RoleListItem[]>([]);
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
const formModel = reactive<RoleCreateRequest & RoleUpdateRequest>({
  name: "",
  code: "",
  description: ""
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: "请输入职位名称" }],
  code: [{ required: true, message: "请输入编码" }]
};

const selectedId = ref<string | null>(null);

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getRolesPaged({
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

onMounted(fetchData);
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
