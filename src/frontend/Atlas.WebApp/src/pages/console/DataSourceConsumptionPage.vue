<template>
  <div class="datasource-consumption-page" data-testid="e2e-datasource-consumption-page">
    <a-page-header :title="t('console.dataSourceConsumption.title')" :sub-title="t('console.dataSourceConsumption.subtitle')">
      <template #extra>
        <a-button @click="goResourceCenter">{{ t("console.dataSourceConsumption.backResource") }}</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="[16, 16]" class="summary-row">
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic :title="t('console.dataSourceConsumption.statPlatform')" :value="summary?.platformDataSourceTotal ?? 0" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic :title="t('console.dataSourceConsumption.statApp')" :value="summary?.appScopedDataSourceTotal ?? 0" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic :title="t('console.dataSourceConsumption.statUnbound')" :value="summary?.unboundTenantAppTotal ?? 0" />
        </a-card>
      </a-col>
    </a-row>

    <a-card :title="t('console.dataSourceConsumption.cardPlatformList')" class="block-card" :loading="loading">
      <a-table
        row-key="dataSourceId"
        :columns="dataSourceColumns"
        :data-source="summary?.platformDataSources ?? []"
        :row-class-name="resolveDangerRowClass"
        size="small"
      >
        <template #expandedRowRender="{ record }">
          <a-table
            row-key="bindingId"
            :columns="bindingRelationColumns"
            :data-source="record.bindingRelations"
            :pagination="false"
            size="small"
          />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'name'">
            <a-button type="link" size="small" @click="goToDatasourceDetail(record.dataSourceId)">
              {{ record.name }}
            </a-button>
          </template>
          <template v-else-if="column.key === 'boundTenantAppCount'">
            <span :class="{ 'zero-bind-count': record.boundTenantAppCount === 0 }">
              {{ record.boundTenantAppCount }}
            </span>
          </template>
          <template v-else-if="column.key === 'lastTestedAt'">
            {{ formatDate(record.lastTestedAt) }}
          </template>
          <template v-else-if="column.key === 'isActive'">
            <a-tag :color="record.isActive ? 'success' : 'default'">{{
              record.isActive ? t("console.dataSourceConsumption.yes") : t("console.dataSourceConsumption.no")
            }}</a-tag>
          </template>
          <template v-else-if="column.key === 'anomaly'">
            <a-space wrap>
              <a-tag
                v-for="tag in getAnomalyTags(record)"
                :key="`${record.dataSourceId}-${tag}`"
                color="warning"
              >
                {{ tag }}
              </a-tag>
            </a-space>
          </template>
          <template v-else-if="column.key === 'repair'">
            <a-space wrap>
              <a-button
                v-if="record.isInvalid"
                type="link"
                size="small"
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'disable'"
                @click="repairDisableInvalid(record)"
              >
                {{ t("console.dataSourceConsumption.disableInvalid") }}
              </a-button>
              <a-button
                v-if="record.isDuplicate || record.isUnbound"
                type="link"
                size="small"
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'switch'"
                @click="repairSwitchPrimary(record)"
              >
                {{ t("console.dataSourceConsumption.switchPrimary") }}
              </a-button>
              <a-button
                v-if="record.isOrphan"
                type="link"
                size="small"
                danger
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'unbind'"
                @click="repairUnbindOrphan(record)"
              >
                {{ t("console.dataSourceConsumption.unbindOrphan") }}
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card :title="t('console.dataSourceConsumption.cardAppList')" class="block-card" :loading="loading">
      <a-table
        row-key="dataSourceId"
        :columns="appScopedColumns"
        :data-source="summary?.appScopedDataSources ?? []"
        :row-class-name="resolveDangerRowClass"
        size="small"
      >
        <template #expandedRowRender="{ record }">
          <a-table
            row-key="bindingId"
            :columns="bindingRelationColumns"
            :data-source="record.bindingRelations"
            :pagination="false"
            size="small"
          />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'name'">
            <a-button type="link" size="small" @click="goToDatasourceDetail(record.dataSourceId)">
              {{ record.name }}
            </a-button>
          </template>
          <template v-else-if="column.key === 'boundTenantAppCount'">
            <span :class="{ 'zero-bind-count': record.boundTenantAppCount === 0 }">
              {{ record.boundTenantAppCount }}
            </span>
          </template>
          <template v-else-if="column.key === 'lastTestedAt'">
            {{ formatDate(record.lastTestedAt) }}
          </template>
          <template v-else-if="column.key === 'isActive'">
            <a-tag :color="record.isActive ? 'success' : 'default'">{{
              record.isActive ? t("console.dataSourceConsumption.yes") : t("console.dataSourceConsumption.no")
            }}</a-tag>
          </template>
          <template v-else-if="column.key === 'anomaly'">
            <a-space wrap>
              <a-tag
                v-for="tag in getAnomalyTags(record)"
                :key="`${record.dataSourceId}-${tag}`"
                color="warning"
              >
                {{ tag }}
              </a-tag>
            </a-space>
          </template>
          <template v-else-if="column.key === 'repair'">
            <a-space wrap>
              <a-button
                v-if="record.isInvalid"
                type="link"
                size="small"
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'disable'"
                @click="repairDisableInvalid(record)"
              >
                {{ t("console.dataSourceConsumption.disableInvalid") }}
              </a-button>
              <a-button
                v-if="record.isDuplicate || record.isUnbound"
                type="link"
                size="small"
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'switch'"
                @click="repairSwitchPrimary(record)"
              >
                {{ t("console.dataSourceConsumption.switchPrimary") }}
              </a-button>
              <a-button
                v-if="record.isOrphan"
                type="link"
                size="small"
                danger
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'unbind'"
                @click="repairUnbindOrphan(record)"
              >
                {{ t("console.dataSourceConsumption.unbindOrphan") }}
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card :title="t('console.dataSourceConsumption.cardUnbound')" class="block-card" :loading="loading">
      <a-table
        row-key="tenantAppInstanceId"
        :columns="unboundColumns"
        :data-source="summary?.unboundTenantApps ?? []"
        size="small"
      />
    </a-card>
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

