<template>
  <div class="app-dashboard">
    <a-page-header :title="appDetail?.name || t('appsDashboard.titleFallback')" :sub-title="appDetail?.appKey || ''">
      <template #extra>
        <a-button @click="go('/console')">{{ t("appsDashboard.backConsole") }}</a-button>
        <a-button type="primary" @click="go(`/apps/${appId}/builder`)">{{ t("appsDashboard.openDesigner") }}</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="16" style="margin-top: 12px">
      <a-col :span="8">
        <a-card>
          <a-statistic :title="t('appsDashboard.statPages')" :value="appDetail?.pageCount ?? 0" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card>
          <a-statistic :title="t('appsDashboard.statVersion')" :value="appDetail?.version ?? 0" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card>
          <a-statistic :title="t('appsDashboard.statStatus')" :value="appDetail?.status ?? '-'" />
        </a-card>
      </a-col>
    </a-row>

    <a-card :title="t('appsDashboard.shortcuts')" style="margin-top: 16px">
      <a-space wrap>
        <a-button @click="go(`/apps/${appId}/builder`)">{{ t("appsDashboard.linkPageDesigner") }}</a-button>
        <a-button @click="go(`/apps/${appId}/settings`)">{{ t("appsDashboard.linkSettings") }}</a-button>
        <a-button @click="go(`/apps/${appId}/agents`)">{{ t("appsDashboard.linkAgents") }}</a-button>
        <a-button @click="go(`/apps/${appId}/workflows`)">{{ t("appsDashboard.linkWorkflows") }}</a-button>
        <a-button @click="go(`/apps/${appId}/prompts`)">{{ t("appsDashboard.linkPrompts") }}</a-button>
        <a-button @click="go(`/apps/${appId}/plugins`)">{{ t("appsDashboard.linkPlugins") }}</a-button>
        <a-button @click="go(`/apps/${appId}/users`)">{{ t("appsDashboard.linkMembers") }}</a-button>
      </a-space>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { TenantAppInstanceDetail } from "@/types/platform-v2";
import { getTenantAppInstanceDetail } from "@/services/api-tenant-app-instances";
import { rememberAppMeta } from "@/utils/app-meta-cache";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const appDetail = ref<TenantAppInstanceDetail | null>(null);
const appId = computed(() => String(route.params.appId ?? ""));

async function loadDetail() {
  if (!appId.value) {
    return;
  }
  try {
    appDetail.value = await getTenantAppInstanceDetail(appId.value);
    if (appDetail.value) {
      rememberAppMeta([{
        id: appDetail.value.id,
        name: appDetail.value.name,
        appKey: appDetail.value.appKey
      }]);
    }

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("appsDashboard.loadFailed"));
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
