<template>
  <div class="schema-snapshot-panel">
    <a-space class="toolbar" wrap>
      <a-input
        v-model:value="keyword"
        allow-clear
        style="width: 220px"
        :placeholder="t('dynamic.schemaSnapshotPanel.searchPlaceholder')"
      />
      <a-button type="primary" :disabled="!canCompare" @click="emit('compare', selectedRowKeys)">
        {{ t("dynamic.schemaSnapshotPanel.compare") }}
      </a-button>
    </a-space>
    <a-table
      row-key="id"
      size="small"
      :row-selection="rowSelection"
      :columns="columns"
      :data-source="displayRows"
      :pagination="{ pageSize: 8, size: 'small' }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
        </template>
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" size="small" @click="emit('restore', record.id)">
              {{ t("dynamic.schemaSnapshotPanel.restore") }}
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import type { TableColumnType } from "ant-design-vue";
import type { TableProps } from "ant-design-vue";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";

export type SnapshotStatus = "draft" | "published" | "archived";

export interface SchemaSnapshotRow {
  id: string;
  version: string;
  createdAt: string;
  status: SnapshotStatus;
}

const props = withDefaults(
  defineProps<{
    tableKey?: string;
    rows?: SchemaSnapshotRow[];
  }>(),
  {
    tableKey: "",
    rows: () => [
      { id: "s1", version: "2026.04.01-1", createdAt: "2026-04-01 10:00", status: "published" },
      { id: "s2", version: "2026.04.02-1", createdAt: "2026-04-02 11:20", status: "draft" }
    ]
  }
);

const emit = defineEmits<{
  (e: "compare", ids: string[]): void;
  (e: "restore", id: string): void;
}>();

const { t } = useI18n();
const keyword = ref("");
const selectedRowKeys = ref<string[]>([]);

const columns = computed<TableColumnType[]>(() => [
  { title: t("dynamic.schemaSnapshotPanel.colVersion"), dataIndex: "version", key: "version" },
  { title: t("dynamic.schemaSnapshotPanel.colDate"), dataIndex: "createdAt", key: "createdAt" },
  { title: t("dynamic.schemaSnapshotPanel.colStatus"), key: "status", width: 120 },
  { title: t("dynamic.schemaSnapshotPanel.colActions"), key: "actions", width: 120 }
]);

const displayRows = computed(() => {
  const q = keyword.value.trim().toLowerCase();
  if (!q) {
    return props.rows;
  }
  return props.rows.filter((r) => r.version.toLowerCase().includes(q) || r.id.toLowerCase().includes(q));
});

const canCompare = computed(() => selectedRowKeys.value.length === 2);

const rowSelection = computed<TableProps["rowSelection"]>(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: (string | number)[]) => {
    selectedRowKeys.value = keys.map(String).slice(0, 2);
  }
}));

function statusColor(s: SnapshotStatus): string {
  if (s === "published") {
    return "green";
  }
  if (s === "draft") {
    return "gold";
  }
  return "default";
}

function statusLabel(s: SnapshotStatus): string {
  const map: Record<SnapshotStatus, string> = {
    draft: t("dynamic.statusDraft"),
    published: t("dynamic.statusActive"),
    archived: t("dynamic.statusArchived")
  };
  return map[s];
}
</script>

<style scoped>
.schema-snapshot-panel {
  padding: 8px 0;
}

.toolbar {
  margin-bottom: 12px;
}
</style>
