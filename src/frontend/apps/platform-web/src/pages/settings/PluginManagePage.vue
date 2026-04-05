<template>
  <CrudPageLayout :title="t('systemPlugins.pageTitle')">
    <template #toolbar-actions>
      <a-upload
        :show-upload-list="false"
        accept=".atpkg,.zip"
        :before-upload="handleUpload"
      >
        <a-button type="primary" :loading="uploading">
          <UploadOutlined />{{ t("systemPlugins.uploadPackage") }}
        </a-button>
      </a-upload>
      <a-button :loading="reloading" @click="handleReload">
        <ReloadOutlined />{{ t("systemPlugins.reload") }}
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
              >{{ t("systemPlugins.enable") }}</a-button>
              <a-button
                v-else-if="record.state === 'Loaded'"
                size="small"
                type="link"
                danger
                @click="handleDisable(record.code)"
              >{{ t("systemPlugins.disable") }}</a-button>
              <a-button
                v-if="record.state !== 'Unloaded'"
                size="small"
                type="link"
                danger
                @click="handleUnload(record.code)"
              >{{ t("systemPlugins.unload") }}</a-button>
              <a-button
                size="small"
                type="link"
                @click="openConfig(record)"
              >{{ t("systemPlugins.config") }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>

      <a-drawer
        v-if="configPlugin"
        :title="t('systemPlugins.configDrawerTitle', { name: configPlugin.name })"
        width="480"
        :open="true"
        @close="configPlugin = null"
      >
        <a-textarea
          v-model:value="configJson"
          :rows="16"
          :placeholder="t('systemPlugins.configPlaceholder')"
        />
        <template #footer>
          <a-space>
            <a-button @click="configPlugin = null">{{ t("common.cancel") }}</a-button>
            <a-button type="primary" :loading="savingConfig" @click="handleSaveConfig">{{ t("common.save") }}</a-button>
          </a-space>
        </template>
      </a-drawer>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { UploadOutlined, ReloadOutlined } from "@ant-design/icons-vue";
import type { BadgeProps } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { CrudPageLayout } from "@atlas/shared-ui";
import {
  disablePlugin,
  enablePlugin,
  getInstalledPlugins,
  getPluginConfig,
  installPluginPackage,
  reloadPlugins,
  savePluginConfig,
  unloadPlugin
} from "@/services/api-plugin";
import type { PluginDescriptor, PluginState } from "@/types/plugin";

const { t } = useI18n();
const isMounted = ref(false);
const loading = ref(false);
const plugins = ref<PluginDescriptor[]>([]);
const reloading = ref(false);
const uploading = ref(false);
const configPlugin = ref<PluginDescriptor | null>(null);
const configJson = ref("");
const savingConfig = ref(false);

const columns = computed(() => [
  { title: t("systemPlugins.colName"), dataIndex: "name", key: "name" },
  { title: t("systemPlugins.colCode"), dataIndex: "code", key: "code" },
  { title: t("systemPlugins.colVersion"), dataIndex: "version", key: "version" },
  { title: t("systemPlugins.colCategory"), key: "category" },
  { title: t("systemPlugins.colAuthor"), dataIndex: "author", key: "author" },
  { title: t("systemPlugins.colState"), key: "state" },
  { title: t("systemPlugins.colActions"), key: "actions", width: 200 }
]);

async function fetchPlugins() {
  loading.value = true;
  try {
    const response = await getInstalledPlugins();
    if (!isMounted.value) return;
    if (response.success) {
      plugins.value = response.data ?? [];
    }
  } finally {
    loading.value = false;
  }
}

async function handleEnable(code: string) {
  await enablePlugin(code);
  if (!isMounted.value) return;
  message.success(t("systemPlugins.enabledOk"));
  void fetchPlugins();
}

async function handleDisable(code: string) {
  await disablePlugin(code);
  if (!isMounted.value) return;
  message.success(t("systemPlugins.disabledOk"));
  void fetchPlugins();
}

async function handleUnload(code: string) {
  await unloadPlugin(code);
  if (!isMounted.value) return;
  message.success(t("systemPlugins.unloadedOk"));
  void fetchPlugins();
}

async function handleReload() {
  reloading.value = true;
  try {
    await reloadPlugins();
    await fetchPlugins();
    if (!isMounted.value) return;
    message.success(t("systemPlugins.reloadDone"));
  } finally {
    reloading.value = false;
  }
}

async function handleUpload(file: File) {
  uploading.value = true;
  try {
    const response = await installPluginPackage(file);
    if (!isMounted.value) return false;
    if (response.success) {
      message.success(t("systemPlugins.installSuccess", { code: response.data?.code ?? "" }));
      void fetchPlugins();
    }
  } catch {
    if (!isMounted.value) return false;
    message.error(t("systemPlugins.installFailed"));
  } finally {
    uploading.value = false;
  }
  return false;
}

async function openConfig(plugin: PluginDescriptor) {
  configPlugin.value = plugin;
  configJson.value = "{}";
  const response = await getPluginConfig(plugin.code);
  if (!isMounted.value) return;
  if (response.success && response.data?.configJson) {
    configJson.value = response.data.configJson;
  }
}

async function handleSaveConfig() {
  if (!configPlugin.value) return;
  savingConfig.value = true;
  try {
    await savePluginConfig(configPlugin.value.code, "Global", configJson.value);
    if (!isMounted.value) return;
    message.success(t("systemPlugins.configSaved"));
    configPlugin.value = null;
  } finally {
    savingConfig.value = false;
  }
}

function stateBadge(state: PluginState): BadgeProps["status"] {
  const mapping: Record<PluginState, BadgeProps["status"]> = {
    Loaded: "success",
    Disabled: "warning",
    Unloaded: "default",
    Failed: "error",
    NoEntryPoint: "error"
  };
  return mapping[state] ?? "default";
}

onMounted(() => {
  isMounted.value = true;
  void fetchPlugins();
});

onUnmounted(() => {
  isMounted.value = false;
});
</script>
