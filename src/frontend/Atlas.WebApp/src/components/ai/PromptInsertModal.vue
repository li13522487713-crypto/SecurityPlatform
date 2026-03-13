<template>
  <a-modal
    :open="open"
    title="插入 Prompt 模板"
    width="760px"
    ok-text="插入"
    @ok="handleInsert"
    @cancel="emit('cancel')"
  >
    <a-space direction="vertical" style="width: 100%">
      <a-input-search
        v-model:value="keyword"
        placeholder="搜索 Prompt 名称"
        @search="loadData"
      />
      <a-table
        row-key="id"
        :columns="columns"
        :data-source="list"
        :loading="loading"
        :pagination="false"
        :row-selection="rowSelection"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'content'">
            <a-typography-paragraph :ellipsis="{ rows: 2, expandable: true, symbol: '展开' }">
              {{ record.content }}
            </a-typography-paragraph>
          </template>
        </template>
      </a-table>
    </a-space>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { message } from "ant-design-vue";
import { getAiPromptTemplatesPaged, type AiPromptTemplateListItem } from "@/services/api-ai-prompt";

const props = defineProps<{
  open: boolean;
}>();

const emit = defineEmits<{
  (e: "insert", content: string): void;
  (e: "cancel"): void;
}>();

const keyword = ref("");
const list = ref<AiPromptTemplateListItem[]>([]);
const loading = ref(false);
const selectedRowKeys = ref<number[]>([]);
const selectedPrompt = computed(() => list.value.find((x) => x.id === selectedRowKeys.value[0]));

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 220 },
  { title: "分类", dataIndex: "category", key: "category", width: 120 },
  { title: "内容", key: "content" }
];

const rowSelection = computed(() => ({
  type: "radio" as const,
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: (string | number)[]) => {
    selectedRowKeys.value = keys.map((x) => Number(x));
  }
}));

async function loadData() {
  loading.value = true;
  try {
    const result = await getAiPromptTemplatesPaged({ pageIndex: 1, pageSize: 20, keyword: keyword.value || undefined });
    list.value = result.items;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载 Prompt 模板失败");
  } finally {
    loading.value = false;
  }
}

function handleInsert() {
  if (!selectedPrompt.value) {
    message.warning("请先选择一个 Prompt 模板");
    return;
  }

  emit("insert", selectedPrompt.value.content);
}

watch(
  () => props.open,
  (value) => {
    if (value) {
      selectedRowKeys.value = [];
      void loadData();
    }
  }
);
</script>
