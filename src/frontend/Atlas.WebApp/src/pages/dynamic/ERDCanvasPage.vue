<template>
  <a-card :title="t('erd.pageTitle')" class="page-card" :body-style="{ height: 'calc(100vh - 150px)', padding: 0 }">
    <template #extra>
      <a-space>
        <a-button @click="router.back()">{{ t('common.back') }}</a-button>
      </a-space>
    </template>
    <RelationDesigner v-if="appId" :app-id="appId" :initial-view-id="initialViewId" style="height:100%;overflow:auto" />
  </a-card>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import RelationDesigner from '@/components/designer/RelationDesigner.vue';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const appId = computed(() => route.params.appId as string);
const initialViewId = computed(() =>
  typeof route.query.viewId === "string" && route.query.viewId.trim() ? route.query.viewId : undefined
);
</script>

<style scoped>
.page-card {
  height: 100%;
}
</style>
