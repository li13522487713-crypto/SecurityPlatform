<template>
  <a-card title="部门管理" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          placeholder="搜索部门名称"
          allow-clear
          @press-enter="fetchData"
        />
        <a-button type="primary" @click="openCreate">新增部门</a-button>
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
            <a-button type="link" @click="openEdit(record)">编辑</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="formVisible"
      :title="formMode === 'create' ? '新增部门' : '编辑部门'"
      @ok="submitForm"
      @cancel="closeForm"
      destroy-on-close
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="部门名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="上级部门" name="parentId">
          <a-select
            v-model:value="formModel.parentId"
            :options="parentOptions"
            allow-clear
            placeholder="无"
          />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig, FormInstance, Rule } from "ant-design-vue";
import { message } from "ant-design-vue";
import { createDepartment, getDepartmentsAll, getDepartmentsPaged, updateDepartment } from "@/services/api";
import type { DepartmentCreateRequest, DepartmentListItem, DepartmentUpdateRequest } from "@/types/api";

type FormMode = "create" | "edit";

interface SelectOption {
  label: string;
  value: number;
}

const columns = [
  { title: "部门名称", dataIndex: "name" },
  { title: "上级部门", dataIndex: "parentId" },
  { title: "排序", dataIndex: "sortOrder" },
  { title: "操作", key: "actions" }
];

const dataSource = ref<DepartmentListItem[]>([]);
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
const formModel = reactive<DepartmentCreateRequest & DepartmentUpdateRequest>({
  name: "",
  parentId: undefined,
  sortOrder: 0
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: "请输入部门名称" }]
};

const parentOptions = ref<SelectOption[]>([]);
const selectedId = ref<string | null>(null);

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getDepartmentsPaged({
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

const fetchParents = async () => {
  try {
    const list = await getDepartmentsAll();
    parentOptions.value = list.map((item) => ({
      label: item.name,
      value: Number(item.id)
    }));
  } catch (error) {
    message.error((error as Error).message || "加载部门失败");
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const resetForm = () => {
  formModel.name = "";
  formModel.parentId = undefined;
  formModel.sortOrder = 0;
};

const openCreate = () => {
  formMode.value = "create";
  selectedId.value = null;
  resetForm();
  formVisible.value = true;
};

const openEdit = (record: DepartmentListItem) => {
  formMode.value = "edit";
  selectedId.value = record.id;
  formModel.name = record.name;
  formModel.parentId = record.parentId ?? undefined;
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
      await createDepartment({
        name: formModel.name,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder
      });
      message.success("创建成功");
    } else if (selectedId.value) {
      await updateDepartment(selectedId.value, {
        name: formModel.name,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder
      });
      message.success("更新成功");
    }
    formVisible.value = false;
    fetchData();
    fetchParents();
  } catch (error) {
    message.error((error as Error).message || "提交失败");
  }
};

onMounted(() => {
  fetchParents();
  fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>
