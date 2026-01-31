<template>
  <a-card :title="pageTitle" class="page-card">
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" />
      <a-empty v-else-if="!loading" description="未找到页面配置" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { message } from "ant-design-vue";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import { getDynamicAmisSchema } from "@/services/dynamic-tables";
import type { JsonValue } from "@/types/api";

const loading = ref(false);
const schema = ref<JsonValue | null>(null);
const pageTitle = ref("动态表管理");

const loadSchema = async () => {
  loading.value = true;
  try {
    schema.value = await getDynamicAmisSchema("list");
  } catch (error) {
    schema.value = null;
    message.error((error as Error).message || "加载页面失败");
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  loadSchema();
});
</script>
