<template>
  <div class="coze-debug-page" data-testid="e2e-coze-debug-page">
    <a-row :gutter="[16, 16]">
      <a-col :xs="24" :lg="12">
        <a-card title="Coze 六层映射总览" :loading="loadingMappings">
          <a-list :data-source="mappingRows" size="small">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta :title="item.layerName" :description="item.description" />
                <a-tag color="blue">{{ item.total }}</a-tag>
              </a-list-item>
            </template>
          </a-list>
        </a-card>
      </a-col>

      <a-col :xs="24" :lg="12">
        <a-card title="调试层嵌入元数据" :loading="loadingMetadata">
          <a-descriptions :column="1" size="small">
            <a-descriptions-item label="TenantId">{{ metadata?.tenantId || "-" }}</a-descriptions-item>
            <a-descriptions-item label="AppId">{{ metadata?.appId || "-" }}</a-descriptions-item>
            <a-descriptions-item label="ProjectId">{{ metadata?.projectId || "-" }}</a-descriptions-item>
            <a-descriptions-item label="ProjectScopeEnabled">
              {{ metadata?.projectScopeEnabled ? "true" : "false" }}
            </a-descriptions-item>
          </a-descriptions>
          <a-divider />
          <a-alert
            v-if="(metadata?.resources?.length ?? 0) === 0"
            type="warning"
            show-icon
            message="当前账号没有调试层资源权限"
            description="请联系管理员分配 debug:view / debug:run / debug:manage 权限。"
            style="margin-bottom: 12px"
          />
          <a-list :data-source="metadata?.resources || []" size="small">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-space direction="vertical" :size="2">
                  <span class="resource-title">{{ item.resourceName }}</span>
                  <span class="resource-desc">{{ item.description }}</span>
                  <a-tag>{{ item.requiredPermission }}</a-tag>
                </a-space>
              </a-list-item>
            </template>
          </a-list>
        </a-card>
      </a-col>
    </a-row>

    <a-card title="运行执行列表（下钻）" class="runtime-card" :loading="loadingRuntimeExecutions">
      <template #extra>
        <a-space wrap>
          <a-input-search
            v-model:value="runtimeKeyword"
            placeholder="按工作流ID/状态检索"
            style="width: 240px"
            allow-clear
            @search="loadRuntimeExecutions"
          />
          <a-input
            v-model:value="releaseFilter"
            placeholder="按发布ID过滤（可选）"
            style="width: 220px"
          />
          <a-button type="primary" @click="loadRuntimeExecutions">刷新</a-button>
        </a-space>
      </template>

      <a-alert
        v-if="!canViewRuntimeExecutions"
        type="warning"
        show-icon
        message="无运行执行查看权限"
        description="该模块需要 debug:view 权限。"
      />
      <a-table
        v-else
        row-key="id"
        class="runtime-table"
        size="small"
        :columns="runtimeColumns"
        :data-source="runtimeRowsFiltered"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === 'Completed' ? 'success' : record.status === 'Failed' ? 'error' : 'processing'">
              {{ record.status }}
            </a-tag>
          </template>
          <template v-if="column.key === 'startedAt'">
            {{ formatDate(record.startedAt) }}
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" size="small" @click="traceExecution(record.id)">审计追溯</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card title="运行执行审计追溯" class="audit-card">
      <a-alert
        v-if="!canViewRuntimeAudits"
        type="warning"
        show-icon
        message="无运行审计追溯权限"
        description="该模块需要 debug:run 权限。"
        style="margin-bottom: 12px"
      />
      <a-space wrap>
        <a-input v-model:value="executionId" placeholder="输入执行ID" style="width: 220px" />
        <a-input-search
          v-model:value="auditKeyword"
          placeholder="关键字（action/target/actor）"
          style="width: 260px"
          allow-clear
          @search="loadAuditTrails"
        />
        <a-button type="primary" :disabled="!canViewRuntimeAudits" @click="loadAuditTrails">查询</a-button>
      </a-space>

      <a-table
        row-key="auditId"
        class="audit-table"
        :loading="loadingAudits"
        :columns="auditColumns"
        :data-source="auditRows"
        :pagination="false"
        size="small"
      />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { TableColumnsType } from "ant-design-vue";
import {
  getCozeLayerMappingsOverview,
  getDebugLayerEmbedMetadata,
  getRuntimeExecutionsPaged,
  getRuntimeExecutionAuditTrails
} from "@/services/api-coze-runtime";
import type {
  CozeLayerMappingItem,
  DebugLayerEmbedMetadata,
  RuntimeExecutionListItem,
  RuntimeExecutionAuditTrailItem
} from "@/types/platform-v2";

const route = useRoute();
const router = useRouter();
const loadingMappings = ref(false);
const loadingMetadata = ref(false);
const loadingRuntimeExecutions = ref(false);
const loadingAudits = ref(false);
const mappingRows = ref<CozeLayerMappingItem[]>([]);
const metadata = ref<DebugLayerEmbedMetadata | null>(null);
const runtimeRows = ref<RuntimeExecutionListItem[]>([]);
const executionId = ref("");
const runtimeKeyword = ref("");
const releaseFilter = ref("");
const auditKeyword = ref("");
const auditRows = ref<RuntimeExecutionAuditTrailItem[]>([]);

