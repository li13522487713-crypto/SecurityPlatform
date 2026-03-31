<template>
  <div class="view-designer-canvas">
    <div class="toolbar">
      <a-space>
        <a-input v-model:value="localDefinition.name" :placeholder="t('dynamicDesigner.viewName')" style="width: 220px" />
        <a-input v-model:value="localDefinition.viewKey" :placeholder="t('dynamicDesigner.viewKey')" style="width: 220px" />
        <a-button type="primary" @click="saveDraft">{{ t("dynamicDesigner.saveDraft") }}</a-button>
        <a-button @click="preview">{{ t("dynamicDesigner.previewData") }}</a-button>
        <a-button @click="showSql">{{ t("dynamicDesigner.previewSql") }}</a-button>
        <a-button type="primary" ghost @click="openPublishModal">{{ t("dynamicDesigner.publish") }}</a-button>
        <a-button @click="openHistory">{{ t("dynamicDesigner.history") }}</a-button>
        <a-button @click="openPhysicalPublishModal">{{ t("dynamicDesigner.publishPhysical", "发布物理VIEW") }}</a-button>
        <a-button @click="openExternalExtractModal">{{ t("dynamicDesigner.externalExtract", "外部抽取") }}</a-button>
        <a-popconfirm
          :title="t('dynamicDesigner.deleteConfirm', '确认删除当前视图？')"
          @confirm="deleteCurrentView"
        >
          <a-button danger>{{ t("common.delete", "删除") }}</a-button>
        </a-popconfirm>
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
          <FieldMappingPanel v-model:mappings="localDefinition.outputFields" />
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
          <template v-else-if="column.key === 'diffSummary'">
            {{ record.comment || "-" }}
          </template>
        </template>
      </a-table>
    </a-modal>

    <a-modal v-model:open="previewOpen" :title="t('dynamicDesigner.previewData')" :footer="null" width="1000">
      <a-alert
        style="margin-bottom: 8px"
        type="info"
        show-icon
        :message="previewPushdownMessage"
      />
      <a-table :data-source="previewRows" :columns="previewColumns" row-key="id" size="small" :pagination="{ pageSize: 10 }" />
    </a-modal>

    <a-modal v-model:open="sqlPreviewOpen" :title="t('dynamicDesigner.previewSql')" :footer="null" width="980">
      <a-alert v-if="sqlPreview.warnings.length > 0" type="warning" show-icon :message="sqlPreview.warnings.join('；')" style="margin-bottom: 8px" />
      <pre class="sql-block">{{ sqlPreview.sql }}</pre>
    </a-modal>

    <a-modal v-model:open="publishModalOpen" :title="t('dynamicDesigner.publish')" @ok="publishWithComment">
      <a-textarea v-model:value="publishComment" :rows="4" :placeholder="t('common.description', '备注')" />
    </a-modal>

    <a-modal v-model:open="physicalPublishOpen" :title="t('dynamicDesigner.publishPhysical', '发布物理VIEW')" @ok="publishPhysical">
      <a-form layout="vertical">
        <a-form-item :label="t('dynamicDesigner.physicalViewName', '物理视图名')">
          <a-input v-model:value="physicalForm.physicalViewName" />
        </a-form-item>
        <a-form-item :label="t('dynamicDesigner.physicalDataSource', '目标数据源')">
          <a-select
            v-model:value="physicalForm.dataSourceId"
            :options="externalDataSourceOptions"
            allow-clear
            :placeholder="t('dynamicDesigner.physicalDataSource', '目标数据源')"
          />
        </a-form-item>
        <a-form-item :label="t('common.description', '备注')">
          <a-textarea v-model:value="physicalForm.comment" :rows="3" />
        </a-form-item>
      </a-form>
      <a-divider />
      <a-table :data-source="physicalPublications" :columns="physicalColumns" row-key="id" size="small" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'actions'">
            <a-space :size="8">
              <a-button type="link" size="small" @click="rollbackPhysical(record.version)">{{ t("dynamicDesigner.rollback", "回滚") }}</a-button>
              <a-popconfirm :title="t('common.delete', '删除')" @confirm="deletePhysical(record.id)">
                <a-button type="link" size="small" danger>{{ t("common.delete", "删除") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-modal>

    <a-modal v-model:open="externalExtractOpen" :title="t('dynamicDesigner.externalExtract', '外部抽取')" :footer="null" width="980">
      <a-form layout="inline">
        <a-form-item :label="t('dynamicDesigner.externalDataSource', '数据源')">
          <a-select
            v-model:value="externalExtractDataSourceId"
            :options="externalDataSourceOptions"
            style="width: 280px"
            @change="loadExternalSchema"
          />
        </a-form-item>
        <a-form-item :label="t('dynamicDesigner.externalSql', 'SQL')">
          <a-input v-model:value="externalSql" style="width: 420px" />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="previewExternal">{{ t("dynamicDesigner.previewData", "预览数据") }}</a-button>
        </a-form-item>
      </a-form>
      <a-divider />
      <a-space direction="vertical" style="width: 100%">
        <a-card size="small" :title="t('dynamicDesigner.externalSchema', 'Schema')">
          <a-table :data-source="externalSchemaRows" :columns="externalSchemaColumns" size="small" row-key="name" :pagination="false" />
        </a-card>
        <a-card size="small" :title="t('dynamicDesigner.previewData', '预览数据')">
          <a-table :data-source="externalPreviewRows" :columns="externalPreviewColumns" size="small" row-key="__index" :pagination="{ pageSize: 10 }" />
        </a-card>
      </a-space>
    </a-modal>

    <a-modal v-model:open="deleteBlockerOpen" :title="t('dynamic.deleteBlockedTitle', '删除被阻断')" :footer="null" width="760">
      <a-table :pagination="false" :data-source="deleteBlockers" :columns="deleteBlockerColumns" row-key="id" size="small" />
      <a-alert v-if="deleteWarnings.length > 0" style="margin-top: 8px" type="warning" show-icon :message="deleteWarnings.join('；')" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import FieldMappingPanel from "@/components/designer/FieldMappingPanel.vue";
import OutputSchemaPanel from "@/components/designer/OutputSchemaPanel.vue";
import type {
  DataViewDefinition,
  DynamicPhysicalViewPublication,
  DynamicViewHistoryItem,
  DeleteCheckBlocker,
  DynamicViewSqlPreviewResult
} from "@/types/dynamic-dataflow";
import {
  createDynamicView,
  deleteDynamicView,
  getDynamicView,
  getDynamicViewDeleteCheck,
  getDynamicViewHistory,
  getExternalExtractSchema,
  listExternalExtractDataSources,
  listPhysicalViewPublications,
  deletePhysicalViewPublication,
  previewDynamicView,
  previewExternalExtract,
  previewDynamicViewSql,
  publishDynamicView,
  publishPhysicalView,
  rollbackPhysicalViewPublication,
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
const sqlPreviewOpen = ref(false);
const publishModalOpen = ref(false);
const publishComment = ref("");
const deleteBlockerOpen = ref(false);
const deleteBlockers = ref<DeleteCheckBlocker[]>([]);
const deleteWarnings = ref<string[]>([]);
const physicalPublishOpen = ref(false);
const externalExtractOpen = ref(false);
const externalExtractDataSourceId = ref<number>();
const externalSql = ref("SELECT * FROM sample LIMIT 100");
const externalSchemaRows = ref<Array<{ name: string; columnsText: string }>>([]);
const externalPreviewRows = ref<Array<Record<string, unknown>>>([]);
const physicalPublications = ref<DynamicPhysicalViewPublication[]>([]);
const externalDataSourceOptions = ref<Array<{ label: string; value: number }>>([]);
const physicalForm = reactive({
  physicalViewName: "",
  dataSourceId: undefined as number | undefined,
  comment: ""
});
const previewPushdownMessage = ref("SQL 可下推比例：100%");
const sqlPreview = ref<DynamicViewSqlPreviewResult>({
  sql: "",
  parameters: [],
  warnings: [],
  fullyPushdown: true
});

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
  { title: t("dynamicDesigner.pipeline", "变更摘要"), key: "diffSummary" },
  { title: t("dynamicDesigner.checksum"), dataIndex: "checksum", key: "checksum" },
  { title: t("common.actions"), key: "action", width: 120 }
]);

const deleteBlockerColumns = computed(() => [
  { title: t("dynamic.blockerType", "类型"), dataIndex: "type", key: "type", width: 120 },
  { title: t("dynamic.blockerName", "名称"), dataIndex: "name", key: "name" },
  { title: t("dynamic.blockerPath", "路径"), dataIndex: "path", key: "path" }
]);

const externalSchemaColumns = computed(() => [
  { title: t("common.name", "名称"), dataIndex: "name", key: "name", width: 220 },
  { title: t("dynamicDesigner.columns", "字段"), dataIndex: "columnsText", key: "columnsText" }
]);

const externalPreviewColumns = computed(() => {
  const first = externalPreviewRows.value[0];
  if (!first) {
    return [];
  }

  return Object.keys(first).map(key => ({
    title: key,
    dataIndex: key,
    key
  }));
});

const physicalColumns = computed(() => [
  { title: "ID", dataIndex: "id", key: "id", width: 180 },
  { title: t("dynamicDesigner.version", "版本"), dataIndex: "version", key: "version", width: 90 },
  { title: t("dynamicDesigner.physicalViewName", "物理视图名"), dataIndex: "physicalViewName", key: "physicalViewName", width: 180 },
  { title: t("dynamicDesigner.status", "状态"), dataIndex: "status", key: "status", width: 120 },
  { title: t("dynamicDesigner.createdAt", "创建时间"), dataIndex: "publishedAt", key: "publishedAt" },
  { title: t("common.actions"), key: "actions", width: 130 }
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

async function ensureExternalDataSources() {
  const dataSources = await listExternalExtractDataSources();
  externalDataSourceOptions.value = dataSources.map(item => ({
    label: `${item.name} (${item.dbType})`,
    value: Number(item.id)
  }));
}

async function preview() {
  const result = await previewDynamicView({
    definition: { ...localDefinition, appId: props.appId },
    limit: 100
  });
  const sqlResult = await previewDynamicViewSql({ ...localDefinition, appId: props.appId });
  previewPushdownMessage.value = sqlResult.fullyPushdown
    ? "SQL 可下推比例：100%"
    : `SQL 可下推比例：部分下推（${sqlResult.warnings.length} 条补偿警告）`;
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
  void loadSqlPreview();
}

function openPublishModal() {
  publishComment.value = "";
  publishModalOpen.value = true;
}

async function publishWithComment() {
  if (!localDefinition.viewKey) {
    message.warning(t("dynamicDesigner.viewKeyRequired"));
    return;
  }
  await publishDynamicView(localDefinition.viewKey, publishComment.value.trim() || undefined);
  message.success(t("dynamicDesigner.publishSuccess"));
  publishModalOpen.value = false;
}

async function openHistory() {
  if (!localDefinition.viewKey) {
    message.warning(t("dynamicDesigner.viewKeyRequired"));
    return;
  }
  historyItems.value = await getDynamicViewHistory(localDefinition.viewKey);
  historyOpen.value = true;
}

async function openPhysicalPublishModal() {
  if (!localDefinition.viewKey) {
    message.warning(t("dynamicDesigner.viewKeyRequired"));
    return;
  }

  await ensureExternalDataSources();
  const rows = await listPhysicalViewPublications(localDefinition.viewKey);
  physicalPublications.value = rows;
  physicalPublishOpen.value = true;
}

async function publishPhysical() {
  if (!localDefinition.viewKey) {
    return;
  }

  const result = await publishPhysicalView(localDefinition.viewKey, {
    replaceIfExists: true,
    physicalViewName: physicalForm.physicalViewName || undefined,
    dataSourceId: physicalForm.dataSourceId ?? null,
    comment: physicalForm.comment || undefined
  });
  message.success(result.message);
  const rows = await listPhysicalViewPublications(localDefinition.viewKey);
  physicalPublications.value = rows;
}

async function rollbackPhysical(version: number) {
  if (!localDefinition.viewKey) {
    return;
  }
  const result = await rollbackPhysicalViewPublication(localDefinition.viewKey, version);
  message.success(result.message);
  const rows = await listPhysicalViewPublications(localDefinition.viewKey);
  physicalPublications.value = rows;
}

async function deletePhysical(publicationId: string) {
  if (!localDefinition.viewKey) {
    return;
  }
  await deletePhysicalViewPublication(localDefinition.viewKey, publicationId);
  message.success(t("common.deleteSuccess", "删除成功"));
  const rows = await listPhysicalViewPublications(localDefinition.viewKey);
  physicalPublications.value = rows;
}

async function openExternalExtractModal() {
  await ensureExternalDataSources();
  externalExtractOpen.value = true;
}

async function loadExternalSchema() {
  if (!externalExtractDataSourceId.value) {
    externalSchemaRows.value = [];
    return;
  }

  const schema = await getExternalExtractSchema(externalExtractDataSourceId.value.toString());
  externalSchemaRows.value = schema.tables.map(table => ({
    name: table.name,
    columnsText: table.columns.map(col => `${col.name}:${col.type}`).join(", ")
  }));
}

async function previewExternal() {
  if (!externalExtractDataSourceId.value) {
    message.warning(t("validation.required", "请完善必填项"));
    return;
  }
  const result = await previewExternalExtract({
    dataSourceId: Number(externalExtractDataSourceId.value),
    sql: externalSql.value,
    limit: 100
  });
  externalPreviewRows.value = result.rows.map((row, index) => ({ __index: index + 1, ...row }));
}

async function rollback(version: number) {
  if (!localDefinition.viewKey) {
    return;
  }
  await rollbackDynamicView(localDefinition.viewKey, version);
  message.success(t("dynamicDesigner.rollbackSuccess"));
  historyItems.value = await getDynamicViewHistory(localDefinition.viewKey);
}

async function loadSqlPreview() {
  sqlPreview.value = await previewDynamicViewSql({ ...localDefinition, appId: props.appId });
  previewPushdownMessage.value = sqlPreview.value.fullyPushdown
    ? "SQL 可下推比例：100%"
    : `SQL 可下推比例：部分下推（${sqlPreview.value.warnings.length} 条补偿警告）`;
  sqlPreviewOpen.value = true;
}

async function deleteCurrentView() {
  if (!localDefinition.viewKey) {
    message.warning(t("dynamicDesigner.viewKeyRequired"));
    return;
  }
  const check = await getDynamicViewDeleteCheck(localDefinition.viewKey);
  if (!check.canDelete) {
    deleteBlockers.value = check.blockers;
    deleteWarnings.value = check.warnings;
    deleteBlockerOpen.value = true;
    return;
  }
  await deleteDynamicView(localDefinition.viewKey);
  message.success(t("common.deleteSuccess", "删除成功"));
  Object.assign(localDefinition, defaultDefinition());
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

.sql-block {
  background: #101522;
  color: #c9d1d9;
  border-radius: 6px;
  padding: 12px;
  max-height: 540px;
  overflow: auto;
  font-family: Consolas, "Courier New", monospace;
  font-size: 12px;
}
</style>
