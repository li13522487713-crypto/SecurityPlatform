<template>
  <div class="entry-gateway" data-testid="app-entry-gateway-page">
    <a-spin :spinning="loading" data-testid="app-entry-gateway-spin">
      <a-result
        v-if="errorMessage"
        data-testid="app-entry-gateway-warning"
        status="warning"
        :title="t('appEntry.unavailable')"
        :sub-title="errorMessage"
      />
      <a-result
        v-else
        data-testid="app-entry-gateway-info"
        status="info"
        :title="t('appEntry.entering')"
        :sub-title="t('appEntry.resolving')"
      />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { getRuntimeMenu } from "@/services/api-runtime";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const loading = ref(true);
const errorMessage = ref("");

async function enterApp() {
  const appKey = String(route.params.appKey ?? "");
  if (!appKey) {
    errorMessage.value = t("appEntry.missingAppKey");
    loading.value = false;
    return;
  }

  try {
    const menu = await getRuntimeMenu(appKey);
    const firstPage = menu.items[0];
    if (!firstPage) {
      errorMessage.value = t("appEntry.noPages");
      loading.value = false;
      return;
    }

    await router.replace({
      path: `/apps/${encodeURIComponent(appKey)}/r/${encodeURIComponent(firstPage.pageKey)}`,
      query: route.query
    });
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : t("appEntry.enterFailed");
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
