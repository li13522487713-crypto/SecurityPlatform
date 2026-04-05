<template>
  <a-card :title="t('dynamicDesigner.centerTitle')" class="page-card">
    <template #extra>
      <a-space>
        <a-radio-group v-model:value="mode" button-style="solid">
          <a-radio-button value="entity">{{ t("dynamicDesigner.modeEntity") }}</a-radio-button>
          <a-radio-button value="relation">{{ t("dynamicDesigner.modeRelation") }}</a-radio-button>
          <a-radio-button value="view">{{ t("dynamicDesigner.modeView") }}</a-radio-button>
          <a-radio-button value="transform">{{ t("dynamicDesigner.modeTransform") }}</a-radio-button>
        </a-radio-group>
      </a-space>
    </template>

    <DynamicTableDesignPage v-if="mode === 'entity' && tableKey" />
    <a-empty v-else-if="mode === 'entity' && !tableKey" :description="t('dynamic.selectTableFirst')" />
    <RelationDesigner v-else-if="mode === 'relation'" :app-id="appId" />
    <ViewDesignerCanvas v-else-if="mode === 'view'" :app-id="appId" :view-key="viewKey" />
    <TransformDesignerCanvas v-else />
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { useI18n } from "vue-i18n";
import DynamicTableDesignPage from "@/pages/dynamic/DynamicTableDesignPage.vue";
import RelationDesigner from "@/components/designer/RelationDesigner.vue";
import ViewDesignerCanvas from "@/components/designer/ViewDesignerCanvas.vue";
import TransformDesignerCanvas from "@/components/designer/TransformDesignerCanvas.vue";

const { t } = useI18n();
const route = useRoute();

type DesignerMode = "entity" | "relation" | "view" | "transform";

const parseMode = (value: string | undefined): DesignerMode => {
  if (value === "entity" || value === "relation" || value === "view" || value === "transform") {
    return value;
  }
  return "entity";
};

const mode = ref<DesignerMode>(parseMode(typeof route.query.mode === "string" ? route.query.mode : undefined));
const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.query.tableKey === "string" ? route.query.tableKey : ""));
const viewKey = computed(() => (typeof route.query.viewKey === "string" ? route.query.viewKey : undefined));
</script>

<style scoped>
.page-card {
  height: 100%;
}
</style>
