<template>
  <a-card :title="pageTitle" class="page-card">
    <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
      <a-tab-pane key="data" :tab="t('dynamicWorkbench.tabData')">
        <component :is="tabComponents.data" />
      </a-tab-pane>
      <a-tab-pane key="fields" :tab="t('dynamicWorkbench.tabFields')">
        <component :is="tabComponents.fields" />
      </a-tab-pane>
      <a-tab-pane key="relations" :tab="t('dynamicWorkbench.tabRelations')">
        <component :is="tabComponents.relations" :app-id="appId" />
      </a-tab-pane>
      <a-tab-pane key="views" :tab="t('dynamicWorkbench.tabViews')">
        <component :is="tabComponents.views" :app-id="appId" />
      </a-tab-pane>
      <a-tab-pane key="transform" :tab="t('dynamicWorkbench.tabTransform')">
        <component :is="tabComponents.transform" />
      </a-tab-pane>
      <a-tab-pane key="migrations" :tab="t('dynamicWorkbench.tabMigrations')">
        <component :is="tabComponents.migrations" />
      </a-tab-pane>
    </a-tabs>
  </a-card>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const DataTab = defineAsyncComponent(() => import("@/pages/dynamic/DynamicRecordsNativePage.vue"));
const FieldsTab = defineAsyncComponent(() => import("@/pages/dynamic/DynamicTableDesignPage.vue"));
const RelationsTab = defineAsyncComponent(() => import("@/components/designer/RelationDesigner.vue"));
const ViewsTab = defineAsyncComponent(() => import("@/components/designer/ViewDesignerCanvas.vue"));
const TransformTab = defineAsyncComponent(() => import("@/components/designer/TransformDesignerCanvas.vue"));

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.params.tableKey === "string" ? route.params.tableKey : ""));
const activeTab = ref(typeof route.query.tab === "string" ? route.query.tab : "data");
const tabComponents = {
  data: DataTab,
  fields: FieldsTab,
  relations: RelationsTab,
  views: ViewsTab,
  transform: TransformTab,
  migrations: FieldsTab
} as const;

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
