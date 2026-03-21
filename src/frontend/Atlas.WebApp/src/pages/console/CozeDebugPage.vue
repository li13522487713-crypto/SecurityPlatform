<template>
  <div class="coze-debug-page" data-testid="e2e-coze-debug-page">
    <a-row :gutter="[16, 16]">
      <a-col :xs="24" :lg="12">
        <a-card :title="t('console.coze.cardMappings')" :loading="loadingMappings">
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
        <a-card :title="t('console.coze.cardMetadata')" :loading="loadingMetadata">
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
            :message="t('console.coze.noDebugPerm')"
            :description="t('console.coze.noDebugPermDesc')"
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

    <a-card :title="t('console.coze.cardExecutions')" class="runtime-card" :loading="loadingRuntimeExecutions">
      <template #extra>
        <a-space wrap>
          <a-input-search
            v-model:value="runtimeKeyword"
            :placeholder="t('console.coze.phWorkflow')"
            style="width: 240px"
            allow-clear
            @search="loadRuntimeExecutions"
          />
          <a-input
            v-model:value="releaseFilter"
            :placeholder="t('console.coze.phRelease')"
            style="width: 220px"
          />
          <a-button type="primary" @click="loadRuntimeExecutions">{{ t("console.coze.refresh") }}</a-button>
        </a-space>
      </template>

      <a-alert
        v-if="!canViewRuntimeExecutions"
        type="warning"
        show-icon
        :message="t('console.coze.noExecPerm')"
        :description="t('console.coze.noExecPermDesc')"
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
              <a-button type="link" size="small" @click="traceExecution(record.id)">{{ t("console.coze.traceAudit") }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-card :title="t('console.coze.cardAuditTrace')" class="audit-card">
      <a-alert
        v-if="!canViewRuntimeAudits"
        type="warning"
        show-icon
        :message="t('console.coze.noAuditPerm')"
        :description="t('console.coze.noAuditPermDesc')"
        style="margin-bottom: 12px"
      />
      <a-space wrap>
        <a-input v-model:value="executionId" :placeholder="t('console.coze.phExecutionId')" style="width: 220px" />
        <a-input-search
          v-model:value="auditKeyword"
          :placeholder="t('console.coze.phAuditKeyword')"
          style="width: 260px"
          allow-clear
          @search="loadAuditTrails"
        />
        <a-button type="primary" :disabled="!canViewRuntimeAudits" @click="loadAuditTrails">{{ t("console.coze.search") }}</a-button>
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
import { computed, onMounted, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
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
const { t } = useI18n();
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

const auditColumns = computed<TableColumnsType<RuntimeExecutionAuditTrailItem>>(() => [
  { title: t("console.runtimeExec.auditColId"), dataIndex: "auditId", key: "auditId", width: 160 },
  { title: t("console.runtimeExec.auditColActor"), dataIndex: "actor", key: "actor", width: 140 },
  { title: t("console.runtimeExec.auditColAction"), dataIndex: "action", key: "action", width: 180 },
  { title: t("console.runtimeExec.auditColResult"), dataIndex: "result", key: "result", width: 120 },
  { title: t("console.runtimeExec.auditColTarget"), dataIndex: "target", key: "target", ellipsis: true },
  { title: t("console.runtimeExec.auditColTime"), dataIndex: "occurredAt", key: "occurredAt", width: 190 }
]);
const runtimeColumns = computed<TableColumnsType<RuntimeExecutionListItem>>(() => [
  { title: t("console.runtimeExec.labelExecId"), dataIndex: "id", key: "id", width: 170 },
  { title: t("console.coze.colWorkflowId"), dataIndex: "workflowId", key: "workflowId", width: 130 },
  { title: t("console.coze.colReleaseIdShort"), dataIndex: "releaseId", key: "releaseId", width: 130 },
  { title: t("console.coze.colRuntimeContextId"), dataIndex: "runtimeContextId", key: "runtimeContextId", width: 140 },
  { title: t("console.runtimeExec.colStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("console.runtimeExec.colStartedAt"), dataIndex: "startedAt", key: "startedAt", width: 190 },
  { title: t("console.runtimeExec.colActions"), key: "actions", width: 120 }
]);

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }
  return new Date(value).toLocaleString();
}

async function loadMappings() {
  loadingMappings.value = true;
  try {
    const overview  = await getCozeLayerMappingsOverview();

    if (!isMounted.value) return;
    mappingRows.value = overview.layers;
  } catch (error) {
    message.error((error as Error).message || t("console.coze.loadMappingsFailed"));
  } finally {
    loadingMappings.value = false;
  }
}

async function loadMetadata() {
  loadingMetadata.value = true;
  try {
    metadata.value = await getDebugLayerEmbedMetadata();

    if (!isMounted.value) return;
    if (canViewRuntimeExecutions.value) {
      await loadRuntimeExecutions();

      if (!isMounted.value) return;
    }
  } catch (error) {
    message.error((error as Error).message || t("console.coze.loadMetaFailed"));
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
    const result  = await getRuntimeExecutionsPaged({
      pageIndex: 1,
      pageSize: 50,
      keyword: runtimeKeyword.value || undefined
    });

    if (!isMounted.value) return;
    runtimeRows.value = result.items;
  } catch (error) {
    message.error((error as Error).message || t("console.coze.loadExecFailed"));
  } finally {
    loadingRuntimeExecutions.value = false;
  }
}

async function loadAuditTrails() {
  if (!canViewRuntimeAudits.value) {
    message.warning(t("console.coze.warnAuditPerm"));
    return;
  }
  if (!executionId.value.trim()) {
    message.warning(t("console.coze.warnExecutionId"));
    return;
  }

  loadingAudits.value = true;
  try {
    const result  = await getRuntimeExecutionAuditTrails(executionId.value.trim(), {
      pageIndex: 1,
      pageSize: 20,
      keyword: auditKeyword.value || undefined
    });

    if (!isMounted.value) return;
    auditRows.value = result.items;
  } catch (error) {
    message.error((error as Error).message || t("console.coze.loadAuditFailed"));
  } finally {
    loadingAudits.value = false;
  }
}

async function traceExecution(id: string) {
  executionId.value = id;
  await loadAuditTrails();

  if (!isMounted.value) return;
}

onMounted(async () => {
  if (typeof route.query.releaseId === "string" && route.query.releaseId.trim()) {
    releaseFilter.value = route.query.releaseId.trim();
  }
  if (typeof route.query.executionId === "string" && route.query.executionId.trim()) {
    executionId.value = route.query.executionId.trim();
  }

  await Promise.all([loadMappings(), loadMetadata()]);


  if (!isMounted.value) return;
  if (executionId.value) {
    await loadAuditTrails();

    if (!isMounted.value) return;
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
