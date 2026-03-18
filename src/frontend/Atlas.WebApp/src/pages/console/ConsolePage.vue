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
          <a-card-meta title="Apps" description="Open application management" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card
          :bordered="false"
          class="widget-card"
          hoverable
          data-testid="e2e-console-card-datasources"
          @click="go('/console/datasources')"
        >
          <a-card-meta title="Datasources" description="Manage tenant datasources" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card
          :bordered="false"
          class="widget-card"
          hoverable
          data-testid="e2e-console-card-system-configs"
          @click="go('/console/settings/system/configs')"
        >
          <a-card-meta title="System settings" description="Open system configuration" />
        </a-card>
      </a-col>
    </a-row>

    <a-card :bordered="false" class="widget-card resource-group-card" title="Resource center groups" :loading="resourceGroupLoading">
      <a-row :gutter="[16, 16]">
        <a-col v-for="group in resourceGroups" :key="group.groupKey" :xs="24" :sm="12" :md="8">
          <a-card size="small" class="resource-group-item">
            <div class="resource-group-title">{{ group.groupName }}</div>
            <div class="resource-group-total">{{ group.total }}</div>
            <div class="resource-group-meta">{{ group.groupKey }}</div>
          </a-card>
        </a-col>
      </a-row>
    </a-card>

    <a-card
      :bordered="false"
      class="widget-card datasource-consumption-card"
      title="Datasource consumption"
      :loading="dataSourceConsumptionLoading"
    >
      <a-row :gutter="[16, 16]">
        <a-col :xs="24" :md="8">
          <a-statistic title="Platform datasources" :value="dataSourceConsumption?.platformDataSourceTotal ?? 0" />
        </a-col>
        <a-col :xs="24" :md="8">
          <a-statistic title="App scoped datasources" :value="dataSourceConsumption?.appScopedDataSourceTotal ?? 0" />
        </a-col>
        <a-col :xs="24" :md="8">
          <a-statistic title="Unbound apps" :value="dataSourceConsumption?.unboundTenantAppTotal ?? 0" />
        </a-col>
      </a-row>

      <a-row :gutter="[16, 16]" style="margin-top: 16px">
        <a-col :xs="24" :lg="12">
          <a-card size="small" title="Platform datasource bindings">
            <a-list
              v-if="(dataSourceConsumption?.platformDataSources.length ?? 0) > 0"
              :data-source="dataSourceConsumption?.platformDataSources.slice(0, 5) ?? []"
              size="small"
            >
              <template #renderItem="{ item }">
                <a-list-item>
                  <a-list-item-meta :title="`${item.name} (${item.dbType})`">
                    <template #description>
                      Bound apps: {{ item.boundTenantAppCount }} · Last test: {{ formatLastTest(item.lastTestedAt) }}
                    </template>
                  </a-list-item-meta>
                </a-list-item>
              </template>
            </a-list>
            <a-empty v-else description="No platform datasources" />
          </a-card>
        </a-col>
        <a-col :xs="24" :lg="12">
          <a-card size="small" title="App scoped datasource bindings">
            <a-list
              v-if="(dataSourceConsumption?.appScopedDataSources.length ?? 0) > 0"
              :data-source="dataSourceConsumption?.appScopedDataSources.slice(0, 5) ?? []"
              size="small"
            >
              <template #renderItem="{ item }">
                <a-list-item>
                  <a-list-item-meta :title="item.name">
                    <template #description>
                      Scope app: {{ item.scopeAppName || item.scopeAppId || "-" }} · Bound apps: {{ item.boundTenantAppCount }}
                    </template>
                  </a-list-item-meta>
                </a-list-item>
              </template>
            </a-list>
            <a-empty v-else description="No app scoped datasources" />
          </a-card>
        </a-col>
      </a-row>
    </a-card>

    <a-card :bordered="false" class="widget-card app-list-card" title="Recent apps" :loading="loading">
      <template #extra>
        <a-space>
          <a-input-search
            v-model:value="keyword"
            placeholder="Search apps"
            style="width: 240px"
            allow-clear
            data-testid="e2e-console-app-search"
            @search="loadApps"
          />
          <a-button type="primary" data-testid="e2e-console-create-app" @click="createWizardVisible = true">
            Create app
          </a-button>
        </a-space>
      </template>

      <a-empty v-if="apps.length === 0 && !loading" description="No apps" />

      <a-row v-else :gutter="[24, 24]">
        <a-col v-for="item in apps" :key="item.id" :xs="24" :sm="12" :md="8" :lg="6">
          <a-card class="app-card" hoverable :data-testid="`e2e-console-app-card-${item.id}`" @click="openApp(item.id)">
            <div class="app-card-header">
              <div class="app-icon">{{ item.name.charAt(0) }}</div>
              <div class="app-status">
                <a-tag :color="item.status === 'Published' ? 'processing' : 'default'">
                  {{ item.status === "Published" ? "Published" : item.status }}
                </a-tag>
              </div>
            </div>
            <div class="app-card-body">
              <h4 class="app-title">{{ item.name }}</h4>
              <p class="app-desc">{{ item.description || "No description" }}</p>
            </div>
            <div class="app-card-footer">
              <span>App key: {{ item.appKey }}</span>
            </div>
          </a-card>
        </a-col>
      </a-row>
    </a-card>

    <AppCreateWizard v-model:open="createWizardVisible" @created="loadApps" />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import AppCreateWizard from "@/pages/console/components/AppCreateWizard.vue";
