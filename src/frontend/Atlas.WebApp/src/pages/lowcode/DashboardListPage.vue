<template>
  <div class="dashboard-list-page">
    <div class="page-header">
      <div class="page-header-left">
        <h2>仪表盘管理</h2>
        <a-input-search v-model:value="keyword" placeholder="搜索仪表盘" allow-clear style="width: 260px" @search="fetchData" />
      </div>
      <div class="page-header-right">
        <a-button type="primary" @click="handleCreate">新建仪表盘</a-button>
      </div>
    </div>
    <a-table :columns="columns" :data-source="dataSource" :pagination="pagination" :loading="loading" row-key="id" @change="onTableChange">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isPublic'">
          <a-tag :color="record.isPublic ? 'blue' : 'default'">{{ record.isPublic ? '公开' : '私有' }}</a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" @click="handleEdit(record)">编辑</a-button>
            <a-popconfirm title="确认删除？" @confirm="handleDelete(record.id)">
              <a-button type="link" danger>删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>
    <a-modal v-model:open="formVisible" :title="editingId ? '编辑仪表盘' : '新建仪表盘'" ok-text="确定" cancel-text="取消" @ok="handleSubmit">
      <a-form layout="vertical">
        <a-form-item label="仪表盘名称" required><a-input v-model:value="form.name" /></a-form-item>
        <a-form-item label="是否公开"><a-switch v-model:checked="form.isPublic" /></a-form-item>
        <a-form-item label="描述"><a-textarea v-model:value="form.description" :rows="3" /></a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { requestApi } from "@/services/api";
import type { ApiResponse, PagedResult } from "@/types/api";
import type { DashboardDefinitionListItem } from "@/types/lowcode";

const keyword = ref("");
const loading = ref(false);
const dataSource = ref<DashboardDefinitionListItem[]>([]);
const pagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 10, total: 0, showTotal: (total: number) => `共 ${total} 条` });
const formVisible = ref(false);
const editingId = ref<string | null>(null);
const form = reactive({ name: "", description: "", isPublic: false });

const columns = [
  { title: "仪表盘名称", dataIndex: "name", key: "name" },
  { title: "公开", key: "isPublic", width: 100 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: "操作", key: "actions", width: 160 }
];

const fetchData = async () => {
  loading.value = true;
  try {
    const query = new URLSearchParams({ pageIndex: (pagination.current ?? 1).toString(), pageSize: (pagination.pageSize ?? 10).toString(), keyword: keyword.value });
    const resp = await requestApi<ApiResponse<PagedResult<DashboardDefinitionListItem>>>(`/dashboards?${query}`);
    if (resp.data) { dataSource.value = resp.data.items; pagination.total = resp.data.total; }
  } catch (e) { message.error((e as Error).message); } finally { loading.value = false; }
};

const onTableChange = (pager: TablePaginationConfig) => { pagination.current = pager.current; pagination.pageSize = pager.pageSize; fetchData(); };
const handleCreate = () => { editingId.value = null; form.name = ""; form.description = ""; form.isPublic = false; formVisible.value = true; };
const handleEdit = (record: DashboardDefinitionListItem) => { editingId.value = record.id; form.name = record.name; form.description = record.description ?? ""; form.isPublic = record.isPublic; formVisible.value = true; };

const handleSubmit = async () => {
  if (!form.name.trim()) { message.warning("请输入名称"); return; }
  try {
    if (editingId.value) {
      await requestApi(`/dashboards/${editingId.value}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: form.name, description: form.description || undefined, isPublic: form.isPublic, layoutJson: "[]" }) });
    } else {
      await requestApi("/dashboards", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: form.name, description: form.description || undefined, isPublic: form.isPublic, layoutJson: "[]" }) });
    }
    formVisible.value = false; message.success("操作成功"); fetchData();
  } catch (e) { message.error((e as Error).message); }
};

const handleDelete = async (id: string) => {
  try { await requestApi(`/dashboards/${id}`, { method: "DELETE" }); message.success("已删除"); fetchData(); } catch (e) { message.error((e as Error).message); }
};

onMounted(fetchData);
</script>

<style scoped>
.dashboard-list-page { padding: 24px; }
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.page-header-left { display: flex; align-items: center; gap: 12px; }
.page-header-left h2 { margin: 0; font-size: 20px; }
</style>
