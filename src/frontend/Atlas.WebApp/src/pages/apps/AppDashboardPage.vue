<template>
  <div class="app-dashboard">
    <a-page-header :title="appDetail?.name || '应用仪表盘'" :sub-title="appDetail?.appKey || ''">
      <template #extra>
        <a-button @click="go('/console')">返回控制台</a-button>
        <a-button type="primary" @click="go(`/apps/${appId}/builder`)">进入设计器</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="16" style="margin-top: 12px">
      <a-col :span="8">
        <a-card>
          <a-statistic title="页面数量" :value="appDetail?.pages?.length ?? 0" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card>
          <a-statistic title="版本号" :value="appDetail?.version ?? 0" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card>
          <a-statistic title="状态" :value="appDetail?.status ?? '-'" />
        </a-card>
      </a-col>
    </a-row>

    <a-card title="快捷入口" style="margin-top: 16px">
      <a-space wrap>
        <a-button @click="go(`/apps/${appId}/builder`)">页面设计器</a-button>
        <a-button @click="go(`/apps/${appId}/settings`)">应用设置</a-button>
        <a-button @click="go(`/apps/${appId}/agents`)">Agent 管理</a-button>
        <a-button @click="go(`/apps/${appId}/workflows`)">Workflow 管理</a-button>
        <a-button @click="go(`/apps/${appId}/prompts`)">Prompt 模板</a-button>
        <a-button @click="go(`/apps/${appId}/plugins`)">插件配置</a-button>
        <a-button @click="go(`/apps/${appId}/users`)">应用成员</a-button>
      </a-space>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { LowCodeAppDetail } from "@/types/lowcode";
import { getLowCodeAppDetail } from "@/services/lowcode";

const route = useRoute();
const router = useRouter();
const appDetail = ref<LowCodeAppDetail | null>(null);
const appId = computed(() => String(route.params.appId ?? ""));

async function loadDetail() {
  if (!appId.value) {
    return;
  }
  try {
    appDetail.value = await getLowCodeAppDetail(appId.value);

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || "加载应用详情失败");
  }
}

function go(path: string) {
  router.push(path);
}

onMounted(loadDetail);
watch(appId, () => {
  loadDetail();
});
</script>

<style scoped>
.app-dashboard {
  padding: 8px;
}
</style>
