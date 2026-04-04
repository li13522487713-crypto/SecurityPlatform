<template>
  <div style="display: flex; align-items: center; justify-content: center; height: 100vh; flex-direction: column; gap: 16px;">
    <h2>{{ t("authPage.registerNow") }}</h2>
    <p style="color: #999;">Coming soon...</p>
    <router-link to="/login">← {{ t("auth.login") }}</router-link>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
const { t } = useI18n();
</script>
