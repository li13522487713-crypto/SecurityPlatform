<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card title="统一搜索" :bordered="false">
      <GlobalSearchBar @search="handleSearch" />
      <template #extra>
        <a-button @click="reload">刷新</a-button>
      </template>
    </a-card>

    <a-card title="搜索结果" :bordered="false">
      <a-table row-key="key" :columns="resultColumns" :data-source="resultRows" :loading="loading" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'title'">
            <a-space direction="vertical" :size="0">
              <a-typography-link @click="goPath(record.path)">
                {{ record.title }}
              </a-typography-link>
              <span class="description-text">{{ record.description || "-" }}</span>
            </a-space>
          </template>
          <template v-if="column.key === 'type'">
            <a-tag color="blue">{{ record.resourceType }}</a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" @click="goPath(record.path)">打开</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card title="最近编辑" :bordered="false">
      <a-list :data-source="recentRows" :loading="recentLoading">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-space>
              <a-tag>{{ item.resourceType }}</a-tag>
              <a-typography-link @click="goPath(item.path)">
                {{ item.title }}
              </a-typography-link>
              <span class="description-text">{{ item.updatedAt }}</span>
            </a-space>
            <template #actions>
              <a-button type="link" danger @click="handleDeleteRecent(item.id)">删除</a-button>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </a-card>
  </a-space>
</template>

<script setup lang="ts">
import { onMounted, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import GlobalSearchBar from "@/components/ai/GlobalSearchBar.vue";
import {
  deleteAiRecentEdit,
  getAiRecentEdits,
  searchAiGlobal,
  type AiRecentEditItem,
  type AiSearchResultItem
} from "@/services/api-ai-search";

const router = useRouter();
const loading = ref(false);
const recentLoading = ref(false);
const keyword = ref("");
const resultRows = ref<AiSearchResultItem[]>([]);
const recentRows = ref<AiRecentEditItem[]>([]);

const resultColumns = [
  { title: "标题", key: "title" },
  { title: "类型", key: "type", width: 120 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: "操作", key: "action", width: 100 }
];

async function loadSearchResult() {
  loading.value = true;
  try {
    const response  = await searchAiGlobal(keyword.value.trim() || undefined, 20);

    if (!isMounted.value) return;
    resultRows.value = response.items;
    if (!keyword.value.trim()) {
      recentRows.value = response.recentEdits;
    }
  } catch (error: unknown) {
    message.error((error as Error).message || "搜索失败");
  } finally {
    loading.value = false;
  }
}

async function loadRecent() {
  recentLoading.value = true;
  try {
    recentRows.value = await getAiRecentEdits(20);

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载最近编辑失败");
  } finally {
    recentLoading.value = false;
  }
}

function handleSearch(nextKeyword: string) {
  keyword.value = nextKeyword;
  void loadSearchResult();
}

async function reload() {
  await loadSearchResult();

  if (!isMounted.value) return;
  await loadRecent();

  if (!isMounted.value) return;
}

async function handleDeleteRecent(id: number) {
  try {
    await deleteAiRecentEdit(id);

    if (!isMounted.value) return;
    message.success("删除成功");
    await loadRecent();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

async function goPath(path: string) {
  await router.push(path);

  if (!isMounted.value) return;
}

onMounted(async () => {
  await loadSearchResult();

  if (!isMounted.value) return;
  await loadRecent();

  if (!isMounted.value) return;
});
</script>

<style scoped>
.description-text {
  color: #999;
  font-size: 12px;
}
</style>
