<template>
  <div class="app-settings">
    <a-page-header :title="t('settings.pageTitle')" />
    <a-card>
      <a-empty :description="t('settings.placeholder')" />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

const { t } = useI18n();
</script>

<style scoped>
.app-settings {
  max-width: 1200px;
}
</style>
