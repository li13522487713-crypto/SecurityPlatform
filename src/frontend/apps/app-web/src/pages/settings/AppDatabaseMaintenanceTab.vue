<template>
  <div class="db-maintenance-tab">
    <!-- Database Info -->
    <a-card :title="t('settings.dbInfo')" :loading="loadingInfo" size="small" style="margin-bottom: 16px">
      <a-descriptions bordered :column="2" size="small">
        <a-descriptions-item :label="t('settings.dbType')">{{ dbInfo?.dbType }}</a-descriptions-item>
        <a-descriptions-item :label="t('settings.journalMode')">{{ dbInfo?.journalMode }}</a-descriptions-item>
        <a-descriptions-item :label="t('settings.dbFileSize')">{{ formatSize(dbInfo?.fileSizeBytes) }}</a-descriptions-item>
        <a-descriptions-item :label="t('settings.connStr')">
          <span class="mono-text">{{ dbInfo?.connectionString }}</span>
        </a-descriptions-item>
      </a-descriptions>
      <template #extra>
        <a-space>
          <a-button size="small" :loading="testingConn" @click="handleTestConn">
            {{ t("settings.testConn") }}
          </a-button>
          <a-tag v-if="connResult !== null" :color="connResult.connected ? 'success' : 'error'">
            {{ connResult.connected ? `OK (${connResult.latencyMs}ms)` : connResult.message }}
          </a-tag>
        </a-space>
      </template>
    </a-card>

    <!-- Backup List -->
    <a-card :title="t('settings.backups')" size="small">
      <template #extra>
        <a-button type="primary" size="small" :loading="backingUp" @click="handleBackup">
          {{ t("settings.backupNow") }}
        </a-button>
      </template>

      <a-table :data-source="backups" :columns="cols" :loading="loadingBackups"
        :pagination="false" row-key="fileName" size="small">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'sizeBytes'">{{ formatSize(record.sizeBytes) }}</template>
          <template v-if="column.key === 'createdAt'">{{ new Date(record.createdAt).toLocaleString() }}</template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import {
  testConnection,
  getDatabaseInfo,
  listBackups,
  backupNow,
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

const cols = computed(() => [
  { title: t("settings.fileName"), dataIndex: "fileName", key: "fileName" },
  { title: t("settings.fileSize"), key: "sizeBytes" },
  { title: t("settings.createdAt"), key: "createdAt" },
  { title: "SHA256", dataIndex: "sha256", key: "sha256", ellipsis: true }
]);

onMounted(async () => {
  await Promise.all([loadDbInfo(), loadBackupList()]);
});

async function loadDbInfo() {
  loadingInfo.value = true;
  try {
    const r = await getDatabaseInfo();
    if (r.success && r.data) dbInfo.value = r.data;
  } finally {
    loadingInfo.value = false;
  }
}

async function loadBackupList() {
  loadingBackups.value = true;
  try {
    const r = await listBackups();
    if (r.success && r.data) backups.value = r.data;
  } finally {
    loadingBackups.value = false;
  }
}

async function handleTestConn() {
  testingConn.value = true;
  connResult.value = null;
  try {
    const r = await testConnection();
    if (r.success && r.data) connResult.value = r.data;
  } finally {
    testingConn.value = false;
  }
}

async function handleBackup() {
  backingUp.value = true;
  try {
    const r = await backupNow();
    if (r.success && r.data?.success) {
      message.success(t("common.success"));
      await loadBackupList();
    } else {
      message.error(r.data?.message ?? t("common.failed"));
    }
  } finally {
    backingUp.value = false;
  }
}

function formatSize(bytes: number | null | undefined): string {
  if (bytes == null) return "-";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1048576).toFixed(1)} MB`;
}
</script>

<style scoped>
.mono-text {
  font-family: monospace;
  font-size: 12px;
  word-break: break-all;
}
</style>
