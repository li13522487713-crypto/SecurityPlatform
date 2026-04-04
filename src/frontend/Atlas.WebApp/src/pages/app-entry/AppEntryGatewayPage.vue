<template>
  <div class="entry-gateway">
    <a-spin :spinning="loading">
      <a-result
        v-if="errorMessage"
        status="warning"
        title="入口暂不可用"
        :sub-title="errorMessage"
      />
      <a-result
        v-else
        status="info"
        title="正在进入应用"
        sub-title="正在解析默认页面和运行时入口。"
      />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getRuntimeMenu } from "@/services/runtime/runtime-api-core";

const route = useRoute();
const router = useRouter();
const loading = ref(true);
const errorMessage = ref("");

async function enterApp() {
  const appKey = String(route.params.appKey ?? "");
  if (!appKey) {
    errorMessage.value = "缺少应用标识。";
    loading.value = false;
    return;
  }

  try {
    const menu = await getRuntimeMenu(appKey);
    const firstPage = menu.items[0];
    if (!firstPage) {
      errorMessage.value = "应用尚未配置可访问页面。";
      loading.value = false;
      return;
    }

    await router.replace(`/app-host/${encodeURIComponent(appKey)}/r/${encodeURIComponent(firstPage.pageKey)}`);
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "进入应用失败";
    loading.value = false;
  }
}

onMounted(() => {
  void enterApp();
});
</script>

<style scoped>
.entry-gateway {
  min-height: 60vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
