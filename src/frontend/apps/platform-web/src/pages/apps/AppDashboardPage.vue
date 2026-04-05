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
        <a-button @click="go(`/apps/${appId}/pages`)">{{ t("appsDashboard.linkPages") }}</a-button>
        <a-button @click="go(`/apps/${appId}/workflows`)">{{ t("appsDashboard.linkWorkflows") }}</a-button>
        <a-button @click="go(`/apps/${appId}/data`)">{{ t("appsDashboard.linkData") }}</a-button>
        <a-button @click="go(`/apps/${appId}/flows`)">{{ t("appsDashboard.linkApprovalFlows") }}</a-button>
      </a-space>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getTenantAppInstanceDetail } from "@/services/api-console";
import type { TenantAppInstanceDetail } from "@/types/platform-console";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const isMounted = ref(false);
const appDetail = ref<TenantAppInstanceDetail | null>(null);
const appId = computed(() => String(route.params.appId ?? ""));

async function loadDetail() {
  if (!appId.value) return;
  try {
    const detail = await getTenantAppInstanceDetail(appId.value);
    if (!isMounted.value) return;
    appDetail.value = detail;
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("appsDashboard.loadFailed"));
  }
}

function go(path: string) {
  void router.push(path);
}

onMounted(() => {
  isMounted.value = true;
  void loadDetail();
});

onUnmounted(() => {
  isMounted.value = false;
});

watch(appId, () => {
  void loadDetail();
});
</script>

<style scoped>
.app-dashboard {
  padding: 8px;
}
</style>
