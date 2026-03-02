<template>
  <div class="form-list-page">
    <div class="page-header">
      <div class="page-header-left">
        <h2 class="page-header-title">表单管理</h2>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索表单名称"
          allow-clear
          style="width: 260px"
          @search="handleSearch"
        />
        <a-select
          v-model:value="categoryFilter"
          placeholder="全部分类"
          allow-clear
          style="width: 160px"
          @change="handleSearch"
        >
          <a-select-option value="人事类">人事类</a-select-option>
          <a-select-option value="财务类">财务类</a-select-option>
          <a-select-option value="采购类">采购类</a-select-option>
          <a-select-option value="通用">通用</a-select-option>
        </a-select>
      </div>
      <div class="page-header-right">
        <a-button type="primary" @click="handleCreate">新建表单</a-button>
      </div>
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
          <a-tag :color="statusColor(record.status)">
            {{ statusLabel(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" @click="handleEdit(record.id)">设计</a-button>
            <a-button
              v-if="record.status === 'Draft'"
              type="link"
              @click="handlePublish(record.id)"
            >发布</a-button>
            <a-button
              v-if="record.status === 'Published'"
              type="link"
              @click="handleDisable(record.id)"
            >停用</a-button>
            <a-button
              v-if="record.status === 'Disabled'"
              type="link"
              @click="handleEnable(record.id)"
            >启用</a-button>
            <a-popconfirm
              title="确认删除该表单？"
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

    <!-- 新建表单对话框 -->
    <a-modal
      v-model:open="createModalVisible"
      title="新建表单"
      ok-text="创建"
      cancel-text="取消"
      @ok="handleCreateSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="表单名称" required>
          <a-input v-model:value="createForm.name" placeholder="请输入表单名称" />
        </a-form-item>
        <a-form-item label="分类">
          <a-select v-model:value="createForm.category" placeholder="选择分类" allow-clear>
            <a-select-option value="人事类">人事类</a-select-option>
            <a-select-option value="财务类">财务类</a-select-option>
            <a-select-option value="采购类">采购类</a-select-option>
            <a-select-option value="通用">通用</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="createForm.description" :rows="3" placeholder="请输入描述" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useRouter } from "vue-router";
import type { FormDefinitionListItem } from "@/types/lowcode";
import {
  getFormDefinitionsPaged,
  createFormDefinition,
  publishFormDefinition,
  disableFormDefinition,
  enableFormDefinition,
  deleteFormDefinition
} from "@/services/lowcode";

const router = useRouter();

const keyword = ref("");
const categoryFilter = ref<string | undefined>(undefined);
const loading = ref(false);
const dataSource = ref<FormDefinitionListItem[]>([]);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total: number) => `共 ${total} 条`
});

const createModalVisible = ref(false);
const createForm = reactive({
  name: "",
  category: undefined as string | undefined,
  description: ""
});

const columns = [
  { title: "表单名称", dataIndex: "name", key: "name" },
  { title: "分类", dataIndex: "category", key: "category", width: 120 },
  { title: "版本", dataIndex: "version", key: "version", width: 80 },
  { title: "状态", key: "status", width: 100 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: "操作", key: "actions", width: 260 }
];

const statusColor = (status: string) => {
  const map: Record<string, string> = {
    Draft: "default",
    Published: "green",
    Disabled: "red",
    Archived: "gray"
  };
  return map[status] ?? "default";
};

const statusLabel = (status: string) => {
  const map: Record<string, string> = {
    Draft: "草稿",
    Published: "已发布",
    Disabled: "已停用",
    Archived: "已归档"
  };
  return map[status] ?? status;
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getFormDefinitionsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined,
      category: categoryFilter.value
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

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const handleCreate = () => {
  createForm.name = "";
  createForm.category = undefined;
  createForm.description = "";
  createModalVisible.value = true;
};

const handleCreateSubmit = async () => {
  if (!createForm.name.trim()) {
    message.warning("请输入表单名称");
    return;
  }

  try {
    const defaultSchema = JSON.stringify({
      type: "page",
      title: createForm.name,
      body: [
        {
          type: "form",
          title: "",
          body: []
        }
      ]
    });

    const result = await createFormDefinition({
      name: createForm.name,
      description: createForm.description || undefined,
      category: createForm.category,
      schemaJson: defaultSchema
    });

    createModalVisible.value = false;
    message.success("创建成功");
    router.push({ name: "apps-form-designer", params: { id: result.id } });
  } catch (error) {
    message.error((error as Error).message || "创建失败");
  }
};

const handleEdit = (id: string) => {
  router.push({ name: "apps-form-designer", params: { id } });
};

const handlePublish = async (id: string) => {
  try {
    await publishFormDefinition(id);
    message.success("发布成功");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "发布失败");
  }
};

const handleDisable = async (id: string) => {
  try {
    await disableFormDefinition(id);
    message.success("已停用");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "停用失败");
  }
};

const handleEnable = async (id: string) => {
  try {
    await enableFormDefinition(id);
    message.success("已启用");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "启用失败");
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteFormDefinition(id);
    message.success("已删除");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "删除失败");
  }
};

onMounted(() => {
  fetchData();
});
</script>

<style scoped>
.form-list-page {
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.page-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.page-header-left h2 {
  margin: 0;
  font-size: 20px;
}
</style>
