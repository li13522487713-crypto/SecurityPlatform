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
          <a-spin :spinning="statsLoading.approval" size="small">
            <a-statistic
              :title="t('dashboard.statApproval')"
              :value="approvalCount ?? '—'"
            />
          </a-spin>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-spin :spinning="statsLoading.reports" size="small">
            <a-statistic
              :title="t('dashboard.statReports')"
              :value="reportCount ?? '—'"
            />
          </a-spin>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :md="6">
        <a-card>
          <a-spin :spinning="statsLoading.dashboards" size="small">
            <a-statistic
              :title="t('dashboard.statDashboards')"
              :value="dashboardCount ?? '—'"
            />
          </a-spin>
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
import { ref, reactive, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  TeamOutlined,
  AuditOutlined,
  BarChartOutlined,
  RobotOutlined
} from "@ant-design/icons-vue";
import { getRuntimeMenu } from "@/services/api-runtime";
import {
  getApprovalPendingCount,
  getReportCount,
  getDashboardCount
} from "@/services/api-dashboard-stats";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appKey = computed(() => String(route.params.appKey ?? ""));
const runtimePageCount = ref(0);
const approvalCount = ref<number | null>(null);
const reportCount = ref<number | null>(null);
const dashboardCount = ref<number | null>(null);

const statsLoading = reactive({
  approval: false,
  reports: false,
  dashboards: false
});

function goTo(suffix: string) {
  void router.push(`/apps/${encodeURIComponent(appKey.value)}/${suffix}`);
}

async function loadStats() {
  const key = appKey.value;
  if (!key) return;

  statsLoading.approval = true;
  statsLoading.reports = true;
  statsLoading.dashboards = true;

  const results = await Promise.allSettled([
    getApprovalPendingCount(),
    getReportCount(key),
    getDashboardCount(key)
  ]);

  if (results[0].status === "fulfilled") {
    approvalCount.value = results[0].value;
  }
  statsLoading.approval = false;

  if (results[1].status === "fulfilled") {
    reportCount.value = results[1].value;
  }
  statsLoading.reports = false;

  if (results[2].status === "fulfilled") {
    dashboardCount.value = results[2].value;
  }
  statsLoading.dashboards = false;
}

onMounted(async () => {
  if (!appKey.value) return;

  try {
    const menu = await getRuntimeMenu(appKey.value);
    runtimePageCount.value = menu.items.length;
  } catch {
    runtimePageCount.value = 0;
  }

  void loadStats();
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
