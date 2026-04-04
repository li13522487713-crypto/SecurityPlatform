<template>
  <div style="padding: 24px;">
    <a-card>
      <a-result status="info" :title="pageTitle" sub-title="Runtime page rendering will be migrated here." />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useI18n } from "vue-i18n";

const { t } = useI18n();
const route = useRoute();
const pageKey = computed(() => String(route.params.pageKey ?? ""));
const pageTitle = computed(() => `${t("appRuntime.pageTitle")} — ${pageKey.value}`);
</script>
