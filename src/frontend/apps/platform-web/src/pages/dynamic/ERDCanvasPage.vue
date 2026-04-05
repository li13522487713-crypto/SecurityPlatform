<template>
  <a-card :title="t('erd.pageTitle')" class="page-card" :body-style="{ minHeight: '560px' }">
    <template #extra>
      <a-space>
        <a-button @click="router.back()">{{ t('common.back') }}</a-button>
      </a-space>
    </template>
    <RelationDesigner v-if="appId" :app-id="appId" :initial-view-id="initialViewId" />
    <a-empty v-else :description="t('dynamic.selectAppFirst')" />
  </a-card>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import RelationDesigner from "@/components/designer/RelationDesigner.vue";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const initialViewId = computed(() =>
  typeof route.query.viewId === "string" && route.query.viewId.trim() ? route.query.viewId : undefined
);
</script>

<style scoped>
.page-card {
  height: 100%;
}
</style>
