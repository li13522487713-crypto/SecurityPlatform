<template>
  <a-dropdown trigger="click">
    <a-button type="text" class="locale-switch" :aria-label="t('layout.switchLanguage')">
      <a-space :size="4">
        <GlobalOutlined />
        <span>{{ currentLabel }}</span>
      </a-space>
    </a-button>
    <template #overlay>
      <a-menu :selected-keys="[currentLocale]">
        <a-menu-item
          v-for="locale in SUPPORTED_LOCALES"
          :key="locale"
          @click="switchLocale(locale)"
        >
          {{ getLocaleLabel(locale) }}
        </a-menu-item>
      </a-menu>
    </template>
  </a-dropdown>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { GlobalOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import { getActiveLocale, getLocaleLabel, setLocale, SUPPORTED_LOCALES, type SupportedLocale } from "@/i18n";

const { t } = useI18n();

const currentLocale = computed(() => getActiveLocale());
const currentLabel = computed(() => getLocaleLabel(currentLocale.value));

function switchLocale(locale: SupportedLocale) {
  if (locale === currentLocale.value) return;
  setLocale(locale);
}
</script>

<style scoped>
.locale-switch {
  color: var(--color-text-primary);
}
</style>
