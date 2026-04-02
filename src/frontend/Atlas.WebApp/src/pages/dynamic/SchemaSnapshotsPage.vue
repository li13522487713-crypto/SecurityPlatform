<template>
  <div class="schema-snapshots-page">
    <a-page-header :title="t('dynamic.schemaSnapshots')" :sub-title="tableKey">
      <template #extra>
        <a-button type="primary" @click="handlePublish">
          {{ t('dynamic.publishSnapshot') }}
        </a-button>
      </template>
    </a-page-header>

    <a-card :bordered="false">
      <a-table
        :columns="columns"
        :data-source="snapshots"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'version'">
            <a-tag color="blue">v{{ record.version }}</a-tag>
          </template>
          <template v-else-if="column.key === 'publishedAt'">
            {{ formatDate(record.publishedAt) }}
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button size="small" @click="handleViewDetail(record)">
                {{ t('common.detail') }}
              </a-button>
              <a-button
                size="small"
                :disabled="record.version <= 1"
                @click="handleDiff(record)"
              >
                {{ t('dynamic.diffWithPrevious') }}
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="showPublishModal"
      :title="t('dynamic.publishSnapshot')"
      @ok="confirmPublish"
      :confirm-loading="publishing"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('dynamic.publishNote')">
          <a-textarea v-model:value="publishNote" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-drawer
      v-model:open="showDiffDrawer"
      :title="t('dynamic.schemaDiff')"
      width="720"
      placement="right"
    >
      <template v-if="diffResult">
        <a-descriptions :column="2" bordered size="small" class="diff-meta">
          <a-descriptions-item :label="t('dynamic.fromVersion')">
            v{{ diffResult.fromVersion }}
          </a-descriptions-item>
          <a-descriptions-item :label="t('dynamic.toVersion')">
            v{{ diffResult.toVersion }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider>{{ t('dynamic.fieldChanges') }}</a-divider>
        <a-table
          :columns="diffFieldColumns"
          :data-source="diffResult.fieldChanges"
          :pagination="false"
          size="small"
          row-key="fieldName"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'changeType'">
              <a-tag :color="changeTypeColor(record.changeType)">
                {{ record.changeType }}
              </a-tag>
            </template>
          </template>
        </a-table>

        <a-divider>{{ t('dynamic.indexChanges') }}</a-divider>
        <a-table
          :columns="diffIndexColumns"
          :data-source="diffResult.indexChanges"
          :pagination="false"
          size="small"
          row-key="indexName"
        />
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { message } from 'ant-design-vue'
import {
  getSnapshots,
  createSnapshot,
  diffSnapshots,
} from '@/services/schema-publish'
import type {
  SchemaPublishSnapshotListItem,
  SchemaSnapshotDiffResult,
} from '@/types/schema-publish'

const { t } = useI18n()
const route = useRoute()
const tableKey = computed(() => String(route.params.tableKey ?? ''))

const snapshots = ref<SchemaPublishSnapshotListItem[]>([])
const loading = ref(false)
const pagination = ref({ current: 1, pageSize: 20, total: 0 })

const showPublishModal = ref(false)
const publishNote = ref('')
const publishing = ref(false)

const showDiffDrawer = ref(false)
const diffResult = ref<SchemaSnapshotDiffResult | null>(null)

const columns = [
  { title: t('dynamic.version'), key: 'version', dataIndex: 'version' },
  { title: t('dynamic.publishNote'), dataIndex: 'publishNote', ellipsis: true },
  { title: t('dynamic.publishedAt'), key: 'publishedAt', dataIndex: 'publishedAt' },
  { title: t('dynamic.publishedBy'), dataIndex: 'publishedBy' },
  { title: t('common.actions'), key: 'actions', width: 200 },
]

const diffFieldColumns = [
  { title: t('dynamic.fieldName'), dataIndex: 'fieldName' },
  { title: t('dynamic.changeType'), key: 'changeType', dataIndex: 'changeType' },
  { title: t('dynamic.oldDefinition'), dataIndex: 'oldDefinition', ellipsis: true },
  { title: t('dynamic.newDefinition'), dataIndex: 'newDefinition', ellipsis: true },
]

const diffIndexColumns = [
  { title: t('dynamic.indexName'), dataIndex: 'indexName' },
  { title: t('dynamic.changeType'), dataIndex: 'changeType' },
]

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString()
}

function changeTypeColor(type: string): string {
  switch (type) {
    case 'Added': return 'green'
    case 'Modified': return 'orange'
    case 'Removed': return 'red'
    default: return 'default'
  }
}

async function fetchSnapshots() {
  loading.value = true
  try {
    const res = await getSnapshots(tableKey.value, {
      pageIndex: pagination.value.current,
      pageSize: pagination.value.pageSize,
    })
    if (res.success && res.data) {
      snapshots.value = res.data.items
      pagination.value.total = res.data.total
    }
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: { current: number; pageSize: number }) {
  pagination.value.current = pag.current
  pagination.value.pageSize = pag.pageSize
  fetchSnapshots()
}

function handlePublish() {
  publishNote.value = ''
  showPublishModal.value = true
}

async function confirmPublish() {
  publishing.value = true
  try {
    const res = await createSnapshot({
      tableKey: tableKey.value,
      publishNote: publishNote.value || null,
    })
    if (res.success) {
      message.success(t('dynamic.snapshotPublished'))
      showPublishModal.value = false
      await fetchSnapshots()
    }
  } finally {
    publishing.value = false
  }
}

function handleViewDetail(record: SchemaPublishSnapshotListItem) {
  message.info(`Snapshot v${record.version} - ID: ${record.id}`)
}

async function handleDiff(record: SchemaPublishSnapshotListItem) {
  const res = await diffSnapshots(tableKey.value, record.version - 1, record.version)
  if (res.success && res.data) {
    diffResult.value = res.data
    showDiffDrawer.value = true
  }
}

onMounted(fetchSnapshots)
</script>

<style scoped>
.schema-snapshots-page {
  padding: 16px;
}
.diff-meta {
  margin-bottom: 16px;
}
</style>
