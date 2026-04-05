<template>
  <div class="semantic-bridge-page">
    <a-card :bordered="false" class="semantic-bridge-card">
      <a-space direction="vertical" size="middle">
        <a-typography-title :level="4" style="margin: 0;">
          {{ t(titleKey) }}
        </a-typography-title>
        <a-typography-paragraph type="secondary" style="margin: 0;">
          {{ t(descriptionKey) }}
        </a-typography-paragraph>
        <a-space wrap>
          <a-button type="primary" @click="go(primaryPath)">{{ t(primaryActionKey) }}</a-button>
          <a-button @click="go(secondaryPath)">{{ t(secondaryActionKey) }}</a-button>
        </a-space>
      </a-space>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";

defineProps<{
  titleKey: string;
  descriptionKey: string;
  primaryActionKey: string;
  primaryPath: string;
  secondaryActionKey: string;
  secondaryPath: string;
}>();

const { t } = useI18n();
const router = useRouter();

function go(path: string) {
  void router.push(path);
}
</script>

<style scoped>
.semantic-bridge-page {
  padding: 24px;
}

.semantic-bridge-card {
  border-radius: 12px;
}
</style>
