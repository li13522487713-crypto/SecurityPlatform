<template>
  <div class="console-page">
    <AppListPage />
  </div>
</template>

<script setup lang="ts">
import AppListPage from "@/pages/lowcode/AppListPage.vue";
</script>

<style scoped>
.console-page {
  min-height: calc(100vh - 88px);
}
</style>
