<template>
  <a-drawer
    v-model:open="visible"
    :title="t('datasource.advancedPreviewTitle', { name: dataSource?.name || '' })"
    :width="previewDrawerWidth"
    placement="right"
    @close="handleClose"
  >
    <div class="preview-layout">
      <div class="schema-sidebar">
        <div class="schema-header">
          <span class="schema-title">{{ t("datasource.schemaTitle") }}</span>
          <a-button size="small" :loading="schemaLoading" @click="loadSchema">
            <template #icon><reload-outlined /></template>
          </a-button>
        </div>

        <a-input
          v-model:value="schemaFilter"
          size="small"
          :placeholder="t('datasource.filterTables')"
          allow-clear
          class="schema-filter"
        />

        <div class="schema-tree">
          <a-tree
            v-if="filteredTables.length > 0"
            :tree-data="schemaTreeData"
            :default-expand-all="false"
            :selectable="false"
            block-node
          >
            <template #title="{ title, isTable, tableName }">
              <span
                v-if="isTable"
                class="schema-table-name"
                :title="t('datasource.dblClickInsert')"
                @dblclick="insertTableName(tableName)"
              >
                <table-outlined class="schema-icon" /> {{ title }}
              </span>
              <span v-else class="schema-column-name">{{ title }}</span>
            </template>
          </a-tree>
          <a-empty v-else :description="t('datasource.noTables')" />
        </div>
      </div>

      <div class="editor-shell">
        <div class="editor-main">
          <a-card size="small" :title="t('datasource.sqlExecutor')">
            <template #extra>
              <a-space>
                <a-dropdown v-if="sqlHistory.length > 0">
                  <a-button size="small">
                    <template #icon><history-outlined /></template>
                    {{ t("datasource.history") }}
                  </a-button>
                  <template #overlay>
                    <a-menu @click="handleHistorySelect">
                      <a-menu-item v-for="(item, index) in sqlHistory" :key="index">
                        <div class="history-item">
                          <code class="history-sql">{{ summarizeSql(item.sql) }}</code>
                          <span class="history-time">{{ item.timeMs }}ms</span>
                        </div>
                      </a-menu-item>
                    </a-menu>
                  </template>
                </a-dropdown>
                <a-button type="primary" :loading="loading" @click="handleExecute">
                  <template #icon><play-circle-outlined /></template>
                  {{ t("datasource.execute") }} (Ctrl+Enter)
                </a-button>
              </a-space>
            </template>
            <sql-editor v-model="sql" @execute="handleExecute" />
          </a-card>

          <a-card size="small" :title="resultTitle" class="result-card">
            <a-alert v-if="errorMsg" type="error" :message="errorMsg" show-icon class="result-alert" />
            <data-preview-table :columns="resultColumns" :data="resultData" :loading="loading" />
          </a-card>
        </div>
      </div>
    </div>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from "vue";
import { HistoryOutlined, PlayCircleOutlined, ReloadOutlined, TableOutlined } from "@ant-design/icons-vue";
import { message } from "ant-design-vue";
import type {
  DataSourceTableInfo,
  SqlQueryResultColumn,
  TenantDataSourceDto
} from "@atlas/shared-core";
import { useI18n } from "vue-i18n";
import { getTenantDataSourceSchema, previewTenantDataSourceQuery } from "@/services/api-datasource";
import DataPreviewTable from "./components/DataPreviewTable.vue";
import SqlEditor from "./components/SqlEditor.vue";

interface HistoryEntry {
  sql: string;
  timeMs: number;
  timestamp: number;
}

const previewDrawerWidth = "min(1280px, 92vw)";
const maxHistory = 20;

const { t } = useI18n();

const visible = ref(false);
const dataSource = ref<TenantDataSourceDto | null>(null);
const sql = ref("SELECT * FROM ");
const loading = ref(false);
const errorMsg = ref("");
const resultColumns = ref<SqlQueryResultColumn[]>([]);
const resultData = ref<Record<string, unknown>[]>([]);
const executionTimeMs = ref(0);

const schemaLoading = ref(false);
const schemaTables = ref<DataSourceTableInfo[]>([]);
const schemaFilter = ref("");
const sqlHistory = ref<HistoryEntry[]>([]);

const resultTitle = computed(() => {
  if (executionTimeMs.value > 0) {
    return t("datasource.resultWithStats", {
      time: executionTimeMs.value,
      count: resultData.value.length
    });
  }
  return t("datasource.result");
});

