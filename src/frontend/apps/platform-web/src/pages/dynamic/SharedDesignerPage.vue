<template>
  <PageContainer :title="t('dynamic.sharedDesigner.title')">
    <DesignerCanvas
      v-model:mode="mode"
      :title="t('dynamicDesigner.centerTitle')"
      :context="designerContext"
      :show-mode-switcher="true"
      :available-modes="designerModes"
      @mode-change="onModeChange"
      class="platform-designer-canvas"
    >
      <template #entity>
        <template v-if="mode === 'entity' && route.query.tableKey">
          <DynamicTableDesignPage />
        </template>
        <a-empty v-else-if="mode === 'entity'" :description="t('dynamic.selectTableFirst')" />
      </template>

      <template #relation>
        <RelationDesigner :app-id="designerContext.appId" />
      </template>

      <template #view>
        <ViewDesignerCanvas :app-id="designerContext.appId || ''" :view-key="designerContext.viewKey" />
      </template>

      <template #transform>
        <TransformDesignerCanvas />
      </template>
    </DesignerCanvas>
  </PageContainer>
</template>

<script setup lang="ts">
import { computed, unref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { DesignerCanvas, type DesignerCanvasMode } from "@atlas/designer-vue";
import { PageContainer } from "@atlas/shared-ui";
import RelationDesigner from "@/components/designer/RelationDesigner.vue";
import ViewDesignerCanvas from "@/components/designer/ViewDesignerCanvas.vue";
import TransformDesignerCanvas from "@/components/designer/TransformDesignerCanvas.vue";
import DynamicTableDesignPage from "./DynamicTableDesignPage.vue";

const router = useRouter();
const route = useRoute();
const { t } = useI18n();

const routeMode = computed<DesignerCanvasMode>(() => {
  const mode = route.query.mode;
  if (mode === "relation" || mode === "view" || mode === "transform") {
    return mode;
  }
  return "entity";
});

const routeValue = computed(() => {
  const appId = typeof route.query.appId === "string"
    ? route.query.appId.trim()
    : typeof route.params.appId === "string"
      ? decodeURIComponent(route.params.appId)
      : "";
  const tableKey = typeof route.query.tableKey === "string" ? route.query.tableKey.trim() : "";
  const viewKey = typeof route.query.viewKey === "string" ? route.query.viewKey.trim() : "";
  return {
    appId,
    tableKey,
    viewKey,
  };
});

const designerContext = computed(() => ({
  appId: routeValue.value.appId || undefined,
  tableKey: routeValue.value.tableKey,
  viewKey: routeValue.value.viewKey,
}));

const mode = computed({
  get: () => routeMode.value,
  set: (nextMode: DesignerCanvasMode) => {
    onModeChange(nextMode);
  },
});

const designerModes: DesignerCanvasMode[] = ["entity", "relation", "view", "transform"];

function onModeChange(nextMode: DesignerCanvasMode) {
  const current = unref(routeMode);
  if (current === nextMode) {
    return;
  }
  void router.push({
    query: {
      ...route.query,
      mode: nextMode,
    },
  });
}
</script>

<style scoped>
.platform-designer-canvas :deep(.ant-card) {
  margin-bottom: 0;
}
</style>
