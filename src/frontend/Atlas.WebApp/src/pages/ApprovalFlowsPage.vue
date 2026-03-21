<template>
  <CrudPageLayout title="流程定义">
    <template #toolbar-actions>
      <a-button @click="importModalOpen = true">导入 JSON</a-button>
      <a-button type="primary" @click="handleCreate">新建流程</a-button>
    </template>
    <template #table>
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
            <a-button type="link" size="small" @click="handleCopy(record.id)">复制</a-button>
            <a-button type="link" size="small" @click="handleExport(record.id)">导出</a-button>
            <a-button type="link" size="small" @click="openCompareModal(record)">对比</a-button>
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

    <a-modal
      v-model:open="importModalOpen"
      title="导入流程 JSON"
      ok-text="导入"
      cancel-text="取消"
      :confirm-loading="importLoading"
      @ok="handleImportConfirm"
    >
      <a-form layout="vertical">
        <a-form-item label="流程名称" required>
          <a-input v-model:value="importName" placeholder="请输入流程名称" />
        </a-form-item>
        <a-form-item label="定义 JSON" required>
          <a-textarea
            v-model:value="importDefinitionJson"
            :rows="10"
            placeholder='{"nodes":{"rootNode":...}}'
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="compareModalOpen"
      title="版本对比"
      ok-text="开始对比"
      cancel-text="关闭"
      :confirm-loading="compareLoading"
      @ok="handleCompareConfirm"
    >
      <a-form layout="vertical">
        <a-form-item label="目标版本号" required>
          <a-input-number
            v-model:value="compareTargetVersion"
            :min="1"
            style="width: 100%"
            placeholder="请输入版本号"
          />
        </a-form-item>
      </a-form>

      <a-alert
        v-if="compareResult"
        :type="compareResult.isSame ? 'success' : 'info'"
        :message="compareResult.summary"
        show-icon
      />
      <a-list
        v-if="compareResult && compareResult.differences.length > 0"
        size="small"
        bordered
        style="margin-top: 12px"
        :data-source="compareResult.differences"
      >
        <template #renderItem="{ item }">
          <a-list-item>
            <div>
              <div><strong>{{ item.path }}</strong></div>
              <div>当前：{{ item.sourceValue }}</div>
              <div>目标：{{ item.targetValue }}</div>
            </div>
          </a-list-item>
        </template>
      </a-list>
    </a-modal>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from "vue-router";
import {
  getApprovalFlowsPaged,
  deleteApprovalFlow,
  publishApprovalFlow,
  disableApprovalFlow,
  copyApprovalFlow,
  exportApprovalFlow,
  importApprovalFlow,
  compareApprovalFlowVersion
} from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  ApprovalFlowStatus,
  type ApprovalFlowCompareResponse,
  type ApprovalFlowDefinitionListItem
} from "@/types/api";
import { message } from "ant-design-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";

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
const importModalOpen = ref(false);
const importLoading = ref(false);
const importName = ref("");
const importDefinitionJson = ref("");
const compareModalOpen = ref(false);
const compareLoading = ref(false);
const compareFlowId = ref<string>();
const compareTargetVersion = ref<number>(1);
const compareResult = ref<ApprovalFlowCompareResponse | null>(null);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const fetchData = async () => {
  loading.value = true;
  try {
    const result  = await getApprovalFlowsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10
    });

    if (!isMounted.value) return;
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
  router.push("/approval/designer");
};

const handleDesign = (id: string) => {
  router.push(`/approval/designer/${id}`);
};

const handleCopy = async (id: string) => {
  try {
    const result  = await copyApprovalFlow(id);

    if (!isMounted.value) return;
    message.success("复制成功，已生成草稿");
    router.push(`/approval/designer/${result.id}`);
  } catch (err) {
    message.error(err instanceof Error ? err.message : "复制失败");
  }
};

const handleExport = async (id: string) => {
  try {
    const result  = await exportApprovalFlow(id);

    if (!isMounted.value) return;
    const content = JSON.stringify(result, null, 2);
    const blob = new Blob([content], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${result.name}-v${result.version}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    message.success("导出成功");
  } catch (err) {
    message.error(err instanceof Error ? err.message : "导出失败");
  }
};

const openCompareModal = (record: ApprovalFlowDefinitionListItem) => {
  compareFlowId.value = record.id;
  compareTargetVersion.value = Math.max(1, record.version - 1);
  compareResult.value = null;
  compareModalOpen.value = true;
};

const handleImportConfirm = async () => {
  if (!importName.value.trim()) {
    message.warning("请输入流程名称");
    return;
  }
  if (!importDefinitionJson.value.trim()) {
    message.warning("请输入定义 JSON");
    return;
  }

  importLoading.value = true;
  try {
    await importApprovalFlow({
      name: importName.value.trim(),
      definitionJson: importDefinitionJson.value.trim()
    });

    if (!isMounted.value) return;
    message.success("导入成功");
    importModalOpen.value = false;
    importName.value = "";
    importDefinitionJson.value = "";
    await fetchData();

    if (!isMounted.value) return;
  } catch (err) {
    message.error(err instanceof Error ? err.message : "导入失败");
  } finally {
    importLoading.value = false;
  }
};

const handleCompareConfirm = async () => {
  if (!compareFlowId.value) {
    message.warning("未选择流程");
    return;
  }
  if (!compareTargetVersion.value || compareTargetVersion.value <= 0) {
    message.warning("请输入有效目标版本号");
    return;
  }

  compareLoading.value = true;
  try {
    compareResult.value = await compareApprovalFlowVersion(compareFlowId.value, compareTargetVersion.value);

    if (!isMounted.value) return;
    message.success("对比完成");
  } catch (err) {
    compareResult.value = null;
    message.error(err instanceof Error ? err.message : "对比失败");
  } finally {
    compareLoading.value = false;
  }
};

const handlePublish = async (id: string) => {
  try {
    await publishApprovalFlow(id);

    if (!isMounted.value) return;
    message.success("发布成功");
    fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "发布失败");
  }
};

const handleDisable = async (id: string) => {
  try {
    await disableApprovalFlow(id);

    if (!isMounted.value) return;
    message.success("停用成功");
    fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "停用失败");
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteApprovalFlow(id);

    if (!isMounted.value) return;
    message.success("删除成功");
    fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : "删除失败");
  }
};

onMounted(fetchData);
</script>
