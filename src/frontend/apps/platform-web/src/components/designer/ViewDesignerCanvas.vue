<template>
  <div class="view-designer">
    <a-card size="small" :title="t('dynamicDesigner.centerTitle')">
      <template #extra>
        <a-space>
          <a-button @click="openHistory">{{ t("dynamicDesigner.history") }}</a-button>
          <a-button @click="showSql">{{ t("dynamicDesigner.previewSql") }}</a-button>
          <a-button type="primary" :loading="saving" @click="saveDraft">{{ t("dynamicDesigner.saveDraft") }}</a-button>
        </a-space>
      </template>

      <a-form layout="vertical">
        <a-row :gutter="16">
          <a-col :span="8">
            <a-form-item :label="t('dynamicDesigner.viewKey')">
              <a-input v-model:value="definition.viewKey" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('dynamicDesigner.viewName')">
              <a-input v-model:value="definition.name" />
            </a-form-item>
          </a-col>
          <a-col :span="8">
            <a-form-item :label="t('common.description')">
              <a-input v-model:value="definition.description" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
    </a-card>

    <a-modal v-model:open="sqlOpen" :title="t('dynamicDesigner.previewSql')" :footer="null" width="900">
      <a-alert
        v-if="sqlResult.warnings.length > 0"
        type="warning"
        show-icon
        :message="sqlResult.warnings.join('；')"
        style="margin-bottom: 8px"
      />
      <pre class="sql-preview">{{ sqlResult.sql }}</pre>
    </a-modal>

    <a-modal v-model:open="historyOpen" :title="t('dynamicDesigner.history')" :footer="null" width="760">
      <a-table :data-source="historyList" :columns="historyColumns" row-key="version" :pagination="false" size="small">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'actions'">
            <a-button type="link" size="small" @click="rollback(record.version)">
              {{ t("dynamicDesigner.rollback") }}
            </a-button>
          </template>
        </template>
      </a-table>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { DataViewDefinition, DynamicViewHistoryItem, DynamicViewSqlPreviewResult } from "@/types/dynamic-dataflow";
import {
  createDynamicView,
  getDynamicView,
  getDynamicViewHistory,
  previewDynamicViewSql,
  rollbackDynamicView,
  updateDynamicView,
} from "@/services/api-dynamic-views";

const props = defineProps<{
  appId: string;
  viewKey?: string;
}>();

const { t } = useI18n();
const saving = ref(false);
const sqlOpen = ref(false);
const historyOpen = ref(false);
const historyList = ref<DynamicViewHistoryItem[]>([]);
const sqlResult = ref<DynamicViewSqlPreviewResult>({
  sql: "",
  warnings: [],
  fullyPushdown: false,
});

const definition = reactive<DataViewDefinition>({
  appId: props.appId,
  viewKey: "",
  name: "",
  description: "",
  nodes: [],
  edges: [],
  outputFields: [],
});

const historyColumns = computed(() => [
  { title: t("dynamicDesigner.version"), dataIndex: "version", key: "version", width: 100 },
  { title: t("common.status"), dataIndex: "status", key: "status", width: 160 },
  { title: t("common.createdAt"), dataIndex: "createdAt", key: "createdAt" },
  { title: t("common.actions"), key: "actions", width: 90 },
]);

const loadDetail = async (viewKey: string | undefined) => {
  if (!viewKey) {
    return;
  }
  try {
    const detail = await getDynamicView(viewKey);
    if (!detail) {
      return;
    }
    definition.appId = detail.appId;
    definition.viewKey = detail.viewKey;
    definition.name = detail.name;
    definition.description = detail.description || "";
    definition.nodes = detail.nodes;
    definition.edges = detail.edges;
    definition.outputFields = detail.outputFields;
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.loadFailed"));
  }
};

const saveDraft = async () => {
  if (!definition.viewKey.trim() || !definition.name.trim()) {
    message.warning(t("validation.required"));
    return;
  }
  saving.value = true;
  try {
    if (props.viewKey) {
      await updateDynamicView(props.viewKey, definition);
    } else {
      await createDynamicView(definition);
    }
    message.success(t("common.save"));
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const showSql = async () => {
  try {
    sqlResult.value = await previewDynamicViewSql(definition);
    sqlOpen.value = true;
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.previewSqlFailed"));
  }
};

const openHistory = async () => {
  if (!definition.viewKey.trim()) {
    message.warning(t("validation.required"));
    return;
  }
  try {
    historyList.value = await getDynamicViewHistory(definition.viewKey);
    historyOpen.value = true;
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.historyLoadFailed"));
  }
};

const rollback = async (version: number) => {
  try {
    await rollbackDynamicView(definition.viewKey, version);
    historyOpen.value = false;
    message.success(t("dynamicDesigner.rollbackSuccess"));
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.rollbackFailed"));
  }
};

watch(
  () => props.viewKey,
  (next) => {
    void loadDetail(next);
  },
  { immediate: true }
);
</script>

<style scoped>
.view-designer {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.sql-preview {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
