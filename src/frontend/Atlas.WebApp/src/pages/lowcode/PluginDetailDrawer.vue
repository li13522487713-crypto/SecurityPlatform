<template>
  <a-drawer
    :title="entry ? entry.name : '插件详情'"
    width="520"
    :open="true"
    @close="$emit('close')"
  >
    <a-spin :spinning="loading">
      <template v-if="entry">
        <div class="detail-header">
          <img v-if="entry.iconUrl" :src="entry.iconUrl" :alt="entry.name" class="detail-icon" />
          <AppstoreOutlined v-else class="detail-icon-default" />
          <div>
            <div class="detail-title">{{ entry.name }}</div>
            <div class="detail-meta">
              <a-tag>{{ entry.category }}</a-tag>
              <span>v{{ entry.latestVersion }}</span>
              <span>{{ entry.author }}</span>
              <span>{{ entry.downloads }} 次安装</span>
            </div>
          </div>
        </div>

        <a-divider />
        <p>{{ entry.description }}</p>
        <a-divider>版本历史</a-divider>

        <a-timeline>
          <a-timeline-item v-for="v in versions" :key="v.id">
            <div><strong>v{{ v.version }}</strong> — {{ formatDate(v.publishedAt) }}</div>
            <div v-if="v.releaseNotes" class="release-notes">{{ v.releaseNotes }}</div>
          </a-timeline-item>
        </a-timeline>
      </template>
    </a-spin>

    <template #footer>
      <a-space>
        <a-button @click="$emit('close')">关闭</a-button>
        <a-button
          v-if="entry"
          type="primary"
          :href="entry.packageUrl ?? undefined"
          target="_blank"
          :disabled="!entry.packageUrl"
        >
          下载安装包
        </a-button>
      </a-space>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { AppstoreOutlined } from '@ant-design/icons-vue'
import { getPluginMarketEntry, getPluginMarketVersions } from '@/services/api-plugin'
import type { PluginMarketEntry, PluginMarketVersion } from '@/types/plugin'

const props = defineProps<{ code: string }>()
defineEmits<{ close: [] }>()

const loading = ref(false)
const entry = ref<PluginMarketEntry | null>(null)
const versions = ref<PluginMarketVersion[]>([])

async function load() {
  loading.value = true
  try {
    const [entryRes, versionsRes] = await Promise.all([
      getPluginMarketEntry(props.code),
      getPluginMarketVersions(props.code),
    ])
    if (entryRes.success) entry.value = entryRes.data ?? null
    if (versionsRes.success) versions.value = versionsRes.data ?? []
  } finally {
    loading.value = false
  }
}

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('zh-CN')
}

onMounted(load)
</script>

<style scoped>
.detail-header {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}
.detail-icon {
  width: 64px;
  height: 64px;
  object-fit: contain;
  border-radius: 8px;
  border: 1px solid #f0f0f0;
}
.detail-icon-default {
  font-size: 64px;
  color: #bfbfbf;
}
.detail-title {
  font-size: 18px;
  font-weight: 600;
  margin-bottom: 6px;
}
.detail-meta {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
  color: #595959;
  font-size: 13px;
}
.release-notes {
  color: #595959;
  font-size: 13px;
  margin-top: 2px;
}
</style>
