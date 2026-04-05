<template>
  <div class="flow-debug-panel">
    <a-row :gutter="12">
      <a-col :xs="24" :lg="14">
        <div class="sub-title">{{ t("logicFlow.designerUi.debug.logTitle") }}</div>
        <a-table
          :columns="logColumns"
          :data-source="logRows"
          size="small"
          row-key="id"
          :pagination="{ pageSize: 6, size: 'small' }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'level'">
              <a-tag :color="levelColor(record.level)">{{ record.level }}</a-tag>
            </template>
          </template>
        </a-table>
      </a-col>
      <a-col :xs="24" :lg="10">
        <div class="sub-title">{{ t("logicFlow.designerUi.debug.watchTitle") }}</div>
        <a-table :columns="watchColumns" :data-source="watchRows" size="small" row-key="name" :pagination="false" />
        <div class="sub-title breakpoints">{{ t("logicFlow.designerUi.debug.breakpointsTitle") }}</div>
        <a-space wrap>
          <a-tag v-for="bp in breakpoints" :key="bp" closable @close="removeBreakpoint(bp)">{{ bp }}</a-tag>
          <a-button size="small" type="dashed" @click="addBreakpoint">{{ t("logicFlow.designerUi.debug.addBreakpoint") }}</a-button>
        </a-space>
        <div class="sub-title test-input">{{ t("logicFlow.designerUi.debug.testInputTitle") }}</div>
        <a-textarea v-model:value="testJson" :rows="6" class="json-editor" :placeholder="t('logicFlow.designerUi.debug.testInputPlaceholder')" />
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import type { TableColumnType } from "ant-design-vue";
import { ref } from "vue";
import { useI18n } from "vue-i18n";

export interface DebugLogRow {
  id: string;
  time: string;
  level: "info" | "warn" | "error";
  message: string;
}

export interface WatchRow {
  name: string;
  value: string;
}

const props = withDefaults(
  defineProps<{
    logRows?: DebugLogRow[];
    watchRows?: WatchRow[];
  }>(),
  {
    logRows: () => [
      { id: "1", time: "10:00:01", level: "info", message: "Flow started" },
      { id: "2", time: "10:00:02", level: "warn", message: "Optional input missing" }
    ],
    watchRows: () => [
      { name: "ctx.userId", value: "—" },
      { name: "ctx.tenantId", value: "—" }
    ]
  }
);

const { t } = useI18n();

const logColumns: TableColumnType[] = [
  { title: t("logicFlow.designerUi.debug.colTime"), dataIndex: "time", key: "time", width: 100 },
  { title: t("logicFlow.designerUi.debug.colLevel"), key: "level", width: 90 },
  { title: t("logicFlow.designerUi.debug.colMessage"), dataIndex: "message", key: "message" }
];

const watchColumns: TableColumnType[] = [
  { title: t("logicFlow.designerUi.debug.colName"), dataIndex: "name", key: "name" },
  { title: t("logicFlow.designerUi.debug.colValue"), dataIndex: "value", key: "value" }
];

const breakpoints = ref<string[]>(["node-start"]);
const testJson = ref('{\n  "sample": true\n}');

function levelColor(level: DebugLogRow["level"]): string {
  if (level === "error") {
    return "red";
  }
  if (level === "warn") {
    return "gold";
  }
  return "blue";
}

let bpSeq = 0;
function addBreakpoint(): void {
  bpSeq += 1;
  breakpoints.value = [...breakpoints.value, `node-${bpSeq}`];
}

function removeBreakpoint(bp: string): void {
  breakpoints.value = breakpoints.value.filter((x) => x !== bp);
}

</script>

<style scoped>
.flow-debug-panel {
  padding: 12px;
  background: #fff;
  border-top: 1px solid #f0f0f0;
}

.sub-title {
  font-weight: 600;
  margin-bottom: 8px;
}

.breakpoints,
.test-input {
  margin-top: 16px;
}

.json-editor {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
  font-size: 12px;
}
</style>
