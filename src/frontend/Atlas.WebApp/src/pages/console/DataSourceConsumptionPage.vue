<template>
  <div class="datasource-consumption-page" data-testid="e2e-datasource-consumption-page">
    <a-page-header title="数据源消费分析" sub-title="平台级与应用级数据源绑定关系分析">
      <template #extra>
        <a-button @click="goResourceCenter">返回资源中心</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="[16, 16]" class="summary-row">
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic title="平台级数据源数" :value="summary?.platformDataSourceTotal ?? 0" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic title="应用级数据源数" :value="summary?.appScopedDataSourceTotal ?? 0" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="8">
        <a-card>
          <a-statistic title="未绑定应用实例数" :value="summary?.unboundTenantAppTotal ?? 0" />
        </a-card>
      </a-col>
    </a-row>

    <a-card title="平台级数据源列表" class="block-card" :loading="loading">
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
            <a-tag :color="record.isActive ? 'success' : 'default'">{{ record.isActive ? "是" : "否" }}</a-tag>
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
                禁用无效绑定
              </a-button>
              <a-button
                v-if="record.isDuplicate || record.isUnbound"
                type="link"
                size="small"
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'switch'"
                @click="repairSwitchPrimary(record)"
              >
                切换主绑定
              </a-button>
              <a-button
                v-if="record.isOrphan"
                type="link"
                size="small"
                danger
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'unbind'"
                @click="repairUnbindOrphan(record)"
              >
                解绑孤儿绑定
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card title="应用级数据源列表" class="block-card" :loading="loading">
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
            <a-tag :color="record.isActive ? 'success' : 'default'">{{ record.isActive ? "是" : "否" }}</a-tag>
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
                禁用无效绑定
              </a-button>
              <a-button
                v-if="record.isDuplicate || record.isUnbound"
                type="link"
                size="small"
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'switch'"
                @click="repairSwitchPrimary(record)"
              >
                切换主绑定
              </a-button>
              <a-button
                v-if="record.isOrphan"
                type="link"
                size="small"
                danger
                :loading="repairingDataSourceId === record.dataSourceId && repairAction === 'unbind'"
                @click="repairUnbindOrphan(record)"
              >
                解绑孤儿绑定
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card title="未绑定清单" class="block-card" :loading="loading">
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
import { onMounted, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

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

const dataSourceColumns: TableColumnsType<TenantDataSourceConsumptionItem> = [
  { title: "数据源名称", dataIndex: "name", key: "name", width: 220 },
  { title: "类型", dataIndex: "dbType", key: "dbType", width: 140 },
  { title: "绑定应用数", dataIndex: "boundTenantAppCount", key: "boundTenantAppCount", width: 120 },
  { title: "最近测试时间", dataIndex: "lastTestedAt", key: "lastTestedAt", width: 190 },
  { title: "是否激活", dataIndex: "isActive", key: "isActive", width: 120 },
  { title: "异常", key: "anomaly", width: 260 },
  { title: "治理动作", key: "repair", width: 280 }
];

const appScopedColumns: TableColumnsType<TenantDataSourceConsumptionItem> = [
  { title: "数据源名称", dataIndex: "name", key: "name", width: 220 },
  { title: "作用域应用", dataIndex: "scopeAppName", key: "scopeAppName", width: 180 },
  { title: "类型", dataIndex: "dbType", key: "dbType", width: 120 },
  { title: "绑定应用数", dataIndex: "boundTenantAppCount", key: "boundTenantAppCount", width: 120 },
  { title: "最近测试时间", dataIndex: "lastTestedAt", key: "lastTestedAt", width: 190 },
  { title: "是否激活", dataIndex: "isActive", key: "isActive", width: 120 },
  { title: "异常", key: "anomaly", width: 260 },
  { title: "治理动作", key: "repair", width: 280 }
];

const bindingRelationColumns: TableColumnsType<TenantDataSourceBindingRelationItem> = [
  { title: "BindingType", dataIndex: "bindingType", key: "bindingType", width: 150 },
  { title: "Source", dataIndex: "source", key: "source", width: 220 },
  { title: "IsActive", dataIndex: "isActive", key: "isActive", width: 120 },
  { title: "BoundAt", dataIndex: "boundAt", key: "boundAt", width: 200 }
];

const unboundColumns: TableColumnsType<TenantAppConsumerItem> = [
  { title: "AppKey", dataIndex: "appKey", key: "appKey", width: 180 },
  { title: "应用名称", dataIndex: "name", key: "name", width: 220 },
  { title: "状态", dataIndex: "status", key: "status", width: 120 }
];

async function loadSummary() {
  loading.value = true;
  try {
    summary.value = await getResourceCenterDataSourceConsumption();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || "加载数据源消费分析失败");
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
    tags.push("孤儿");
  }
  if (record.isDuplicate) {
    tags.push("重复");
  }
  if (record.isInvalid) {
    tags.push("失效");
  }
  if (record.isUnbound) {
    tags.push("未绑定");
  }

  if (tags.length === 0) {
    tags.push("正常");
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
    message.warning("未找到可处理的绑定关系");
    return;
  }

  repairingDataSourceId.value = record.dataSourceId;
  repairAction.value = "disable";
  try {
    const result  = await disableInvalidBinding(bindingId);

    if (!isMounted.value) return;
    message.success(result.message);
    await loadSummary();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || "禁用无效绑定失败");
  } finally {
    repairingDataSourceId.value = undefined;
    repairAction.value = undefined;
  }
}

async function repairSwitchPrimary(record: TenantDataSourceConsumptionItem) {
  const appId = resolveAppId(record);
  if (!appId) {
    message.warning("未找到可切换主绑定的应用实例");
    return;
  }

  repairingDataSourceId.value = record.dataSourceId;
  repairAction.value = "switch";
  try {
    const result  = await switchPrimaryBinding(appId, record.dataSourceId, "resource-center-repair");

    if (!isMounted.value) return;
    message.success(result.message);
    await loadSummary();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || "切换主绑定失败");
  } finally {
    repairingDataSourceId.value = undefined;
    repairAction.value = undefined;
  }
}

async function repairUnbindOrphan(record: TenantDataSourceConsumptionItem) {
  const bindingId = resolveBindingId(record);
  if (!bindingId) {
    message.warning("未找到孤儿绑定记录");
    return;
  }

  repairingDataSourceId.value = record.dataSourceId;
  repairAction.value = "unbind";
  try {
    const result  = await unbindOrphanBinding(bindingId);

    if (!isMounted.value) return;
    message.success(result.message);
    await loadSummary();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || "解绑孤儿绑定失败");
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
