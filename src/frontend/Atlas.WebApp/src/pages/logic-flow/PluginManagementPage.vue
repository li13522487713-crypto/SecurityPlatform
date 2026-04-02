<template>
  <div class="plugin-mgmt">
    <a-page-header :title="t('logicFlow.pluginManagement.title')" @back="$router.back()" />

    <a-card :bordered="false">
      <a-space style="margin-bottom: 12px" wrap>
        <a-select
          v-model:value="typeFilter"
          allow-clear
          style="width: 200px"
          :placeholder="t('logicFlow.pluginManagement.filterType')"
          @change="applyFilter"
        >
          <a-select-option value="Node">{{ t('logicFlow.pluginManagement.typeNode') }}</a-select-option>
          <a-select-option value="Function">{{ t('logicFlow.pluginManagement.typeFunction') }}</a-select-option>
          <a-select-option value="DataSource">{{ t('logicFlow.pluginManagement.typeDataSource') }}</a-select-option>
          <a-select-option value="Template">{{ t('logicFlow.pluginManagement.typeTemplate') }}</a-select-option>
        </a-select>
        <a-button @click="loadAll">{{ t('batchProcess.common.refresh') }}</a-button>
      </a-space>

      <a-table
        :columns="columns"
        :data-source="filteredPlugins"
        :loading="loading"
        row-key="rowKey"
        :pagination="false"
        :custom-row="customRow"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'pluginType'">
            <a-tag>{{ record.pluginType }}</a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" @click.stop="openDetail(record)">
              {{ t('batchProcess.common.detail') }}
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer v-model:open="drawerOpen" width="420" :title="t('logicFlow.pluginManagement.detailTitle')" @close="drawerOpen = false">
      <template v-if="selected">
        <a-descriptions :column="1" size="small" bordered>
          <a-descriptions-item :label="t('logicFlow.pluginManagement.colType')">{{ selected.pluginType }}</a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.pluginManagement.colKey')">{{ selected.key }}</a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.pluginManagement.colDisplay')">{{ selected.displayName }}</a-descriptions-item>
        </a-descriptions>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { getPlugins, type PluginInfoDto } from '@/services/api-logic-flow'

const { t } = useI18n()

const loading = ref(false)
const plugins = ref<(PluginInfoDto & { rowKey: string })[]>([])
const typeFilter = ref<string | undefined>(undefined)
const drawerOpen = ref(false)
const selected = ref<PluginInfoDto | null>(null)

const columns = computed(() => [
  { title: t('logicFlow.pluginManagement.colType'), key: 'pluginType', width: 140 },
  { title: t('logicFlow.pluginManagement.colKey'), dataIndex: 'key', key: 'key', ellipsis: true },
  { title: t('logicFlow.pluginManagement.colDisplay'), dataIndex: 'displayName', key: 'displayName', ellipsis: true },
  { title: t('batchProcess.common.action'), key: 'action', width: 100 },
])

const filteredPlugins = computed(() => {
  if (!typeFilter.value) return plugins.value
  return plugins.value.filter((p) => p.pluginType === typeFilter.value)
})

async function loadAll() {
  loading.value = true
  try {
    const res = await getPlugins()
    const rows = res?.data ?? []
    plugins.value = rows.map((p) => ({
      ...p,
      rowKey: `${p.pluginType}:${p.key}`,
    }))
  } finally {
    loading.value = false
  }
}

function applyFilter() {
  /* filter is computed */
}

function openDetail(record: PluginInfoDto) {
  selected.value = record
  drawerOpen.value = true
}

function customRow(record: PluginInfoDto & { rowKey: string }) {
  return {
    onClick: () => openDetail(record),
  }
}

onMounted(() => {
  loadAll()
})
</script>

<style scoped>
.plugin-mgmt {
  padding: 0 0 24px;
}
</style>
