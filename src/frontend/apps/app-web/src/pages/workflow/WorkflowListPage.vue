<template>
  <a-card :bordered="false">
    <template #title>工作流管理</template>
    <template #extra>
      <a-button type="primary" @click="createWorkflow">新建工作流</a-button>
    </template>

    <a-table
      row-key="id"
      :data-source="items"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'name'">
          <a-button type="link" @click="openEditor(record.id)">{{ record.name }}</a-button>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" @click="openEditor(record.id)">编辑</a-button>
            <a-button type="link" danger @click="removeWorkflow(record.id)">删除</a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message, Modal } from "ant-design-vue";
import { workflowV2Api } from "@/services/api-workflow-v2";
import type { WorkflowListItem } from "@/types/workflow-v2";

const route = useRoute();
const router = useRouter();

const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);
const items = ref<WorkflowListItem[]>([]);

const columns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "模式", dataIndex: "mode", key: "mode" },
  { title: "版本", dataIndex: "latestVersionNumber", key: "version" },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt" },
  { title: "操作", key: "actions", width: 160 }
];

const pagination = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true
}));

async function load() {
  loading.value = true;
  try {
    const res = await workflowV2Api.list(pageIndex.value, pageSize.value);
    if (res.success && res.data) {
      items.value = res.data.items;
      total.value = res.data.total;
    }
  } catch (error) {
    items.value = [];
    total.value = 0;
    message.error(error instanceof Error ? error.message : "工作流加载失败");
  } finally {
    loading.value = false;
  }
}

function openEditor(id: string) {
  router.push({
    name: "app-workflow-editor",
    params: { appKey: String(route.params.appKey), id }
  });
}

async function createWorkflow() {
  const res = await workflowV2Api.create({
    name: `工作流_${Date.now().toString().slice(-6)}`,
    mode: 0
  });
  if (res.success && res.data?.id) {
    message.success("创建成功");
    openEditor(res.data.id);
  }
}

async function removeWorkflow(id: string) {
  Modal.confirm({
    title: "确认删除",
    content: "删除后不可恢复，是否继续？",
    async onOk() {
      const res = await workflowV2Api.delete(id);
      if (res.success) {
        message.success("删除成功");
        await load();
      }
    }
  });
}

function handleTableChange(pag: { current?: number; pageSize?: number }) {
  pageIndex.value = pag.current ?? 1;
  pageSize.value = pag.pageSize ?? 20;
  void load();
}

onMounted(() => {
  void load();
});
</script>
