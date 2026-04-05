<template>
  <div class="migration-governance-page" data-testid="e2e-console-migration-governance-page">
    <a-page-header :title="t('console.migration.title')" :sub-title="t('console.migration.subtitle')">
      <template #extra>
        <a-button type="primary" :loading="loading" @click="loadOverview">{{ t("console.migration.refresh") }}</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="[16, 16]" class="summary-row">
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic :title="t('console.migration.statTotalApi')" :value="overview?.totalApiHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic :title="t('console.migration.statLegacy')" :value="overview?.legacyRouteHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic :title="t('console.migration.statRewrite')" :value="overview?.rewriteHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic :title="t('console.migration.statFallback')" :value="overview?.fallbackCount ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic :title="t('console.migration.stat404')" :value="overview?.notFoundCount ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic :title="t('console.migration.stat404Rate')" :value="notFoundRateText" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic :title="t('console.migration.statV1')" :value="overview?.v1EntryHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="8" :xl="6">
        <a-card><a-statistic :title="t('console.migration.statV2')" :value="overview?.v2EntryHits ?? 0" /></a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="8">
        <a-card>
          <a-statistic :title="t('console.migration.statCoverage')" :value="newEntryCoverageText" />
          <a-progress :percent="newEntryCoveragePercent" size="small" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="8">
        <a-card>
          <a-statistic :title="t('console.migration.statWindow')" :value="windowStartedAtText" />
        </a-card>
      </a-col>
    </a-row>

    <a-card class="detail-card" :title="t('console.migration.cardExplain')">
      <a-alert
        :type="newEntryCoveragePercent >= 80 ? 'success' : 'warning'"
        show-icon
        :message="t('console.migration.hintCoverage', { value: newEntryCoverageText })"
        :description="interpretation"
      />
      <ul class="tips-list">
        <li>{{ t("console.migration.bullet1") }}</li>
        <li>{{ t("console.migration.bullet2") }}</li>
        <li>{{ t("console.migration.bullet3") }}</li>
      </ul>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { getMigrationGovernanceOverview } from "@/services/api-migration-governance";
import type { MigrationGovernanceOverview } from "@/types/platform-console";

const { t, locale } = useI18n();
const isMounted = ref(false);
const loading = ref(false);
const overview = ref<MigrationGovernanceOverview | null>(null);

const notFoundRateText = computed(() => `${((overview.value?.notFoundRate ?? 0) * 100).toFixed(2)}%`);
const newEntryCoverageText = computed(() => `${((overview.value?.newEntryCoverageRate ?? 0) * 100).toFixed(2)}%`);
const newEntryCoveragePercent = computed(() => Number(((overview.value?.newEntryCoverageRate ?? 0) * 100).toFixed(2)));
const windowStartedAtText = computed(() => formatDateTime(overview.value?.windowStartedAt));

const interpretation = computed(() => {
  if (!overview.value) {
    return t("console.migration.summaryEmpty");
  }
  if (newEntryCoveragePercent.value >= 90 && overview.value.notFoundRate <= 0.01) {
    return t("console.migration.summaryGood");
  }
  if (overview.value.legacyRouteHits > overview.value.v2EntryHits) {
    return t("console.migration.summaryLegacy");
  }
  return t("console.migration.summaryFallback");
});

function formatDateTime(value?: string) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  const currentLocale = locale.value === "en-US" ? "en-US" : "zh-CN";
  return date.toLocaleString(currentLocale, { hour12: false });
}

async function loadOverview() {
  loading.value = true;
  try {
    const data = await getMigrationGovernanceOverview();
    if (!isMounted.value) return;
    overview.value = data;
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("console.migration.loadFailed"));
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  isMounted.value = true;
  void loadOverview();
});

onUnmounted(() => {
  isMounted.value = false;
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
