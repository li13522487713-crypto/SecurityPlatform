<template>
  <div class="db-maintenance-page">
    <a-page-header :title="t('dbMaintenance.title')" :sub-title="t('dbMaintenance.subtitle')" />

    <!-- Database Info Card -->
    <a-card :title="t('dbMaintenance.connectionInfo')" :loading="loadingInfo" style="margin-bottom: 16px">
      <a-descriptions bordered :column="2" size="small">
        <a-descriptions-item :label="t('dbMaintenance.dbType')">{{ dbInfo?.dbType }}</a-descriptions-item>
        <a-descriptions-item :label="t('dbMaintenance.journalMode')">{{ dbInfo?.journalMode }}</a-descriptions-item>
        <a-descriptions-item :label="t('dbMaintenance.fileSize')">{{ formatFileSize(dbInfo?.fileSizeBytes) }}</a-descriptions-item>
        <a-descriptions-item :label="t('dbMaintenance.connectionStr')">
          <span class="masked-text">{{ dbInfo?.connectionString }}</span>
        </a-descriptions-item>
      </a-descriptions>
      <template #extra>
        <a-space>
          <a-button :loading="testingConn" @click="handleTestConn">
            {{ t("dbMaintenance.testConnection") }}
          </a-button>
          <a-tag v-if="connResult !== null" :color="connResult.connected ? 'success' : 'error'">
            {{ connResult.connected ? `${t("dbMaintenance.connected")} (${connResult.latencyMs}ms)` : connResult.message }}
          </a-tag>
        </a-space>
      </template>
    </a-card>

    <!-- Backup Card -->
    <a-card :title="t('dbMaintenance.backups')" style="margin-bottom: 16px">
      <template #extra>
        <a-button type="primary" :loading="backingUp" @click="handleBackup">
          {{ t("dbMaintenance.backupNow") }}
        </a-button>
      </template>

      <a-table :data-source="backups" :columns="backupColumns" :loading="loadingBackups"
        :pagination="false" row-key="fileName" size="small">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'sizeBytes'">{{ formatFileSize(record.sizeBytes) }}</template>
          <template v-if="column.key === 'createdAt'">{{ formatDate(record.createdAt) }}</template>
          <template v-if="column.key === 'actions'">
            <a-popconfirm :title="t('dbMaintenance.restoreConfirm')" @confirm="handleRestore(record.fileName)">
              <a-button type="link" danger size="small">{{ t("dbMaintenance.restore") }}</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import {
  testConnection,
  getDatabaseInfo,
  listBackups,
  backupNow,
  restoreFromBackup,
  type DatabaseInfo,
  type DatabaseConnectionStatus,
  type BackupFileInfo
} from "@/services/api-db-maintenance";

const { t } = useI18n();

const dbInfo = ref<DatabaseInfo | null>(null);
const loadingInfo = ref(false);
const testingConn = ref(false);
const connResult = ref<DatabaseConnectionStatus | null>(null);
const backups = ref<BackupFileInfo[]>([]);
const loadingBackups = ref(false);
const backingUp = ref(false);

const backupColumns = computed(() => [
  { title: t("dbMaintenance.fileName"), dataIndex: "fileName", key: "fileName" },
  { title: t("dbMaintenance.fileSize"), key: "sizeBytes" },
  { title: t("dbMaintenance.createdAt"), key: "createdAt" },
  { title: t("dbMaintenance.sha256"), dataIndex: "sha256", key: "sha256", ellipsis: true },
  { title: t("common.edit"), key: "actions", width: 100 }
]);

onMounted(async () => {
  await Promise.all([loadDbInfo(), loadBackups()]);
});

async function loadDbInfo() {
  loadingInfo.value = true;
  try {
    const resp = await getDatabaseInfo();
    if (resp.success && resp.data) {
      dbInfo.value = resp.data;
    }
  } finally {
    loadingInfo.value = false;
  }
}

async function loadBackups() {
  loadingBackups.value = true;
  try {
    const resp = await listBackups();
    if (resp.success && resp.data) {
      backups.value = resp.data;
    }
  } finally {
    loadingBackups.value = false;
  }
}

async function handleTestConn() {
  testingConn.value = true;
  connResult.value = null;
  try {
    const resp = await testConnection();
    if (resp.success && resp.data) {
      connResult.value = resp.data;
    }
  } finally {
    testingConn.value = false;
  }
}

async function handleBackup() {
  backingUp.value = true;
  try {
    const resp = await backupNow();
    if (resp.success && resp.data?.success) {
      message.success(t("common.success"));
      await loadBackups();
    } else {
      message.error(resp.data?.message ?? t("common.failed"));
    }
  } finally {
    backingUp.value = false;
  }
}

async function handleRestore(fileName: string) {
  try {
    const resp = await restoreFromBackup(fileName);
    if (resp.success) {
      message.success(t("common.success"));
    } else {
      message.error(resp.message ?? t("common.failed"));
    }
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : String(e));
  }
}

function formatFileSize(bytes: number | null | undefined): string {
  if (bytes == null) return "-";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1048576).toFixed(1)} MB`;
}

function formatDate(dateStr: string): string {
  if (!dateStr) return "-";
  return new Date(dateStr).toLocaleString();
}
</script>

<style scoped>
.db-maintenance-page {
  padding: 16px;
}

.masked-text {
  font-family: monospace;
  font-size: 12px;
  word-break: break-all;
}
</style>
