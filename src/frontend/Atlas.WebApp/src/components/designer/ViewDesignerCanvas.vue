<template>
  <div class="view-designer-canvas">
    <div class="toolbar">
      <a-space>
        <a-input v-model:value="localDefinition.name" :placeholder="t('dynamicDesigner.viewName')" style="width: 220px" />
        <a-input v-model:value="localDefinition.viewKey" :placeholder="t('dynamicDesigner.viewKey')" style="width: 220px" />
        <a-button type="primary" @click="saveDraft">{{ t("dynamicDesigner.saveDraft") }}</a-button>
        <a-button @click="preview">{{ t("dynamicDesigner.previewData") }}</a-button>
        <a-button @click="showSql">{{ t("dynamicDesigner.previewSql") }}</a-button>
        <a-button type="primary" ghost @click="publish">{{ t("dynamicDesigner.publish") }}</a-button>
        <a-button @click="openHistory">{{ t("dynamicDesigner.history") }}</a-button>
      </a-space>
    </div>

    <div class="layout">
      <div class="left">
        <a-card size="small" :title="t('dynamicDesigner.nodePalette')">
          <a-tag v-for="node in nodeTypes" :key="node" style="margin-bottom: 8px">{{ node }}</a-tag>
        </a-card>
      </div>
      <div class="center">
        <a-card size="small" :title="t('dynamicDesigner.canvas')">
          <a-empty :description="t('dynamicDesigner.canvasPlaceholder')" />
        </a-card>
      </div>
      <div class="right">
        <a-card size="small" :title="t('dynamicDesigner.fieldMapping')">
          <FieldMappingPanel :mappings="localDefinition.outputFields" />
        </a-card>
        <a-card size="small" :title="t('dynamicDesigner.outputSchema')" style="margin-top: 8px">
          <OutputSchemaPanel :mappings="localDefinition.outputFields" />
        </a-card>
      </div>
    </div>

    <a-modal v-model:open="historyOpen" :title="t('dynamicDesigner.history')" :footer="null" width="860">
      <a-table :pagination="false" :data-source="historyItems" :columns="historyColumns" row-key="version">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <a-button type="link" @click="rollback(record.version)">{{ t('dynamicDesigner.rollback') }}</a-button>
          </template>
        </template>
      </a-table>
    </a-modal>

    <a-modal v-model:open="previewOpen" :title="t('dynamicDesigner.previewData')" :footer="null" width="1000">
      <a-table :data-source="previewRows" :columns="previewColumns" row-key="id" size="small" :pagination="{ pageSize: 10 }" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import FieldMappingPanel from "@/components/designer/FieldMappingPanel.vue";
import OutputSchemaPanel from "@/components/designer/OutputSchemaPanel.vue";
import type { DataViewDefinition, DynamicViewHistoryItem } from "@/types/dynamic-dataflow";
import {
  createDynamicView,
  getDynamicView,
  getDynamicViewHistory,
  previewDynamicView,
  publishDynamicView,
  rollbackDynamicView,
  updateDynamicView
} from "@/services/dynamic-views";

const props = defineProps<{
  appId: string;
  viewKey?: string;
}>();

const { t } = useI18n();

const defaultDefinition = (): DataViewDefinition => ({
  appId: props.appId,
  viewKey: "",
  name: "",
  description: "",
  nodes: [],
  edges: [],
  outputFields: []
});

const localDefinition = reactive<DataViewDefinition>(defaultDefinition());
const historyItems = ref<DynamicViewHistoryItem[]>([]);
const historyOpen = ref(false);
const previewOpen = ref(false);
const previewRows = ref<Array<Record<string, unknown>>>([]);

const nodeTypes = [
  "sourceTable",
  "sourceView",
  "join",
  "select",
  "filter",
  "compute",
  "cast",
  "lookup",
  "aggregate",
  "union",
  "sort",
  "limit",
  "outputView"
];

const historyColumns = computed(() => [
  { title: t("dynamicDesigner.version"), dataIndex: "version", key: "version", width: 100 },
  { title: t("dynamicDesigner.status"), dataIndex: "status", key: "status", width: 120 },
  { title: t("dynamicDesigner.createdAt"), dataIndex: "createdAt", key: "createdAt" },
  { title: t("dynamicDesigner.checksum"), dataIndex: "checksum", key: "checksum" },
  { title: t("common.actions"), key: "action", width: 120 }
]);

const previewColumns = computed(() => {
  const first = previewRows.value[0];
  if (!first) {
    return [{ title: "id", dataIndex: "id", key: "id" }];
  }

  return Object.keys(first).map(key => ({
    title: key,
    dataIndex: key,
    key
  }));
});

watch(
  () => props.viewKey,
  async next => {
    Object.assign(localDefinition, defaultDefinition());
    if (!next) {
      return;
    }
    const detail = await getDynamicView(next);
    if (detail) {
      Object.assign(localDefinition, detail);
    }
  },
  { immediate: true }
);

async function saveDraft() {
  if (!localDefinition.viewKey) {
    message.warning(t("dynamicDesigner.viewKeyRequired"));
    return;
  }

  if (!localDefinition.name) {
    message.warning(t("dynamicDesigner.viewNameRequired"));
    return;
  }

  const payload: DataViewDefinition = {
    ...localDefinition,
    appId: props.appId
  };

  const exists = await getDynamicView(payload.viewKey);
  if (exists) {
    await updateDynamicView(payload.viewKey, payload);
  } else {
    await createDynamicView(payload);
  }

  message.success(t("common.saveSuccess"));
}

async function preview() {
  const result = await previewDynamicView({
    definition: { ...localDefinition, appId: props.appId },
    limit: 100
  });
  previewRows.value = result.items.map(item => {
    const row: Record<string, unknown> = { id: item.id };
    for (const value of item.values) {
      row[value.field] =
        value.stringValue ?? value.intValue ?? value.longValue ?? value.decimalValue ?? value.boolValue ?? value.dateTimeValue ?? value.dateValue ?? null;
    }
    return row;
  });
  previewOpen.value = true;
}

function showSql() {
  message.info(t("dynamicDesigner.sqlPreviewNotReady"));
}

async function publish() {
  if (!localDefinition.viewKey) {
    message.warning(t("dynamicDesigner.viewKeyRequired"));
    return;
  }
  await publishDynamicView(localDefinition.viewKey);
  message.success(t("dynamicDesigner.publishSuccess"));
}

async function openHistory() {
  if (!localDefinition.viewKey) {
    message.warning(t("dynamicDesigner.viewKeyRequired"));
    return;
  }
  historyItems.value = await getDynamicViewHistory(localDefinition.viewKey);
  historyOpen.value = true;
}

async function rollback(version: number) {
  if (!localDefinition.viewKey) {
    return;
  }
  await rollbackDynamicView(localDefinition.viewKey, version);
  message.success(t("dynamicDesigner.rollbackSuccess"));
  historyItems.value = await getDynamicViewHistory(localDefinition.viewKey);
}
</script>

<style scoped>
.view-designer-canvas {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.layout {
  display: grid;
  grid-template-columns: 240px 1fr 360px;
  gap: 8px;
}

@media (max-width: 1200px) {
  .layout {
    grid-template-columns: 1fr;
  }
}
</style>