import {
  getResourceCenterDataSourceConsumption,
  getResourceCenterGroups,
  getTenantAppInstancesPaged
} from "@/services/api-tenant-app-instances";
import { useUserStore } from "@/stores/user";
import type {
  ResourceCenterDataSourceConsumptionResponse,
  ResourceCenterGroupItem,
  TenantAppInstanceListItem
} from "@/types/platform-v2";

const router = useRouter();
const userStore = useUserStore();
const loading = ref(false);
const resourceGroupLoading = ref(false);
const dataSourceConsumptionLoading = ref(false);
const keyword = ref("");
const apps = ref<TenantAppInstanceListItem[]>([]);
const resourceGroups = ref<ResourceCenterGroupItem[]>([]);
const dataSourceConsumption = ref<ResourceCenterDataSourceConsumptionResponse | null>(null);
const createWizardVisible = ref(false);

const profileDisplayName = computed(() => userStore.profile?.displayName || userStore.profile?.username || "Admin");

const todayDate = computed(() =>
  new Date().toLocaleDateString("en-US", {
    year: "numeric",
    month: "long",
    day: "numeric"
  })
);

async function loadApps() {
  loading.value = true;
  try {
    const result = await getTenantAppInstancesPaged({
      pageIndex: 1,
      pageSize: 60,
      keyword: keyword.value || undefined
    });
    apps.value = result.items;
  } catch (error) {
    message.error((error as Error).message || "Failed to load apps");
  } finally {
    loading.value = false;
  }
}

async function loadResourceGroups() {
  resourceGroupLoading.value = true;
  try {
    resourceGroups.value = await getResourceCenterGroups();
  } catch (error) {
    message.error((error as Error).message || "Failed to load resource groups");
  } finally {
    resourceGroupLoading.value = false;
  }
}

async function loadDataSourceConsumption() {
  dataSourceConsumptionLoading.value = true;
  try {
    dataSourceConsumption.value = await getResourceCenterDataSourceConsumption();
  } catch (error) {
    message.error((error as Error).message || "Failed to load datasource consumption");
  } finally {
    dataSourceConsumptionLoading.value = false;
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

  return date.toLocaleString("en-US");
}

function openApp(id: string) {
  router.push(`/apps/${id}`);
}

function go(path: string) {
  router.push(path);
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
