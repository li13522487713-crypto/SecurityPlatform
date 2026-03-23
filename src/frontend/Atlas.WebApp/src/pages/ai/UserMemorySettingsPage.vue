<template>
  <a-card :title="t('ai.memory.pageTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-number
          v-model:value="agentId"
          :min="1"
          :placeholder="t('ai.memory.agentIdPlaceholder')"
          style="width: 180px"
        />
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.memory.searchPlaceholder')"
          style="width: 280px"
          @search="loadData"
        />
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        <a-popconfirm :title="t('ai.memory.clearConfirm')" @confirm="handleClear">
          <a-button danger :loading="clearing">{{ t("ai.memory.clearAll") }}</a-button>
        </a-popconfirm>
      </a-space>
    </div>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="items"
      :loading="loading"
      :pagination="false"
      :locale="{ emptyText: t('ai.memory.empty') }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'content'">
          <a-typography-paragraph :ellipsis="{ rows: 2, expandable: true, symbol: t('common.more') }">
            {{ record.content }}
          </a-typography-paragraph>
        </template>
        <template v-if="column.key === 'action'">
          <a-popconfirm :title="t('ai.memory.deleteConfirm')" @confirm="handleDelete(record.id)">
            <a-button type="link" danger>{{ t("common.delete") }}</a-button>
          </a-popconfirm>
        </template>
      </template>
    </a-table>

    <div class="pager">
      <a-pagination
        v-model:current="pageIndex"
        v-model:page-size="pageSize"
        :total="total"
        show-size-changer
        :page-size-options="['10', '20', '50']"
        @change="loadData"
      />
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import {
  clearLongTermMemories,
  deleteLongTermMemory,
  getLongTermMemoriesPaged,
  type LongTermMemoryListItem
} from "@/services/api-ai-memory";

const { t } = useI18n();

const loading = ref(false);
const clearing = ref(false);
const items = ref<LongTermMemoryListItem[]>([]);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);
const agentId = ref<number | null>(null);
const keyword = ref("");

const columns = computed(() => [
  { title: t("ai.memory.colId"), dataIndex: "id", key: "id", width: 130 },
  { title: t("ai.memory.colAgentId"), dataIndex: "agentId", key: "agentId", width: 140 },
  { title: t("ai.memory.colSource"), dataIndex: "source", key: "source", width: 120 },
  { title: t("ai.memory.colHitCount"), dataIndex: "hitCount", key: "hitCount", width: 110 },
  { title: t("ai.memory.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: t("ai.memory.colContent"), key: "content" },
  { title: t("ai.colActions"), key: "action", width: 100 }
]);

async function loadData() {
  loading.value = true;
  try {
    const result = await getLongTermMemoriesPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      agentId.value ?? undefined,
      keyword.value || undefined
    );
    items.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.memory.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  agentId.value = null;
  keyword.value = "";
  pageIndex.value = 1;
  void loadData();
}

async function handleDelete(id: number) {
  try {
    await deleteLongTermMemory(id);
    message.success(t("ai.memory.deleteSuccess"));
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.memory.deleteFailed"));
  }
}

async function handleClear() {
  clearing.value = true;
  try {
    await clearLongTermMemories(agentId.value ?? undefined);
    message.success(t("ai.memory.clearSuccess"));
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.memory.clearFailed"));
  } finally {
    clearing.value = false;
  }
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
