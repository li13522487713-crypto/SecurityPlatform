<template>
  <div class="console-page" data-testid="e2e-console-page">
    <div class="greet-widget">
      <div class="greet-info">
        <h3>Hello, {{ profileDisplayName }}</h3>
        <p>{{ todayDate }}</p>
      </div>
    </div>

    <a-row :gutter="24" class="quick-actions" data-testid="e2e-console-quick-actions">
      <a-col :span="8">
        <a-card :bordered="false" class="widget-card" hoverable data-testid="e2e-console-card-apps" @click="go('/console/apps')">
          <a-card-meta :title="t('console.dashboard.cardAppsTitle')" :description="t('console.dashboard.cardAppsDesc')" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card
          :bordered="false"
          class="widget-card"
          hoverable
          data-testid="e2e-console-card-datasources"
          @click="go('/settings/system/datasources')"
        >
          <a-card-meta :title="t('console.dashboard.cardDsTitle')" :description="t('console.dashboard.cardDsDesc')" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card
          :bordered="false"
          class="widget-card"
          hoverable
          data-testid="e2e-console-card-system-configs"
          @click="go('/settings/system/configs')"
        >
          <a-card-meta :title="t('console.dashboard.cardSettingsTitle')" :description="t('console.dashboard.cardSettingsDesc')" />
        </a-card>
      </a-col>
    </a-row>

    <a-card :bordered="false" class="widget-card resource-group-card" :title="t('console.dashboard.resourceStatsTitle')" :loading="resourceGroupLoading">
      <a-row :gutter="[16, 16]">
        <a-col v-for="group in resourceGroups" :key="group.groupKey" :xs="24" :sm="12" :md="8">
          <a-card size="small" class="resource-group-item" hoverable @click="openResourceGroup(group.groupKey)">
            <div class="resource-group-title">{{ group.groupName }}</div>
            <div class="resource-group-total">{{ group.total }}</div>
          </a-card>
        </a-col>
      </a-row>
    </a-card>

    <a-card
      :bordered="false"
      class="widget-card datasource-consumption-card"
      :title="t('console.dashboard.dsDistributionTitle')"
      :loading="dataSourceConsumptionLoading"
    >
      <template #extra>
        <a-space wrap>
          <a-input-search
            v-model:value="dataSourceKeyword"
            :placeholder="t('console.dashboard.phSearchDs')"
            style="width: 220px"
            allow-clear
          />
          <a-select
            v-model:value="selectedAppId"
            allow-clear
            show-search
            :filter-option="false"
            :options="appFilterOptions"
            :placeholder="t('console.dashboard.phFilterApp')"
            style="width: 260px"
            @search="searchAppFilterOptions"
          />
        </a-space>
      </template>
      <a-row :gutter="[16, 16]">
        <a-col :xs="24" :md="8">
          <a-statistic :title="t('console.dashboard.statPlatformDs')" :value="dataSourceConsumption?.platformDataSourceTotal ?? 0" />
        </a-col>
        <a-col :xs="24" :md="8">
          <a-statistic :title="t('console.dashboard.statAppDs')" :value="dataSourceConsumption?.appScopedDataSourceTotal ?? 0" />
        </a-col>
        <a-col :xs="24" :md="8">
          <a-statistic :title="t('console.dashboard.statUnbound')" :value="dataSourceConsumption?.unboundTenantAppTotal ?? 0" />
        </a-col>
      </a-row>

      <a-tabs class="datasource-tabs" size="small">
        <a-tab-pane key="platform" :tab="t('console.dashboard.tabPlatformDs')">
          <a-table
            row-key="dataSourceId"
            :columns="dataSourceColumns"
            :data-source="filteredPlatformDataSources"
            :pagination="{ pageSize: 5, showSizeChanger: false }"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'lastTestedAt'">
                {{ formatLastTest(record.lastTestedAt) }}
              </template>
              <template v-if="column.key === 'actions'">
                <a-button type="link" size="small" @click="showDataSourceDetails(record)">{{ t("console.dashboard.bindingDetail") }}</a-button>
              </template>
            </template>
          </a-table>
        </a-tab-pane>
        <a-tab-pane key="appScoped" :tab="t('console.dashboard.tabAppDs')">
          <a-table
            row-key="dataSourceId"
            :columns="appScopedDataSourceColumns"
            :data-source="filteredAppScopedDataSources"
            :pagination="{ pageSize: 5, showSizeChanger: false }"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'lastTestedAt'">
                {{ formatLastTest(record.lastTestedAt) }}
              </template>
              <template v-if="column.key === 'actions'">
                <a-button type="link" size="small" @click="showDataSourceDetails(record)">{{ t("console.dashboard.bindingDetail") }}</a-button>
              </template>
            </template>
          </a-table>
        </a-tab-pane>
        <a-tab-pane key="unbound" :tab="t('console.dashboard.tabUnbound')">
          <a-table
            row-key="tenantAppInstanceId"
            :columns="unboundAppColumns"
            :data-source="filteredUnboundTenantApps"
            :pagination="{ pageSize: 5, showSizeChanger: false }"
            size="small"
          />
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <a-modal v-model:open="bindingDetailVisible" :title="t('console.dashboard.modalBindingTitle')" width="860px" :footer="null">
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item :label="t('console.dashboard.labelDsId')">{{ selectedDataSource?.dataSourceId }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.dashboard.labelName')">{{ selectedDataSource?.name }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.dashboard.labelType')">{{ selectedDataSource?.dbType }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.dashboard.labelScope')">
          {{ selectedDataSource?.scope }}{{ selectedDataSource?.scopeAppName ? ` (${selectedDataSource.scopeAppName})` : "" }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('console.dashboard.labelBoundApps')">{{ selectedDataSource?.boundTenantAppCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.dashboard.labelLastTest')">{{ formatLastTest(selectedDataSource?.lastTestedAt) }}</a-descriptions-item>
      </a-descriptions>

      <a-divider orientation="left">{{ t("console.dashboard.dividerBindings") }}</a-divider>
      <a-table
        row-key="bindingId"
        :columns="bindingRelationColumns"
        :data-source="selectedDataSource?.bindingRelations ?? []"
        :pagination="false"
        size="small"
      />
    </a-modal>

    <a-card :bordered="false" class="widget-card app-list-card" :title="t('console.dashboard.recentAppsTitle')" :loading="loading">
      <template #extra>
        <a-space>
          <a-input-search
            v-model:value="keyword"
            :placeholder="t('console.dashboard.phSearchApp')"
            style="width: 240px"
            allow-clear
            data-testid="e2e-console-app-search"
            @search="loadApps"
          />
          <a-button type="primary" data-testid="e2e-console-create-app" @click="createWizardVisible = true">
            {{ t("console.dashboard.newApp") }}
          </a-button>
        </a-space>
      </template>

      <a-empty v-if="apps.length === 0 && !loading" :description="t('console.dashboard.emptyApps')" />

      <a-row v-else :gutter="[24, 24]">
        <a-col v-for="item in apps" :key="item.id" :xs="24" :sm="12" :md="8" :lg="6">
          <a-card class="app-card" hoverable :data-testid="`e2e-console-app-card-${item.id}`" @click="openApp(item.id)">
            <div class="app-card-header">
              <div class="app-icon">{{ item.name.charAt(0) }}</div>
              <div class="app-status">
                <a-tag :color="item.status === 'Published' ? 'processing' : 'default'">
                  {{ item.status === "Published" ? t("console.dashboard.published") : item.status }}
                </a-tag>
              </div>
            </div>
            <div class="app-card-body">
              <h4 class="app-title">{{ item.name }}</h4>
              <p class="app-desc">{{ item.description || t("console.dashboard.noDesc") }}</p>
            </div>
            <div class="app-card-footer">
              <span>{{ t("console.dashboard.appKeyLabelInline", { key: item.appKey }) }}</span>
            </div>
          </a-card>
        </a-col>
      </a-row>
    </a-card>

    <AppCreateWizard v-model:open="createWizardVisible" @created="loadApps" />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { TableColumnsType } from "ant-design-vue";
import AppCreateWizard from "@/pages/console/components/AppCreateWizard.vue";
import {
  getResourceCenterDataSourceConsumption,
  getResourceCenterGroups,
  getTenantAppInstancesPaged
} from "@/services/api-tenant-app-instances";
import { useUserStore } from "@/stores/user";
import type {
  TenantAppConsumerItem,
  TenantDataSourceBindingRelationItem,
  TenantDataSourceConsumptionItem,
  ResourceCenterDataSourceConsumptionResponse,
  ResourceCenterGroupItem,
  TenantAppInstanceListItem
} from "@/types/platform-v2";

const router = useRouter();
const { t, locale } = useI18n();
const userStore = useUserStore();
const loading = ref(false);
const resourceGroupLoading = ref(false);
const dataSourceConsumptionLoading = ref(false);
const keyword = ref("");
const apps = ref<TenantAppInstanceListItem[]>([]);
const resourceGroups = ref<ResourceCenterGroupItem[]>([]);
const dataSourceConsumption = ref<ResourceCenterDataSourceConsumptionResponse | null>(null);
const createWizardVisible = ref(false);
const dataSourceKeyword = ref("");
const selectedAppId = ref<string>();
const appFilterOptions = ref<Array<{ label: string; value: string }>>([]);
const bindingDetailVisible = ref(false);
const selectedDataSource = ref<TenantDataSourceConsumptionItem | null>(null);

const dataSourceColumns = computed<TableColumnsType<TenantDataSourceConsumptionItem>>(() => [
  { title: t("console.dashboard.tableColName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("console.dashboard.tableColType"), dataIndex: "dbType", key: "dbType", width: 120 },
  { title: t("console.dashboard.tableColBoundApps"), dataIndex: "boundTenantAppCount", key: "boundTenantAppCount", width: 120 },
  { title: t("console.dashboard.tableColLastTest"), dataIndex: "lastTestedAt", key: "lastTestedAt", width: 180 },
  { title: t("console.dashboard.tableColActions"), key: "actions", width: 120 }
]);
const appScopedDataSourceColumns = computed<TableColumnsType<TenantDataSourceConsumptionItem>>(() => [
  { title: t("console.dashboard.tableColName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("console.dashboard.tableColScopeApp"), dataIndex: "scopeAppName", key: "scopeAppName", width: 180 },
  { title: t("console.dashboard.tableColBoundApps"), dataIndex: "boundTenantAppCount", key: "boundTenantAppCount", width: 120 },
  { title: t("console.dashboard.tableColLastTest"), dataIndex: "lastTestedAt", key: "lastTestedAt", width: 180 },
  { title: t("console.dashboard.tableColActions"), key: "actions", width: 120 }
]);
const unboundAppColumns = computed<TableColumnsType<TenantAppConsumerItem>>(() => [
  { title: t("console.dashboard.unboundColInstanceId"), dataIndex: "tenantAppInstanceId", key: "tenantAppInstanceId", width: 170 },
  { title: t("console.dashboard.unboundColAppKey"), dataIndex: "appKey", key: "appKey", width: 180 },
  { title: t("console.dashboard.unboundColAppName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("console.dashboard.unboundColStatus"), dataIndex: "status", key: "status", width: 120 }
]);
const bindingRelationColumns = computed<TableColumnsType<TenantDataSourceBindingRelationItem>>(() => [
  { title: t("console.dashboard.bindingColId"), dataIndex: "bindingId", key: "bindingId", width: 180 },
  { title: t("console.dashboard.bindingColTenantInstanceId"), dataIndex: "tenantAppInstanceId", key: "tenantAppInstanceId", width: 180 },
  { title: t("console.dashboard.bindingColBindingType"), dataIndex: "bindingType", key: "bindingType", width: 120 },
  { title: t("console.dashboard.bindingColSource"), dataIndex: "source", key: "source", width: 180 },
  { title: t("console.dashboard.bindingColActive"), dataIndex: "isActive", key: "isActive", width: 80 }
]);

const filteredPlatformDataSources = computed(() => {
  const keywordValue = dataSourceKeyword.value.trim().toLowerCase();
  const selectedApp = selectedAppId.value;
  const source = dataSourceConsumption.value?.platformDataSources ?? [];
  return source.filter((item) => {
    const keywordMatched = !keywordValue || item.name.toLowerCase().includes(keywordValue);
    const appMatched = !selectedApp || item.boundTenantApps.some((app) => app.tenantAppInstanceId === selectedApp);
    return keywordMatched && appMatched;
  });
});
const filteredAppScopedDataSources = computed(() => {
  const keywordValue = dataSourceKeyword.value.trim().toLowerCase();
  const selectedApp = selectedAppId.value;
  const source = dataSourceConsumption.value?.appScopedDataSources ?? [];
  return source.filter((item) => {
    const keywordMatched = !keywordValue || item.name.toLowerCase().includes(keywordValue);
    const appMatched = !selectedApp
      || item.scopeAppId === selectedApp
      || item.boundTenantApps.some((app) => app.tenantAppInstanceId === selectedApp);
    return keywordMatched && appMatched;
  });
});
const filteredUnboundTenantApps = computed(() => {
  const selectedApp = selectedAppId.value;
  const source = dataSourceConsumption.value?.unboundTenantApps ?? [];
  return selectedApp ? source.filter((item) => item.tenantAppInstanceId === selectedApp) : source;
});

const profileDisplayName = computed(() => userStore.profile?.displayName || userStore.profile?.username || "Admin");

const todayDate = computed(() =>
  new Date().toLocaleDateString(locale.value === "en-US" ? "en-US" : "zh-CN", {
    year: "numeric",
    month: "long",
    day: "numeric"
  })
);

async function loadApps() {
  loading.value = true;
  try {
    const result  = await getTenantAppInstancesPaged({
      pageIndex: 1,
      pageSize: 60,
      keyword: keyword.value || undefined
    });

    if (!isMounted.value) return;
    apps.value = result.items;
  } catch (error) {
    message.error((error as Error).message || t("console.dashboard.loadAppsFailed"));
  } finally {
    loading.value = false;
  }
}

async function loadResourceGroups() {
  resourceGroupLoading.value = true;
  try {
    resourceGroups.value = await getResourceCenterGroups();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("console.dashboard.loadResourceGroupsFailed"));
  } finally {
    resourceGroupLoading.value = false;
  }
}

async function loadDataSourceConsumption() {
  dataSourceConsumptionLoading.value = true;
  try {
    dataSourceConsumption.value = await getResourceCenterDataSourceConsumption();

    if (!isMounted.value) return;
    await searchAppFilterOptions();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("console.dashboard.loadDataSourceConsumptionFailed"));
  } finally {
    dataSourceConsumptionLoading.value = false;
  }
}

async function searchAppFilterOptions(keyword?: string) {
  try {
    const response  = await getTenantAppInstancesPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keyword || undefined
    });

    if (!isMounted.value) return;
    appFilterOptions.value = response.items.map((item) => ({
      label: t("console.dashboard.appOptionLabel", { name: item.name, appKey: item.appKey }),
      value: item.id
    }));
  } catch (error) {
    message.error((error as Error).message || t("console.dashboard.loadAppFilterFailed"));
  }
}

function formatLastTest(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString(locale.value === "en-US" ? "en-US" : "zh-CN");
}

function showDataSourceDetails(item: TenantDataSourceConsumptionItem) {
  selectedDataSource.value = item;
  bindingDetailVisible.value = true;
}

function openApp(id: string) {
  router.push(`/apps/${id}`);
}

function go(path: string) {
  router.push(path);
}

function openResourceGroup(groupKey: string) {
  router.push({
    path: "/console/resources",
    query: { groupKey }
  });
}

onMounted(() => {
  void loadApps();
  void loadResourceGroups();
  void loadDataSourceConsumption();
});
</script>

<style scoped>
.console-page {
  padding: 24px;
  max-width: 1440px;
  margin: 0 auto;
}

.greet-widget {
  margin-bottom: 24px;
}

.greet-info h3 {
  font-size: 20px;
  font-weight: 600;
  margin: 0 0 6px;
  color: var(--color-text-primary);
}

.greet-info p {
  color: var(--color-text-secondary);
  font-size: 14px;
  margin: 0;
}

.quick-actions {
  margin-bottom: 24px;
}

.widget-card {
  border-radius: var(--border-radius-lg);
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.02);
}

.app-list-card {
  min-height: 400px;
}

.resource-group-card {
  margin-bottom: 24px;
}

.datasource-consumption-card {
  margin-bottom: 24px;
}

.resource-group-item {
  border-radius: var(--border-radius-md);
}

.resource-group-title {
  font-size: 14px;
  color: var(--color-text-secondary);
}

.resource-group-total {
  font-size: 28px;
  font-weight: 600;
  line-height: 1.2;
}

.resource-group-meta {
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.app-card {
  border-radius: var(--border-radius-md);
  border: 1px solid var(--color-border);
  transition: all 0.3s;
}

.app-card:hover {
  box-shadow: var(--shadow-sm);
  border-color: transparent;
  transform: translateY(-2px);
}

.app-card :deep(.ant-card-body) {
  padding: 20px;
}

.app-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
}

.app-icon {
  width: 40px;
  height: 40px;
  background: var(--color-primary-bg);
  color: var(--color-primary);
  border-radius: var(--border-radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  font-weight: bold;
}

.app-title {
  font-size: 16px;
  font-weight: 600;
  margin: 0 0 8px;
  color: var(--color-text-primary);
}

.app-desc {
  font-size: 13px;
  color: var(--color-text-tertiary);
  margin: 0 0 16px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  height: 40px;
}

.app-card-footer {
  padding-top: 16px;
  border-top: 1px dashed var(--color-border);
  font-size: 12px;
  color: var(--color-text-tertiary);
}
</style>
