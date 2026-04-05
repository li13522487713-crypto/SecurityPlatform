<template>
  <div class="app-dashboard">
    <a-page-header :title="t('dashboard.pageTitle')" :sub-title="appKey" />

    <a-row :gutter="[16, 16]" class="dashboard-stats">
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-statistic :title="t('dashboard.statRuntime')" :value="runtimePageCount" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-statistic :title="t('dashboard.statApproval')" value="—" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-statistic :title="t('dashboard.statReports')" value="—" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-statistic :title="t('dashboard.statAI')" value="—" />
        </a-card>
      </a-col>
    </a-row>

    <a-card :title="t('dashboard.quickActions')" class="dashboard-actions">
      <a-space wrap>
        <a-button @click="goTo('org')">
          <TeamOutlined />
          {{ t("workspace.menuOrg") }}
        </a-button>
        <a-button @click="goTo('approval')">
          <AuditOutlined />
          {{ t("workspace.menuApproval") }}
        </a-button>
        <a-button @click="goTo('reports')">
          <BarChartOutlined />
          {{ t("workspace.menuReports") }}
        </a-button>
        <a-button @click="goTo('ai/chat')">
          <RobotOutlined />
          {{ t("workspace.menuAIChat") }}
        </a-button>
      </a-space>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  TeamOutlined,
  AuditOutlined,
  BarChartOutlined,
  RobotOutlined
} from "@ant-design/icons-vue";
import { getRuntimeMenu } from "@/services/api-runtime";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appKey = computed(() => String(route.params.appKey ?? ""));
const runtimePageCount = ref(0);

function goTo(suffix: string) {
  void router.push(`/apps/${encodeURIComponent(appKey.value)}/${suffix}`);
}

onMounted(async () => {
  if (!appKey.value) return;
  try {
    const menu = await getRuntimeMenu(appKey.value);
    runtimePageCount.value = menu.items.length;
  } catch {
    runtimePageCount.value = 0;
  }
});
</script>

<style scoped>
.app-dashboard {
  max-width: 1200px;
}

.dashboard-stats {
  margin-bottom: 16px;
}

.dashboard-actions {
  margin-bottom: 16px;
}
</style>
