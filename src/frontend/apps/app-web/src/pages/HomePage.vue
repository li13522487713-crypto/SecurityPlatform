<template>
  <div class="app-home">
    <a-result
      status="info"
      :title="t('route.home')"
      sub-title="Atlas AppWeb — Application Runtime Host"
    />
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
const { t } = useI18n();
</script>

<style scoped>
.app-home {
  min-height: 60vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
