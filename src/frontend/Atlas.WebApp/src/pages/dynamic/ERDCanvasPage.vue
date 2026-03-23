<template>
  <a-card :title="t('erd.pageTitle')" class="page-card" :body-style="{ height: 'calc(100vh - 150px)', padding: 0 }">
    <template #extra>
      <a-space>
        <a-radio-group v-model:value="viewMode" button-style="solid" size="small">
          <a-radio-button value="erd">{{ t('erd.modeErd') }}</a-radio-button>
          <a-radio-button value="relation">{{ t('erd.modeRelation') }}</a-radio-button>
        </a-radio-group>
        <a-button @click="router.back()">{{ t('common.back') }}</a-button>
      </a-space>
    </template>
    <ERDCanvas v-if="appId && viewMode === 'erd'" :app-id="appId" />
    <RelationDesigner v-else-if="appId && viewMode === 'relation'" :app-id="appId" style="height:100%;overflow:auto" />
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import ERDCanvas from '@/components/designer/ERDCanvas.vue';
import RelationDesigner from '@/components/designer/RelationDesigner.vue';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const appId = computed(() => route.params.appId as string);
const viewMode = ref<'erd' | 'relation'>('erd');
</script>

<style scoped>
.page-card {
  height: 100%;
}
</style>
