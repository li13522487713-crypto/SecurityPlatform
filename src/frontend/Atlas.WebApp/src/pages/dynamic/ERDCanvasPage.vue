<template>
  <a-card title="ERD 实体设计器" class="page-card" :body-style="{ height: 'calc(100vh - 150px)', padding: 0 }">
    <template #extra>
      <a-button @click="router.back()">返回</a-button>
    </template>
    <ERDCanvas :app-id="appId" v-if="appId" />
  </a-card>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import ERDCanvas from '@/components/designer/ERDCanvas.vue';

const route = useRoute();
const router = useRouter();
const appId = computed(() => route.params.appId as string);
</script>

<style scoped>
.page-card {
  height: 100%;
}
</style>
