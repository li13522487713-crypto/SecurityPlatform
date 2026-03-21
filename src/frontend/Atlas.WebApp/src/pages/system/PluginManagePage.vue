<template>
  <CrudPageLayout title="插件管理">
    <template #toolbar-actions>
      <a-upload
        :show-upload-list="false"
        accept=".atpkg,.zip"
        :before-upload="handleUpload"
      >
        <a-button type="primary" :loading="uploading">
          <UploadOutlined />上传安装包
        </a-button>
      </a-upload>
      <a-button :loading="reloading" @click="handleReload">
        <ReloadOutlined />重新加载
      </a-button>
    </template>

    <template #table>
      <a-table
        :columns="columns"
        :data-source="plugins"
        :loading="loading"
        row-key="code"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'state'">
            <a-badge
              :status="stateBadge(record.state)"
              :text="record.state"
            />
          </template>
          <template v-if="column.key === 'category'">
            <a-tag>{{ record.category }}</a-tag>
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button
                v-if="record.state === 'Disabled'"
                size="small"
                type="link"
                @click="handleEnable(record.code)"
              >启用</a-button>
              <a-button
                v-else-if="record.state === 'Loaded'"
                size="small"
                type="link"
                danger
                @click="handleDisable(record.code)"
              >禁用</a-button>
              <a-button
                v-if="record.state !== 'Unloaded'"
                size="small"
                type="link"
                danger
                @click="handleUnload(record.code)"
              >卸载</a-button>
              <a-button
                size="small"
                type="link"
                @click="openConfig(record)"
              >配置</a-button>
            </a-space>
          </template>
        </template>
      </a-table>

      <!-- 配置 Drawer -->
      <a-drawer
        v-if="configPlugin"
        :title="`插件配置 — ${configPlugin.name}`"
        width="480"
        :open="true"
        @close="configPlugin = null"
      >
        <a-textarea
          v-model:value="configJson"
          :rows="16"
          placeholder='{"key": "value"}'
        />
        <template #footer>
          <a-space>
            <a-button @click="configPlugin = null">取消</a-button>
            <a-button type="primary" :loading="savingConfig" @click="handleSaveConfig">保存</a-button>
          </a-space>
        </template>
      </a-drawer>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { UploadOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import { message } from 'ant-design-vue'
import type { BadgeProps } from 'ant-design-vue'
import {
  getInstalledPlugins,
  enablePlugin,
  disablePlugin,
  unloadPlugin,
  reloadPlugins,
  installPluginPackage,
  getPluginConfig,
  savePluginConfig,
} from '@/services/api-plugin'
import type { PluginDescriptor, PluginState } from '@/types/plugin'

const loading = ref(false)
const plugins = ref<PluginDescriptor[]>([])
const reloading = ref(false)
const uploading = ref(false)
const configPlugin = ref<PluginDescriptor | null>(null)
const configJson = ref('')
const savingConfig = ref(false)

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name' },
  { title: '代码', dataIndex: 'code', key: 'code' },
  { title: '版本', dataIndex: 'version', key: 'version' },
  { title: '分类', key: 'category' },
  { title: '作者', dataIndex: 'author', key: 'author' },
  { title: '状态', key: 'state' },
  { title: '操作', key: 'actions', width: 200 },
]

async function fetchPlugins() {
  loading.value = true
  try {
    const res = await getInstalledPlugins()
    if (res.success) plugins.value = res.data ?? []
  } finally {
    loading.value = false
  }
}

async function handleEnable(code: string) {
  await enablePlugin(code)
  message.success('已启用')
  fetchPlugins()
}

async function handleDisable(code: string) {
  await disablePlugin(code)
  message.success('已禁用')
  fetchPlugins()
}

async function handleUnload(code: string) {
  await unloadPlugin(code)
  message.success('已卸载')
  fetchPlugins()
}

async function handleReload() {
  reloading.value = true
  try {
    await reloadPlugins()
    await fetchPlugins()
    message.success('重载完成')
  } finally {
    reloading.value = false
  }
}

async function handleUpload(file: File) {
  uploading.value = true
  try {
    const res = await installPluginPackage(file)
    if (res.success) {
      message.success(`插件 ${res.data?.code} 安装成功`)
      fetchPlugins()
    }
  } catch {
    message.error('安装失败')
  } finally {
    uploading.value = false
  }
  return false // 阻止默认上传
}

async function openConfig(plugin: PluginDescriptor) {
  configPlugin.value = plugin
  configJson.value = '{}'
  const res = await getPluginConfig(plugin.code)
  if (res.success && res.data?.configJson) {
    configJson.value = res.data.configJson
  }
}

async function handleSaveConfig() {
  if (!configPlugin.value) return
  savingConfig.value = true
  try {
    await savePluginConfig(configPlugin.value.code, 'Global', configJson.value)
    message.success('配置已保存')
    configPlugin.value = null
  } finally {
    savingConfig.value = false
  }
}

function stateBadge(state: PluginState): BadgeProps['status'] {
  const map: Record<PluginState, BadgeProps['status']> = {
    Loaded: 'success',
    Disabled: 'warning',
    Unloaded: 'default',
    Failed: 'error',
    NoEntryPoint: 'error',
  }
  return map[state] ?? 'default'
}

onMounted(fetchPlugins)
</script>