const filteredTables = computed(() => {
  const filter = schemaFilter.value.trim().toLowerCase();
  if (!filter) {
    return schemaTables.value;
  }

  return schemaTables.value.filter((table) => table.name.toLowerCase().includes(filter));
});

const schemaTreeData = computed(() =>
  filteredTables.value.map((table) => ({
    key: `table-${table.name}`,
    title: `${table.name} (${table.columns.length})`,
    isTable: true,
    tableName: table.name,
    children: table.columns.map((column) => ({
      key: `column-${table.name}-${column.name}`,
      title: `${column.name}  ${column.dataType}${column.isPrimaryKey ? " PK" : ""}${column.isNullable ? "" : " NOT NULL"}`,
      isLeaf: true,
      isTable: false,
      tableName: ""
    }))
  }))
);

function summarizeSql(sqlText: string) {
  if (sqlText.length <= 60) {
    return sqlText;
  }
  return `${sqlText.substring(0, 60)}...`;
}

function insertTableName(name: string) {
  sql.value += `${name} `;
}

async function loadSchema() {
  if (!dataSource.value?.id) {
    return;
  }

  schemaLoading.value = true;
  try {
    const result = await getTenantDataSourceSchema(dataSource.value.id);
    if (result.success) {
      schemaTables.value = result.tables;
      return;
    }
    message.error(result.errorMessage || t("datasource.schemaFailed"));
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("datasource.schemaFailed"));
  } finally {
    schemaLoading.value = false;
  }
}

async function handleExecute() {
  if (!dataSource.value?.id) {
    return;
  }

  if (!sql.value.trim()) {
    message.warning(t("datasource.sqlRequired"));
    return;
  }

  loading.value = true;
  errorMsg.value = "";
  try {
    const result = await previewTenantDataSourceQuery(dataSource.value.id, { sql: sql.value });
    if (result.success) {
      resultColumns.value = result.columns;
      resultData.value = result.data;
      executionTimeMs.value = result.executionTimeMs;
      pushHistory(sql.value, result.executionTimeMs);
      message.success(t("datasource.executeSuccess"));
      return;
    }

    errorMsg.value = result.errorMessage || t("datasource.executeFailed");
    resultColumns.value = [];
    resultData.value = [];
  } catch (error) {
    errorMsg.value = error instanceof Error ? error.message : t("datasource.executeFailed");
  } finally {
    loading.value = false;
  }
}

function handleHistorySelect({ key }: { key: string }) {
  const index = Number(key);
  if (index >= 0 && index < sqlHistory.value.length) {
    sql.value = sqlHistory.value[index].sql;
  }
}

function pushHistory(sqlText: string, timeMs: number) {
  sqlHistory.value.unshift({
    sql: sqlText,
    timeMs,
    timestamp: Date.now()
  });

  if (sqlHistory.value.length > maxHistory) {
    sqlHistory.value.length = maxHistory;
  }
}

function open(record: TenantDataSourceDto) {
  dataSource.value = record;
  sql.value = "SELECT * FROM ";
  errorMsg.value = "";
  resultColumns.value = [];
  resultData.value = [];
  executionTimeMs.value = 0;
  schemaTables.value = [];
  schemaFilter.value = "";
  visible.value = true;
  void nextTick(() => loadSchema());
}

function handleClose() {
  visible.value = false;
}

defineExpose({ open });
</script>

<style scoped>
.preview-layout {
  display: flex;
  gap: 16px;
  height: calc(100vh - 100px);
}

.schema-sidebar {
  width: 280px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  background: #fafafa;
}

.schema-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-bottom: 1px solid #f0f0f0;
}

.schema-title {
  font-weight: 600;
  font-size: 13px;
}

.schema-filter {
  margin: 8px;
}

.schema-tree {
  flex: 1;
  overflow: auto;
  padding: 0 4px 8px;
}

.schema-table-name {
  cursor: pointer;
  font-weight: 500;
}

.schema-column-name {
  font-size: 12px;
  color: #666;
  font-family: "Fira Code", Consolas, monospace;
}

.schema-icon {
  margin-right: 4px;
  color: #1677ff;
}

.editor-shell {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  overflow: hidden;
}

.editor-main {
  width: 100%;
  max-width: min(1080px, 100%);
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
}

.result-card {
  flex: 1;
  overflow: auto;
}

.result-alert {
  margin-bottom: 12px;
}

.history-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  max-width: 400px;
}

.history-sql {
  font-size: 12px;
  color: #333;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.history-time {
  font-size: 11px;
  color: #999;
  flex-shrink: 0;
}
</style>
