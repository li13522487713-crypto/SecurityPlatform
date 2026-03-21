<template>
  <div class="resource-center-page" data-testid="e2e-resource-center-page">
    <a-page-header :title="t('console.resourceCenter.title')" :sub-title="t('console.resourceCenter.subtitle')">
      <template #extra>
        <a-button type="primary" @click="goToConsumptionPage">{{ t("console.resourceCenter.goConsumption") }}</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="[16, 16]" class="summary-row">
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic :title="t('console.resourceCenter.statCatalog')" :value="catalogTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic :title="t('console.resourceCenter.statInstances')" :value="instanceTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic :title="t('console.resourceCenter.statReleases')" :value="releaseTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic :title="t('console.resourceCenter.statExecutions')" :value="runtimeExecutionTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic :title="t('console.resourceCenter.statAudit')" :value="auditSummaryTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic :title="t('console.resourceCenter.statPlatformDs')" :value="platformDataSourceTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic :title="t('console.resourceCenter.statUnbound')" :value="unboundTenantAppTotal" />
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]" class="group-row">
      <a-col v-for="group in displayGroups" :key="group.groupKey" :xs="24" :xl="8">
        <a-card :loading="loading" :title="group.groupName">
          <template #extra>
            <a-tag color="blue">{{ t("console.resourceCenter.groupTotal", { n: group.total }) }}</a-tag>
          </template>
          <a-table
            row-key="resourceId"
            :columns="groupColumns"
            :data-source="group.items"
            :pagination="false"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'actions'">
                <a-button
                  type="link"
                  size="small"
                  :disabled="!record.navigationPath"
                  @click="jumpToResource(record)"
                >
                  {{ t("console.resourceCenter.jump") }}
                </a-button>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

import { useRoute, useRouter } from "vue-router";
import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getResourceCenterDataSourceConsumption, getResourceCenterGroups } from "@/services/api-tenant-app-instances";
import type {
  ResourceCenterDataSourceConsumptionResponse,
  ResourceCenterGroupEntry,
  ResourceCenterGroupItem
} from "@/types/platform-v2";

const router = useRouter();
const route = useRoute();
const loading = ref(false);
const resourceGroups = ref<ResourceCenterGroupItem[]>([]);
const dataSourceConsumption = ref<ResourceCenterDataSourceConsumptionResponse | null>(null);

const groupColumns = computed<TableColumnsType<ResourceCenterGroupEntry>>(() => [
  { title: t("console.resourceCenter.colName"), dataIndex: "resourceName", key: "resourceName", ellipsis: true },
  { title: t("console.resourceCenter.colType"), dataIndex: "resourceType", key: "resourceType", width: 150 },
  { title: t("console.resourceCenter.colStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("console.resourceCenter.colDescription"), dataIndex: "description", key: "description", ellipsis: true },
  { title: t("console.resourceCenter.colActions"), key: "actions", width: 90, fixed: "right" }
]);

const catalogTotal = computed(() => resourceGroups.value.find((item) => item.groupKey === "catalogs")?.total ?? 0);
const instanceTotal = computed(() => resourceGroups.value.find((item) => item.groupKey === "instances")?.total ?? 0);
const releaseTotal = computed(() => resourceGroups.value.find((item) => item.groupKey === "releases")?.total ?? 0);
const runtimeExecutionTotal = computed(
  () => resourceGroups.value.find((item) => item.groupKey === "runtime-executions")?.total ?? 0
);
const auditSummaryTotal = computed(
  () => resourceGroups.value.find((item) => item.groupKey === "audit-summary")?.total ?? 0
);
const platformDataSourceTotal = computed(() => dataSourceConsumption.value?.platformDataSourceTotal ?? 0);
const unboundTenantAppTotal = computed(() => dataSourceConsumption.value?.unboundTenantAppTotal ?? 0);
const displayGroups = computed(() => {
  const groupKey = typeof route.query.groupKey === "string" ? route.query.groupKey : "";
  if (!groupKey) {
    return resourceGroups.value;
  }

  return resourceGroups.value.filter((item) => item.groupKey === groupKey);
});

async function loadResourceCenterData() {
  loading.value = true;
  try {
    const [groups, consumption] = await Promise.all([
      getResourceCenterGroups(),
      getResourceCenterDataSourceConsumption()
    ]);

    if (!isMounted.value) return;
    resourceGroups.value = groups;
    dataSourceConsumption.value = consumption;
  } catch (error) {
    message.error((error as Error).message || t("console.resourceCenter.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function goToConsumptionPage() {
  void router.push("/console/resources/datasource-consumption");
}

function jumpToResource(entry: ResourceCenterGroupEntry) {
  if (!entry.navigationPath) {
    return;
  }

  void router.push(entry.navigationPath);
}

onMounted(() => {
  void loadResourceCenterData();
});
</script>

<style scoped>
.resource-center-page {
  padding: 24px;
}

.summary-row {
  margin-top: 8px;
}

.group-row {
  margin-top: 8px;
}
</style>
