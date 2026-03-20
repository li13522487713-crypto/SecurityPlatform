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
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getResourceCenterDataSourceConsumption } from "@/services/api-tenant-app-instances";
import type {
  ResourceCenterDataSourceConsumptionResponse,
  TenantAppConsumerItem,
  TenantDataSourceBindingRelationItem,
  TenantDataSourceConsumptionItem
} from "@/types/platform-v2";

const router = useRouter();
const loading = ref(false);
const summary = ref<ResourceCenterDataSourceConsumptionResponse | null>(null);

const dataSourceColumns: TableColumnsType<TenantDataSourceConsumptionItem> = [
  { title: "数据源名称", dataIndex: "name", key: "name", width: 220 },
  { title: "类型", dataIndex: "dbType", key: "dbType", width: 140 },
  { title: "绑定应用数", dataIndex: "boundTenantAppCount", key: "boundTenantAppCount", width: 120 },
  { title: "最近测试时间", dataIndex: "lastTestedAt", key: "lastTestedAt", width: 190 },
  { title: "是否激活", dataIndex: "isActive", key: "isActive", width: 120 }
];

const appScopedColumns: TableColumnsType<TenantDataSourceConsumptionItem> = [
  { title: "数据源名称", dataIndex: "name", key: "name", width: 220 },
  { title: "作用域应用", dataIndex: "scopeAppName", key: "scopeAppName", width: 180 },
  { title: "类型", dataIndex: "dbType", key: "dbType", width: 120 },
  { title: "绑定应用数", dataIndex: "boundTenantAppCount", key: "boundTenantAppCount", width: 120 },
  { title: "最近测试时间", dataIndex: "lastTestedAt", key: "lastTestedAt", width: 190 },
  { title: "是否激活", dataIndex: "isActive", key: "isActive", width: 120 }
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
  return record.boundTenantAppCount === 0 ? "danger-row" : "";
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