import { useRouter } from "vue-router";
import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  disableInvalidBinding,
  getResourceCenterDataSourceConsumption,
  switchPrimaryBinding,
  unbindOrphanBinding
} from "@/services/api-tenant-app-instances";
import type {
  ResourceCenterDataSourceConsumptionResponse,
  TenantAppConsumerItem,
  TenantDataSourceBindingRelationItem,
  TenantDataSourceConsumptionItem
} from "@/types/platform-v2";

const router = useRouter();
const loading = ref(false);
const summary = ref<ResourceCenterDataSourceConsumptionResponse | null>(null);
const repairingDataSourceId = ref<string>();
const repairAction = ref<"disable" | "switch" | "unbind">();

const dataSourceColumns = computed<TableColumnsType<TenantDataSourceConsumptionItem>>(() => [
  { title: t("console.dataSourceConsumption.colDsName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("console.dataSourceConsumption.colDbType"), dataIndex: "dbType", key: "dbType", width: 140 },
  { title: t("console.dataSourceConsumption.colBoundApps"), dataIndex: "boundTenantAppCount", key: "boundTenantAppCount", width: 120 },
  { title: t("console.dataSourceConsumption.colLastTested"), dataIndex: "lastTestedAt", key: "lastTestedAt", width: 190 },
  { title: t("console.dataSourceConsumption.colIsActive"), dataIndex: "isActive", key: "isActive", width: 120 },
  { title: t("console.dataSourceConsumption.colAnomaly"), key: "anomaly", width: 260 },
  { title: t("console.dataSourceConsumption.colRepair"), key: "repair", width: 280 }
]);

const appScopedColumns = computed<TableColumnsType<TenantDataSourceConsumptionItem>>(() => [
  { title: t("console.dataSourceConsumption.colDsName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("console.dataSourceConsumption.colScopeApp"), dataIndex: "scopeAppName", key: "scopeAppName", width: 180 },
  { title: t("console.dataSourceConsumption.colDbType"), dataIndex: "dbType", key: "dbType", width: 120 },
  { title: t("console.dataSourceConsumption.colBoundApps"), dataIndex: "boundTenantAppCount", key: "boundTenantAppCount", width: 120 },
  { title: t("console.dataSourceConsumption.colLastTested"), dataIndex: "lastTestedAt", key: "lastTestedAt", width: 190 },
  { title: t("console.dataSourceConsumption.colIsActive"), dataIndex: "isActive", key: "isActive", width: 120 },
  { title: t("console.dataSourceConsumption.colAnomaly"), key: "anomaly", width: 260 },
  { title: t("console.dataSourceConsumption.colRepair"), key: "repair", width: 280 }
]);

const bindingRelationColumns = computed<TableColumnsType<TenantDataSourceBindingRelationItem>>(() => [
  { title: t("console.dataSourceConsumption.colBindingType"), dataIndex: "bindingType", key: "bindingType", width: 150 },
  { title: t("console.dataSourceConsumption.colSource"), dataIndex: "source", key: "source", width: 220 },
  { title: t("console.dataSourceConsumption.colIsActive"), dataIndex: "isActive", key: "isActive", width: 120 },
  { title: t("console.dataSourceConsumption.colBoundAt"), dataIndex: "boundAt", key: "boundAt", width: 200 }
]);

const unboundColumns = computed<TableColumnsType<TenantAppConsumerItem>>(() => [
  { title: t("console.dataSourceConsumption.colUnboundAppKey"), dataIndex: "appKey", key: "appKey", width: 180 },
  { title: t("console.dataSourceConsumption.colUnboundName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("console.dataSourceConsumption.colUnboundStatus"), dataIndex: "status", key: "status", width: 120 }
]);

async function loadSummary() {
  loading.value = true;
  try {
    summary.value = await getResourceCenterDataSourceConsumption();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("console.dataSourceConsumption.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString();
}

function resolveDangerRowClass(record: TenantDataSourceConsumptionItem) {
  return record.isOrphan || record.isDuplicate || record.isInvalid || record.isUnbound ? "danger-row" : "";
}

function getAnomalyTags(record: TenantDataSourceConsumptionItem) {
  const tags: string[] = [];
  if (record.isOrphan) {
    tags.push(t("console.dataSourceConsumption.tagOrphan"));
  }
  if (record.isDuplicate) {
    tags.push(t("console.dataSourceConsumption.tagDuplicate"));
  }
  if (record.isInvalid) {
    tags.push(t("console.dataSourceConsumption.tagInvalid"));
  }
  if (record.isUnbound) {
    tags.push(t("console.dataSourceConsumption.tagUnboundTag"));
  }

  if (tags.length === 0) {
    tags.push(t("console.dataSourceConsumption.tagNormal"));
  }
  return tags;
}

function resolveBindingId(record: TenantDataSourceConsumptionItem) {
  return record.bindingRelations[0]?.bindingId;
}

function resolveAppId(record: TenantDataSourceConsumptionItem) {
  return record.scopeAppId || record.boundTenantApps[0]?.tenantAppInstanceId;
}

async function repairDisableInvalid(record: TenantDataSourceConsumptionItem) {
  const bindingId = resolveBindingId(record);
  if (!bindingId) {
    message.warning(t("console.dataSourceConsumption.warnNoBinding"));
    return;
  }

  repairingDataSourceId.value = record.dataSourceId;
  repairAction.value = "disable";
  try {
    const result = await disableInvalidBinding(bindingId);

    if (!isMounted.value) return;
    message.success(result.message);
    await loadSummary();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("console.dataSourceConsumption.errDisableBinding"));
  } finally {
    repairingDataSourceId.value = undefined;
    repairAction.value = undefined;
  }
}

async function repairSwitchPrimary(record: TenantDataSourceConsumptionItem) {
  const appId = resolveAppId(record);
  if (!appId) {
    message.warning(t("console.dataSourceConsumption.warnNoAppForSwitch"));
    return;
  }

  repairingDataSourceId.value = record.dataSourceId;
  repairAction.value = "switch";
  try {
    const result = await switchPrimaryBinding(appId, record.dataSourceId, "resource-center-repair");

    if (!isMounted.value) return;
    message.success(result.message);
    await loadSummary();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("console.dataSourceConsumption.errSwitchPrimary"));
  } finally {
    repairingDataSourceId.value = undefined;
    repairAction.value = undefined;
  }
}

async function repairUnbindOrphan(record: TenantDataSourceConsumptionItem) {
  const bindingId = resolveBindingId(record);
  if (!bindingId) {
    message.warning(t("console.dataSourceConsumption.warnNoOrphanBinding"));
    return;
  }

  repairingDataSourceId.value = record.dataSourceId;
  repairAction.value = "unbind";
  try {
    const result = await unbindOrphanBinding(bindingId);

    if (!isMounted.value) return;
    message.success(result.message);
    await loadSummary();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("console.dataSourceConsumption.errUnbindOrphan"));
  } finally {
    repairingDataSourceId.value = undefined;
    repairAction.value = undefined;
  }
}

function goToDatasourceDetail(dataSourceId: string) {
  void router.push({
    path: "/settings/system/datasources",
    query: { dataSourceId }
  });
}

function goResourceCenter() {
  void router.push("/console/resources");
}

onMounted(() => {
  void loadSummary();
});
</script>

<style scoped>
.datasource-consumption-page {
  padding: 24px;
}

.summary-row {
  margin-top: 8px;
}

.block-card {
  margin-top: 16px;
}

:deep(.danger-row > td) {
  background: #fff1f0 !important;
}

.zero-bind-count {
  color: #cf1322;
  font-weight: 600;
}
</style>
