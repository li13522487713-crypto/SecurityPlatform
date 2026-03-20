<template>
  <div class="migration-governance-page" data-testid="e2e-console-migration-governance-page">
    <a-page-header title="迁移治理看板" sub-title="观测 legacy 命中、重写、404 与新入口覆盖率">
      <template #extra>
        <a-button type="primary" :loading="loading" @click="loadOverview">刷新</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="[16, 16]" class="summary-row">
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic title="API 总请求数" :value="overview?.totalApiHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic title="Legacy 命中数" :value="overview?.legacyRouteHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic title="重写命中数" :value="overview?.rewriteHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic title="Fallback 数" :value="overview?.fallbackCount ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic title="404 数" :value="overview?.notFoundCount ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic title="404 率" :value="notFoundRateText" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic title="v1 命中数" :value="overview?.v1EntryHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic title="v2 命中数" :value="overview?.v2EntryHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="8">
        <a-card>
          <a-statistic title="新入口覆盖率（v2）" :value="newEntryCoverageText" />
          <a-progress :percent="newEntryCoveragePercent" size="small" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="8">
        <a-card>
          <a-statistic title="窗口起始时间" :value="windowStartedAtText" />
        </a-card>
      </a-col>
    </a-row>

    <a-card class="detail-card" title="治理解读">
      <a-alert
        :type="newEntryCoveragePercent >= 80 ? 'success' : 'warning'"
        show-icon
        :message="`新入口覆盖率 ${newEntryCoverageText}`"
        :description="interpretation"
      />
      <ul class="tips-list">
        <li>优先清理高频 Legacy 入口，降低 rewrite 与 fallback 命中。</li>
        <li>当 404 率持续升高时，需回查路由映射与兼容跳转链路。</li>
        <li>建议将新入口覆盖率目标设为 ≥ 90%。</li>
      </ul>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import dayjs from "dayjs";
import { message } from "ant-design-vue";
import { getMigrationGovernanceOverview } from "@/services/api-migration-governance";
import type { MigrationGovernanceOverview } from "@/types/platform-v2";

const loading = ref(false);
const overview = ref<MigrationGovernanceOverview | null>(null);

const notFoundRateText = computed(() => `${((overview.value?.notFoundRate ?? 0) * 100).toFixed(2)}%`);
const newEntryCoverageText = computed(() => `${((overview.value?.newEntryCoverageRate ?? 0) * 100).toFixed(2)}%`);
const newEntryCoveragePercent = computed(() => Number(((overview.value?.newEntryCoverageRate ?? 0) * 100).toFixed(2)));
const windowStartedAtText = computed(() => {
  if (!overview.value?.windowStartedAt) {
    return "-";
  }
  return dayjs(overview.value.windowStartedAt).format("YYYY-MM-DD HH:mm:ss");
});

const interpretation = computed(() => {
  if (!overview.value) {
    return "暂无迁移治理指标。";
  }

  if (newEntryCoveragePercent.value >= 90 && overview.value.notFoundRate <= 0.01) {
    return "迁移治理状态良好，可继续推进 legacy 入口下线。";
  }

  if (overview.value.legacyRouteHits > overview.value.v2EntryHits) {
    return "Legacy 入口命中仍高于 v2，请优先排查高频旧入口并补齐重定向提示。";
  }

  return "建议持续跟踪 fallback 与 404 指标，分批推进入口收敛。";
});

async function loadOverview() {
  loading.value = true;
  try {
    overview.value = await getMigrationGovernanceOverview();
  } catch (error) {
    message.error((error as Error).message || "加载迁移治理指标失败");
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadOverview();
});
</script>

<style scoped>
.migration-governance-page {
  padding: 24px;
}

.summary-row {
  margin-top: 8px;
}

.detail-card {
  margin-top: 16px;
}

.tips-list {
  margin: 12px 0 0;
  padding-left: 18px;
  color: #595959;
}
</style>
