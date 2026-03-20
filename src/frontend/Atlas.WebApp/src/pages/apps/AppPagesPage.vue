<template>
  <div class="app-pages-page">
    <a-page-header :title="`页面管理 - ${appDetail?.name ?? ''}`" :sub-title="appDetail?.appKey ?? ''">
      <template #extra>
        <a-button @click="go(`/apps/${appId}/dashboard`)">返回仪表盘</a-button>
        <a-button type="primary" @click="go(`/apps/${appId}/builder`)">进入页面设计器</a-button>
      </template>
    </a-page-header>

    <a-card style="margin-top: 12px">
      <a-table
        :columns="columns"
        :data-source="pages"
        :loading="loading"
        row-key="id"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'isPublished'">
            <a-tag :color="record.isPublished ? 'green' : 'default'">
              {{ record.isPublished ? "已发布" : "草稿" }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" @click="go(`/apps/${appId}/builder`)">设计</a-button>
              <a-button
                v-if="record.pageKey"
                type="link"
                @click="go(`/apps/${appId}/run/${record.pageKey}`)"
              >
                运行态预览
              </a-button>
              <a-button
                v-if="record.pageKey && appDetail?.appKey"
                type="link"
                @click="go(`/r/${appDetail.appKey}/${record.pageKey}`)"
              >
                去正式发布页
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { getLowCodeAppDetail } from "@/services/lowcode";
import type { LowCodeAppDetail, LowCodePageListItem } from "@/types/lowcode";

const route = useRoute();
const router = useRouter();

const appId = computed(() => String(route.params.appId ?? ""));
const appDetail = ref<LowCodeAppDetail | null>(null);
const pages = ref<LowCodePageListItem[]>([]);
const loading = ref(false);

const columns = [
  { title: "页面名称", dataIndex: "name", key: "name", ellipsis: true },
  { title: "页面Key", dataIndex: "pageKey", key: "pageKey", width: 220 },
  { title: "路由", dataIndex: "routePath", key: "routePath", width: 240 },
  { title: "版本", dataIndex: "version", key: "version", width: 100 },
  { title: "发布状态", key: "isPublished", width: 120 },
  { title: "操作", key: "actions", width: 180 }
];

async function loadPages() {
  if (!appId.value) {
    appDetail.value = null;
    pages.value = [];
    return;
  }

  loading.value = true;
  try {
    const detail = await getLowCodeAppDetail(appId.value);
    appDetail.value = detail;
    pages.value = [...detail.pages].sort((a, b) => a.sortOrder - b.sortOrder);
  } catch (error) {
    message.error((error as Error).message || "加载页面列表失败");
  } finally {
    loading.value = false;
  }
}

function go(path: string) {
  router.push(path);
}

onMounted(loadPages);
watch(appId, () => {
  loadPages();
});
</script>

<style scoped>
.app-pages-page {
  padding: 8px;
}
</style>
