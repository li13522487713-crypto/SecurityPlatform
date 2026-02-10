<template>
  <a-card :title="pageTitle" class="page-card">
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" />
      <a-empty v-else-if="!loading" description="未找到页面配置" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import { getAmisPageDefinition } from "@/services/api";
import type { AmisPageDefinition } from "@/types/api";
import type { AmisSchema } from "@/types/amis";

const route = useRoute();
const loading = ref(false);
const schema = ref<AmisSchema | null>(null);
const pageTitle = ref("系统管理");

const schemaKey = computed(() => {
  const key = route.meta.amisKey;
  return typeof key === "string" ? key : "";
});

const loadSchema = async () => {
  if (!schemaKey.value) {
    schema.value = null;
    return;
  }

  loading.value = true;
  try {
    const definition: AmisPageDefinition = await getAmisPageDefinition(schemaKey.value);
    schema.value = definition.schema as AmisSchema;
    pageTitle.value = definition.title;
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

watch(schemaKey, () => {
  loadSchema();
});
</script>
