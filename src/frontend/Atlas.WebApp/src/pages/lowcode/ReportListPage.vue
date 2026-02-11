<template>
  <div class="report-list-page">
    <div class="page-header">
      <div class="page-header-left">
        <h2>报表管理</h2>
        <a-input-search v-model:value="keyword" placeholder="搜索报表" allow-clear style="width: 260px" @search="fetchData" />
      </div>
      <div class="page-header-right">
        <a-button type="primary" @click="handleCreate">新建报表</a-button>
      </div>
    </div>
    <a-table :columns="columns" :data-source="dataSource" :pagination="pagination" :loading="loading" row-key="id" @change="onTableChange">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="record.status === 'Published' ? 'green' : 'default'">{{ record.status === 'Published' ? '已发布' : '草稿' }}</a-tag>
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
    <a-modal v-model:open="formVisible" :title="editingId ? '编辑报表' : '新建报表'" ok-text="确定" cancel-text="取消" @ok="handleSubmit">
      <a-form layout="vertical">
        <a-form-item label="报表名称" required><a-input v-model:value="form.name" /></a-form-item>
        <a-form-item label="分类"><a-input v-model:value="form.category" /></a-form-item>
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
import type { ReportDefinitionListItem } from "@/types/lowcode";

const keyword = ref("");
const loading = ref(false);
const dataSource = ref<ReportDefinitionListItem[]>([]);
const pagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 10, total: 0, showTotal: (total: number) => `共 ${total} 条` });
const formVisible = ref(false);
const editingId = ref<string | null>(null);
const form = reactive({ name: "", category: "", description: "" });

const columns = [
  { title: "报表名称", dataIndex: "name", key: "name" },
  { title: "分类", dataIndex: "category", key: "category", width: 120 },
  { title: "版本", dataIndex: "version", key: "version", width: 80 },
  { title: "状态", key: "status", width: 100 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: "操作", key: "actions", width: 160 }
];

const fetchData = async () => {
  loading.value = true;
  try {
    const query = new URLSearchParams({ pageIndex: (pagination.current ?? 1).toString(), pageSize: (pagination.pageSize ?? 10).toString(), keyword: keyword.value });
    const resp = await requestApi<ApiResponse<PagedResult<ReportDefinitionListItem>>>(`/reports?${query}`);
    if (resp.data) { dataSource.value = resp.data.items; pagination.total = resp.data.total; }
  } catch (e) { message.error((e as Error).message); } finally { loading.value = false; }
};

const onTableChange = (pager: TablePaginationConfig) => { pagination.current = pager.current; pagination.pageSize = pager.pageSize; fetchData(); };
const handleCreate = () => { editingId.value = null; form.name = ""; form.category = ""; form.description = ""; formVisible.value = true; };
const handleEdit = (record: ReportDefinitionListItem) => { editingId.value = record.id; form.name = record.name; form.category = record.category ?? ""; form.description = record.description ?? ""; formVisible.value = true; };

const handleSubmit = async () => {
  if (!form.name.trim()) { message.warning("请输入名称"); return; }
  try {
    if (editingId.value) {
      await requestApi(`/reports/${editingId.value}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: form.name, category: form.category || undefined, description: form.description || undefined, configJson: "{}" }) });
    } else {
      await requestApi("/reports", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: form.name, category: form.category || undefined, description: form.description || undefined, configJson: "{}" }) });
    }
    formVisible.value = false; message.success("操作成功"); fetchData();
  } catch (e) { message.error((e as Error).message); }
};

const handleDelete = async (id: string) => {
  try { await requestApi(`/reports/${id}`, { method: "DELETE" }); message.success("已删除"); fetchData(); } catch (e) { message.error((e as Error).message); }
};

onMounted(fetchData);
</script>

<style scoped>
.report-list-page { padding: 24px; }
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.page-header-left { display: flex; align-items: center; gap: 12px; }
.page-header-left h2 { margin: 0; font-size: 20px; }
</style>
