<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card title="AI Mock 集" :bordered="false">
      <a-table :columns="columns" :data-source="mockSets" row-key="id" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.enabled ? 'green' : 'default'">
              {{ record.enabled ? "启用" : "停用" }}
            </a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" @click="previewMock(record.id)">预览</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <PreviewPanel :title="previewTitle" :content="previewContent" />
  </a-space>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import PreviewPanel from "@/components/ai/PreviewPanel.vue";

interface MockSetItem {
  id: number;
  name: string;
  targetApi: string;
  enabled: boolean;
  mockResponse: string;
}

const mockSets = ref<MockSetItem[]>([
  {
    id: 1,
    name: "聊天接口延迟模拟",
    targetApi: "/api/v1/open/chat/completions",
    enabled: true,
    mockResponse: "{ \"choices\": [{ \"message\": { \"content\": \"这是 mock 返回\" } }] }"
  },
  {
    id: 2,
    name: "知识检索空结果模拟",
    targetApi: "/api/v1/open/knowledge/search",
    enabled: false,
    mockResponse: "{ \"items\": [], \"total\": 0 }"
  }
]);

const selectedId = ref<number>(1);

const columns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "目标接口", dataIndex: "targetApi", key: "targetApi" },
  { title: "状态", key: "status", width: 100 },
  { title: "操作", key: "action", width: 120 }
];

const selectedItem = computed(() => mockSets.value.find((x) => x.id === selectedId.value));
const previewTitle = computed(() => selectedItem.value?.name ?? "Mock 预览");
const previewContent = computed(() => selectedItem.value?.mockResponse ?? "暂无预览内容");

function previewMock(id: number) {
  selectedId.value = id;
}
</script>
