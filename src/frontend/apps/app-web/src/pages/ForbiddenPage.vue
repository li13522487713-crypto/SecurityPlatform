<template>
  <div class="forbidden-page">
    <a-result
      status="403"
      :title="t('error.forbidden')"
      :sub-title="t('error.forbiddenDesc')"
    >
      <template #extra>
        <a-button type="primary" @click="goBack">
          {{ t("error.goBack") }}
        </a-button>
        <a-button @click="goHome">
          {{ t("error.goHome") }}
        </a-button>
      </template>
    </a-result>
  </div>
</template>

<script setup lang="ts">
import { useRouter, useRoute } from "vue-router";
import { useI18n } from "vue-i18n";

const { t } = useI18n();
const router = useRouter();
const route = useRoute();

function goBack() {
  if (window.history.length > 1) {
    router.back();
  } else {
    goHome();
  }
}

function goHome() {
  const appKey = typeof route.params.appKey === "string" ? route.params.appKey : "";
  if (appKey) {
    void router.push(`/apps/${encodeURIComponent(appKey)}/dashboard`);
  } else {
    void router.push("/");
  }
}
</script>

<style scoped>
.forbidden-page {
  min-height: 60vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