const permissionKeys = computed(() =>
  new Set((metadata.value?.resources ?? []).map((item) => item.resourceKey))
);
const canViewRuntimeExecutions = computed(() => permissionKeys.value.has("workflow-executions"));
const canViewRuntimeAudits = computed(() => permissionKeys.value.has("runtime-audit-trails"));
const runtimeRowsFiltered = computed(() => {
  const releaseId = releaseFilter.value.trim();
  if (!releaseId) {
    return runtimeRows.value;
  }

  return runtimeRows.value.filter((item) => item.releaseId === releaseId);
});

const auditColumns: TableColumnsType<RuntimeExecutionAuditTrailItem> = [
  { title: "审计ID", dataIndex: "auditId", key: "auditId", width: 160 },
  { title: "操作人", dataIndex: "actor", key: "actor", width: 140 },
  { title: "动作", dataIndex: "action", key: "action", width: 180 },
  { title: "结果", dataIndex: "result", key: "result", width: 120 },
  { title: "目标", dataIndex: "target", key: "target", ellipsis: true },
  { title: "发生时间", dataIndex: "occurredAt", key: "occurredAt", width: 190 }
];
const runtimeColumns: TableColumnsType<RuntimeExecutionListItem> = [
  { title: "执行ID", dataIndex: "id", key: "id", width: 170 },
  { title: "工作流ID", dataIndex: "workflowId", key: "workflowId", width: 130 },
  { title: "发布ID", dataIndex: "releaseId", key: "releaseId", width: 130 },
  { title: "运行上下文ID", dataIndex: "runtimeContextId", key: "runtimeContextId", width: 140 },
  { title: "状态", dataIndex: "status", key: "status", width: 120 },
  { title: "开始时间", dataIndex: "startedAt", key: "startedAt", width: 190 },
  { title: "操作", key: "actions", width: 120 }
];

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }
  return new Date(value).toLocaleString();
}

async function loadMappings() {
  loadingMappings.value = true;
  try {
    const overview = await getCozeLayerMappingsOverview();
    mappingRows.value = overview.layers;
  } catch (error) {
    message.error((error as Error).message || "加载 Coze 映射失败");
  } finally {
    loadingMappings.value = false;
  }
}

async function loadMetadata() {
  loadingMetadata.value = true;
  try {
    metadata.value = await getDebugLayerEmbedMetadata();
    if (canViewRuntimeExecutions.value) {
      await loadRuntimeExecutions();
    }
  } catch (error) {
    message.error((error as Error).message || "加载调试层元数据失败");
  } finally {
    loadingMetadata.value = false;
  }
}

async function loadRuntimeExecutions() {
  if (!canViewRuntimeExecutions.value) {
    runtimeRows.value = [];
    return;
  }

  loadingRuntimeExecutions.value = true;
  try {
    const result = await getRuntimeExecutionsPaged({
      pageIndex: 1,
      pageSize: 50,
      keyword: runtimeKeyword.value || undefined
    });
    runtimeRows.value = result.items;
  } catch (error) {
    message.error((error as Error).message || "加载运行执行列表失败");
  } finally {
    loadingRuntimeExecutions.value = false;
  }
}

async function loadAuditTrails() {
  if (!canViewRuntimeAudits.value) {
    message.warning("当前账号缺少运行审计追溯权限");
    return;
  }
  if (!executionId.value.trim()) {
    message.warning("请先输入执行ID");
    return;
  }

  loadingAudits.value = true;
  try {
    const result = await getRuntimeExecutionAuditTrails(executionId.value.trim(), {
      pageIndex: 1,
      pageSize: 20,
      keyword: auditKeyword.value || undefined
    });
    auditRows.value = result.items;
  } catch (error) {
    message.error((error as Error).message || "加载执行审计失败");
  } finally {
    loadingAudits.value = false;
  }
}

async function traceExecution(id: string) {
  executionId.value = id;
  await loadAuditTrails();
}

onMounted(async () => {
  if (typeof route.query.releaseId === "string" && route.query.releaseId.trim()) {
    releaseFilter.value = route.query.releaseId.trim();
  }
  if (typeof route.query.executionId === "string" && route.query.executionId.trim()) {
    executionId.value = route.query.executionId.trim();
  }

  await Promise.all([loadMappings(), loadMetadata()]);
  if (executionId.value) {
    await loadAuditTrails();
  }
  void router.replace({
    query: {
      ...route.query,
      releaseId: releaseFilter.value || undefined,
      executionId: executionId.value || undefined
    }
  });
});
</script>

<style scoped>
.coze-debug-page {
  padding: 24px;
}

.audit-card {
  margin-top: 16px;
}

.runtime-card {
  margin-top: 16px;
}

.runtime-table {
  margin-top: 12px;
}

.audit-table {
  margin-top: 12px;
}

.resource-title {
  font-weight: 500;
}

.resource-desc {
  color: #8c8c8c;
  font-size: 12px;
}
</style>
