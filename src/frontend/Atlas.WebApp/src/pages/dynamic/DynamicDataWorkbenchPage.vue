<template>
  <a-card :title="pageTitle" class="page-card">
    <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
      <a-tab-pane key="data" :tab="t('dynamicWorkbench.tabData')">
        <DynamicRecordsNativePage />
      </a-tab-pane>
      <a-tab-pane key="fields" :tab="t('dynamicWorkbench.tabFields')">
        <DynamicTableDesignPage />
      </a-tab-pane>
      <a-tab-pane key="relations" :tab="t('dynamicWorkbench.tabRelations')">
        <RelationDesigner :app-id="appId" />
      </a-tab-pane>
      <a-tab-pane key="views" :tab="t('dynamicWorkbench.tabViews')">
        <ViewDesignerCanvas :app-id="appId" />
      </a-tab-pane>
      <a-tab-pane key="transform" :tab="t('dynamicWorkbench.tabTransform')">
        <TransformDesignerCanvas />
      </a-tab-pane>
      <a-tab-pane key="migrations" :tab="t('dynamicWorkbench.tabMigrations')">
        <DynamicTableDesignPage />
      </a-tab-pane>
    </a-tabs>
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import DynamicRecordsNativePage from "@/pages/dynamic/DynamicRecordsNativePage.vue";
import DynamicTableDesignPage from "@/pages/dynamic/DynamicTableDesignPage.vue";
import RelationDesigner from "@/components/designer/RelationDesigner.vue";
import ViewDesignerCanvas from "@/components/designer/ViewDesignerCanvas.vue";
import TransformDesignerCanvas from "@/components/designer/TransformDesignerCanvas.vue";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.params.tableKey === "string" ? route.params.tableKey : ""));
const activeTab = ref(typeof route.query.tab === "string" ? route.query.tab : "data");

const pageTitle = computed(() => `${t("dynamicWorkbench.title")} - ${tableKey.value}`);

watch(
  () => route.query.tab,
  tab => {
    if (typeof tab === "string" && tab !== activeTab.value) {
      activeTab.value = tab;
    }
  }
);

function handleTabChange(tab: string) {
  void router.replace({
    path: route.path,
    query: {
      ...route.query,
      tab
    }
  });
}
</script>

<style scoped>
.page-card {
  height: 100%;
}
</style>
