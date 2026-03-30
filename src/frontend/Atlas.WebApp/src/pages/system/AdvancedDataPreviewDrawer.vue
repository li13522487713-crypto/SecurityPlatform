<template>
  <a-drawer
    v-model:open="visible"
    :title="t('datasource.advancedPreviewTitle', { name: dataSource?.name || '' })"
    :width="previewDrawerWidth"
    placement="right"
    @close="handleClose"
  >
    <div class="preview-layout">
      <!-- Schema Browser Sidebar -->
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

      <!-- Main：宽屏下限制可读宽度并居中，避免 SQL/结果横向拉得过开 -->
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
                    <a-menu-item v-for="(item, idx) in sqlHistory" :key="idx">
                      <div class="history-item">
                        <code class="history-sql">{{ item.sql.substring(0, 60) }}{{ item.sql.length > 60 ? "..." : "" }}</code>
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
          <data-preview-table
            :columns="resultColumns"
            :data="resultData"
            :loading="loading"
          />
        </a-card>
        </div>
      </div>
    </div>
  </a-drawer>
</template>

<script setup lang="ts">
import { ref, computed, nextTick } from "vue";
import {
  PlayCircleOutlined,
  ReloadOutlined,
  HistoryOutlined,
  TableOutlined
} from "@ant-design/icons-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type {
  TenantDataSourceDto,
  SqlQueryResultColumn,
  DataSourceTableInfo
} from "@/types/api";
import {
  previewTenantDataSourceQuery,
  getTenantDataSourceSchema
} from "@/services/api-system";
import SqlEditor from "./components/SqlEditor.vue";
import DataPreviewTable from "./components/DataPreviewTable.vue";

/** 抽屉整体宽度：超宽屏不再按 90vw 拉满，避免整块预览过扁过长 */
const previewDrawerWidth = "min(1280px, 92vw)";

interface HistoryEntry {
  sql: string;
  timeMs: number;
  timestamp: number;
}

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

const MAX_HISTORY = 20;

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
  const filter = schemaFilter.value.toLowerCase();
  if (!filter) return schemaTables.value;
  return schemaTables.value.filter((tbl) =>
    tbl.name.toLowerCase().includes(filter)
  );
});

const schemaTreeData = computed(() =>
  filteredTables.value.map((tbl) => ({
    key: `table-${tbl.name}`,
    title: `${tbl.name} (${tbl.columns.length})`,
    isTable: true,
    tableName: tbl.name,
    children: tbl.columns.map((col) => ({
      key: `col-${tbl.name}-${col.name}`,
      title: `${col.name}  ${col.dataType}${col.isPrimaryKey ? " PK" : ""}${col.isNullable ? "" : " NOT NULL"}`,
      isLeaf: true,
      isTable: false,
      tableName: ""
    }))
  }))
);

function insertTableName(name: string) {
  sql.value += name + " ";
}

async function loadSchema() {
  if (!dataSource.value?.id) return;
  schemaLoading.value = true;
  try {
    const result = await getTenantDataSourceSchema(dataSource.value.id);
    if (result.success) {
      schemaTables.value = result.tables;
    } else {
      message.error(result.errorMessage || t("datasource.schemaFailed"));
    }
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : t("datasource.schemaFailed");
    message.error(msg);
  } finally {
    schemaLoading.value = false;
  }
}

const open = (record: TenantDataSourceDto) => {
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
};

const handleClose = () => {
  visible.value = false;
};

const handleExecute = async () => {
  if (!dataSource.value?.id) return;
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
    } else {
      errorMsg.value = result.errorMessage || t("datasource.executeFailed");
      resultColumns.value = [];
      resultData.value = [];
    }
  } catch (err: unknown) {
    errorMsg.value = err instanceof Error ? err.message : t("datasource.executeFailed");
  } finally {
    loading.value = false;
  }
};

function pushHistory(sqlText: string, timeMs: number) {
  sqlHistory.value.unshift({ sql: sqlText, timeMs, timestamp: Date.now() });
  if (sqlHistory.value.length > MAX_HISTORY) {
    sqlHistory.value.length = MAX_HISTORY;
  }
}

function handleHistorySelect({ key }: { key: string }) {
  const idx = Number(key);
  if (idx >= 0 && idx < sqlHistory.value.length) {
    sql.value = sqlHistory.value[idx].sql;
  }
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
