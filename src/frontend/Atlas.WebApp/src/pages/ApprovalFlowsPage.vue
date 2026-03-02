<template>
  <a-card title="流程定义" class="page-card">
    <template #extra>
      <a-button type="primary" @click="handleCreate">新建流程</a-button>
    </template>
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
          <a-tag :color="getStatusColor(record.status)">
            {{ getStatusText(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" size="small" @click="handleDesign(record.id)">设计</a-button>
            <a-button
              v-if="record.status === 0"
              type="link"
              size="small"
              @click="handlePublish(record.id)"
            >
              发布
            </a-button>
            <a-button
              v-if="record.status === 1"
              type="link"
              size="small"
              danger
              @click="handleDisable(record.id)"
            >
              停用
            </a-button>
            <a-popconfirm title="确定删除吗？" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { getApprovalFlowsPaged, deleteApprovalFlow, publishApprovalFlow, disableApprovalFlow } from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";
import { ApprovalFlowStatus, type ApprovalFlowDefinitionListItem } from "@/types/api";
import { message } from "ant-design-vue";

const router = useRouter();

const columns = [
  { title: "流程名称", dataIndex: "name", key: "name" },
  { title: "版本", dataIndex: "version", key: "version" },
  { title: "状态", key: "status" },
  { title: "发布时间", dataIndex: "publishedAt", key: "publishedAt" },
  { title: "操作", key: "action", width: 200 }
];

const dataSource = ref<ApprovalFlowDefinitionListItem[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getApprovalFlowsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (err) {
    message.error(err instanceof Error ? err.message : "查询失败");
  } finally {
    loading.value = false;
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const getStatusColor = (status: ApprovalFlowStatus) => {
  switch (status) {
    case ApprovalFlowStatus.Draft:
      return "default";
    case ApprovalFlowStatus.Published:
      return "green";
    case ApprovalFlowStatus.Disabled:
      return "red";
    default:
      return "default";
  }
};

const getStatusText = (status: ApprovalFlowStatus) => {
  switch (status) {
    case ApprovalFlowStatus.Draft:
      return "草稿";
    case ApprovalFlowStatus.Published:
      return "已发布";
    case ApprovalFlowStatus.Disabled:
      return "已停用";
    default:
      return "未知";
  }
};

const handleCreate = () => {
  router.push("/process/designer");
};

const handleDesign = (id: string) => {
  router.push(`/process/designer/${id}`);
};

const handlePublish = async (id: string) => {
  try {
    await publishApprovalFlow(id);
    message.success("发布成功");
    fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "发布失败");
  }
};

const handleDisable = async (id: string) => {
  try {
    await disableApprovalFlow(id);
    message.success("停用成功");
    fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "停用失败");
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteApprovalFlow(id);
    message.success("删除成功");
    fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "删除失败");
  }
};

onMounted(fetchData);
</script>
