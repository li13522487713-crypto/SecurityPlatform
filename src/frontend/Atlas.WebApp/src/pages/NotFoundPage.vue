<template>
  <div class="not-found">
    <a-result status="404" :title="t('pages.notFound.title')" :sub-title="t('pages.notFound.subtitle')">
      <template #extra>
        <a-button type="primary" @click="goHome">{{ t("pages.notFound.backHome") }}</a-button>
      </template>
    </a-result>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";

const router = useRouter();
const { t } = useI18n();

const goHome = () => {
  router.push("/");
};
</script>

<style scoped>
.not-found {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: calc(100vh - var(--header-height, 56px) - 34px);
}
</style>
