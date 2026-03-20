<template>
  <div class="resource-center-page" data-testid="e2e-resource-center-page">
    <a-page-header title="资源中心" sub-title="平台资源分组与数据源绑定概览">
      <template #extra>
        <a-button type="primary" @click="goToConsumptionPage">查看消费分析</a-button>
      </template>
    </a-page-header>

    <a-row :gutter="[16, 16]" class="summary-row">
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic title="应用目录总数" :value="catalogTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic title="租户应用实例总数" :value="instanceTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic title="平台级数据源总数" :value="platformDataSourceTotal" />
        </a-card>
      </a-col>
      <a-col :xs="24" :md="12" :xl="6">
        <a-card>
          <a-statistic title="未绑定数据源应用实例数" :value="unboundTenantAppTotal" />
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]" class="group-row">
      <a-col
        v-for="group in resourceGroups"
        :key="group.groupKey"
        :xs="24"
        :xl="8"
      >
        <a-card :loading="loading" :title="group.groupName">
          <template #extra>
            <a-tag color="blue">共 {{ group.total }} 项</a-tag>
          </template>
          <a-table
            row-key="resourceId"
            :columns="groupColumns"
            :data-source="group.items"
            :pagination="false"
            size="small"
          />
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getResourceCenterDataSourceConsumption, getResourceCenterGroups } from "@/services/api-tenant-app-instances";
import type {
  ResourceCenterDataSourceConsumptionResponse,
  ResourceCenterGroupEntry,
  ResourceCenterGroupItem
} from "@/types/platform-v2";

const router = useRouter();
const loading = ref(false);
const resourceGroups = ref<ResourceCenterGroupItem[]>([]);
const dataSourceConsumption = ref<ResourceCenterDataSourceConsumptionResponse | null>(null);

const groupColumns: TableColumnsType<ResourceCenterGroupEntry> = [
  { title: "名称", dataIndex: "resourceName", key: "resourceName", ellipsis: true },
  { title: "类型", dataIndex: "resourceType", key: "resourceType", width: 150 },
  { title: "状态", dataIndex: "status", key: "status", width: 120 },
  { title: "描述", dataIndex: "description", key: "description", ellipsis: true }
];

const catalogTotal = computed(() => resourceGroups.value.find((item) => item.groupKey === "catalogs")?.total ?? 0);
const instanceTotal = computed(() => resourceGroups.value.find((item) => item.groupKey === "instances")?.total ?? 0);
const platformDataSourceTotal = computed(() => dataSourceConsumption.value?.platformDataSourceTotal ?? 0);
const unboundTenantAppTotal = computed(() => dataSourceConsumption.value?.unboundTenantAppTotal ?? 0);

async function loadResourceCenterData() {
  loading.value = true;
  try {
    const [groups, consumption] = await Promise.all([
      getResourceCenterGroups(),
      getResourceCenterDataSourceConsumption()
    ]);
    resourceGroups.value = groups;
    dataSourceConsumption.value = consumption;
  } catch (error) {
    message.error((error as Error).message || "加载资源中心数据失败");
  } finally {
    loading.value = false;
  }
}

function goToConsumptionPage() {
  void router.push("/console/resources/datasource-consumption");
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
