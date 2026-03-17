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

    <a-card title="运行执行审计追溯" class="audit-card">
      <a-space wrap>
        <a-input v-model:value="executionId" placeholder="输入执行ID" style="width: 220px" />
        <a-input-search
          v-model:value="auditKeyword"
          placeholder="关键字（action/target/actor）"
          style="width: 260px"
          allow-clear
          @search="loadAuditTrails"
        />
        <a-button type="primary" @click="loadAuditTrails">查询</a-button>
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
import { onMounted, ref } from "vue";
import { message } from "ant-design-vue";
import type { TableColumnsType } from "ant-design-vue";
import {
  getCozeLayerMappingsOverview,
  getDebugLayerEmbedMetadata,
  getRuntimeExecutionAuditTrails
} from "@/services/api-coze-runtime";
import type {
  CozeLayerMappingItem,
  DebugLayerEmbedMetadata,
  RuntimeExecutionAuditTrailItem
} from "@/types/platform-v2";

const loadingMappings = ref(false);
const loadingMetadata = ref(false);
const loadingAudits = ref(false);
const mappingRows = ref<CozeLayerMappingItem[]>([]);
const metadata = ref<DebugLayerEmbedMetadata | null>(null);
const executionId = ref("");
const auditKeyword = ref("");
const auditRows = ref<RuntimeExecutionAuditTrailItem[]>([]);

const auditColumns: TableColumnsType<RuntimeExecutionAuditTrailItem> = [
  { title: "审计ID", dataIndex: "auditId", key: "auditId", width: 160 },
  { title: "操作人", dataIndex: "actor", key: "actor", width: 140 },
  { title: "动作", dataIndex: "action", key: "action", width: 180 },
  { title: "结果", dataIndex: "result", key: "result", width: 120 },
  { title: "目标", dataIndex: "target", key: "target", ellipsis: true },
  { title: "发生时间", dataIndex: "occurredAt", key: "occurredAt", width: 190 }
];

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
  } catch (error) {
    message.error((error as Error).message || "加载调试层元数据失败");
  } finally {
    loadingMetadata.value = false;
  }
}

async function loadAuditTrails() {
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

onMounted(() => {
  void loadMappings();
  void loadMetadata();
});
</script>

<style scoped>
.coze-debug-page {
  padding: 24px;
}

.audit-card {
  margin-top: 16px;
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
