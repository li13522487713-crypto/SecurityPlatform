<template>
  <a-card :title="pageTitle" data-testid="app-runtime-page">
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" />
      <a-empty v-else :description="t('runtimePage.emptyNoPage')" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { useRoute } from "vue-router";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import { buildRuntimeRecordsUrl, getRuntimeMenu, getRuntimePageSchema } from "@/services/api-runtime";
import type { AmisSchema } from "@/types/amis";

const { t } = useI18n();
const route = useRoute();

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

const loading = ref(false);
const schema = ref<AmisSchema | null>(null);
const pageTitle = ref(t("runtimePage.defaultTitle"));

const appKey = computed(() => String(route.params.appKey ?? ""));
const pageKey = computed(() => String(route.params.pageKey ?? ""));

function applyRuntimeApis(schemaNode: unknown, currentPageKey: string, currentAppKey: string) {
  if (!schemaNode || typeof schemaNode !== "object") {
    return;
  }

  const node = schemaNode as Record<string, unknown>;
  const baseUrl = buildRuntimeRecordsUrl(currentPageKey, currentAppKey);

  if (node.type === "form" && !node.api) {
    node.api = `post:${baseUrl}`;
  }

  if (node.type === "form" && !node.initApi && node.dataTableKey) {
    node.initApi = `get:${baseUrl}/\${id}`;
  }

  if (node.type === "crud" && !node.api) {
    node.api = baseUrl;
  }

  Object.values(node).forEach((child) => {
    if (Array.isArray(child)) {
      child.forEach((item) => applyRuntimeApis(item, currentPageKey, currentAppKey));
      return;
    }

    applyRuntimeApis(child, currentPageKey, currentAppKey);
  });
}

async function loadRuntime() {
  if (!appKey.value || !pageKey.value) {
    schema.value = null;
    return;
  }

  loading.value = true;
  try {
    const [runtimeSchema, runtimeMenu] = await Promise.all([
      getRuntimePageSchema(pageKey.value, appKey.value),
      getRuntimeMenu(appKey.value)
    ]);

    if (!isMounted.value) return;

    const matchedPage = runtimeMenu.items.find((item) => item.pageKey === pageKey.value);
    pageTitle.value = matchedPage?.title ?? `${appKey.value} / ${pageKey.value}`;
    const parsedSchema = JSON.parse(runtimeSchema.schemaJson) as AmisSchema;
    applyRuntimeApis(parsedSchema, pageKey.value, appKey.value);
    schema.value = parsedSchema;
  } catch (error) {
    schema.value = null;
    message.error(error instanceof Error ? error.message : t("runtimePage.loadFailed"));
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadRuntime();
});

watch([appKey, pageKey], () => {
  void loadRuntime();
});
</script>
