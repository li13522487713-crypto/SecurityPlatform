<template>
  <a-card :title="pageTitle" :class="{ 'runtime-mobile-card': isMobile }">
    <a-alert
      v-if="isWorkspacePreviewMode"
      type="warning"
      show-icon
      banner
      message="预览模式"
      description="当前为工作台预览，不计入正式运行记录。"
      class="runtime-preview-banner"
    />
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" />
      <a-empty v-else description="未找到可运行页面，请先在设计器发布页面" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import type { AmisSchema } from "@/types/amis";
import { getLowCodeAppByKey, getLowCodeAppDetail, getLowCodeRuntimePageSchemaByKey } from "@/services/lowcode";

const route = useRoute();
const loading = ref(false);
const schema = ref<AmisSchema | null>(null);
const pageTitle = ref("运行态页面");
const isMobile = computed(() => window.innerWidth <= 768 || route.query.deviceMode === "mobile");
const isWorkspacePreviewMode = computed(() => route.name === "app-workspace-runtime");

const appId = computed(() => String(route.params.appId ?? ""));
const appKey = computed(() => String(route.params.appKey ?? ""));
const pageKey = computed(() => String(route.params.pageKey ?? ""));

function applyRuntimeSubmitApi(schemaNode: unknown, appKeyValue: string, pageKeyValue: string) {
  if (!schemaNode || typeof schemaNode !== "object") {
    return;
  }

  const node = schemaNode as Record<string, unknown>;
  if (node.type === "form" && !node.api) {
    node.api = `post:/api/v1/runtime/apps/${encodeURIComponent(appKeyValue)}/pages/${encodeURIComponent(pageKeyValue)}/records`;
  }

  Object.values(node).forEach((child) => {
    if (Array.isArray(child)) {
      child.forEach((item) => applyRuntimeSubmitApi(item, appKeyValue, pageKeyValue));
      return;
    }
    applyRuntimeSubmitApi(child, appKeyValue, pageKeyValue);
  });
}

async function loadRuntime() {
  if ((!appId.value && !appKey.value) || !pageKey.value) {
    schema.value = null;
    return;
  }

  loading.value = true;
  try {
    const app = appId.value
      ? await getLowCodeAppDetail(appId.value)
      : await getLowCodeAppByKey(appKey.value);
    const page = app.pages.find((item) => item.pageKey === pageKey.value);
    if (!page) {
      schema.value = null;
      return;
    }

    pageTitle.value = `${app.name} / ${page.name}`;
    const runtime  = await getLowCodeRuntimePageSchemaByKey(app.appKey, page.pageKey);

    if (!isMounted.value) return;
    const parsedSchema = JSON.parse(runtime.schemaJson) as AmisSchema;
    applyRuntimeSubmitApi(parsedSchema, app.appKey, page.pageKey);
    schema.value = parsedSchema;
  } catch (error) {
    schema.value = null;
    message.error((error as Error).message || "加载运行态页面失败");
  } finally {
    loading.value = false;
  }
}

onMounted(loadRuntime);
watch([appId, appKey, pageKey], () => {
  loadRuntime();
});
</script>

<style scoped>
.runtime-mobile-card {
  margin: 0;
  border-radius: 0;
}

.runtime-preview-banner {
  margin-bottom: 12px;
}
</style>
