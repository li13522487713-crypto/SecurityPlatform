<template>
  <a-modal
    :open="open"
    :title="t('ai.promptLib.insertModalTitle')"
    width="760px"
    :ok-text="t('ai.promptLib.insertOk')"
    @ok="handleInsert"
    @cancel="emit('cancel')"
  >
    <a-space direction="vertical" style="width: 100%">
      <a-input-search
        v-model:value="keyword"
        :placeholder="t('ai.promptLib.searchPlaceholder')"
        @search="loadData"
      />
      <a-table
        row-key="id"
        :columns="columns"
        :data-source="list"
        :loading="loading"
        :pagination="false"
        :row-selection="rowSelection"
        :custom-row="customRow"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'content'">
            <a-typography-paragraph :ellipsis="{ rows: 2, expandable: true, symbol: t('ai.expand') }">
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
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { getAiPromptTemplatesPaged, type AiPromptTemplateListItem } from "@/services/api-ai-prompt";

const { t } = useI18n();

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

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("ai.promptLib.colCategory"), dataIndex: "category", key: "category", width: 120 },
  { title: t("ai.promptLib.colContent"), key: "content" }
]);

const rowSelection = computed(() => ({
  type: "radio" as const,
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: (string | number)[]) => {
    selectedRowKeys.value = keys.map((x) => Number(x));
  }
}));

function customRow(record: AiPromptTemplateListItem) {
  return {
    style: { cursor: "pointer" },
    onClick: () => {
      selectedRowKeys.value = [record.id];
    },
    onDblclick: () => {
      selectedRowKeys.value = [record.id];
      handleInsert();
    }
  };
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getAiPromptTemplatesPaged({ pageIndex: 1, pageSize: 20, keyword: keyword.value || undefined });
    list.value = result.items;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.promptLib.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleInsert() {
  if (!selectedPrompt.value) {
    message.warning(t("ai.promptLib.selectPromptFirst"));
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
