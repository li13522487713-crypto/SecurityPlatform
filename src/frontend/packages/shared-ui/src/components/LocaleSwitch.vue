<template>
  <a-dropdown trigger="click">
    <a-button type="text" class="locale-switch" :aria-label="ariaLabel">
      <a-space :size="4">
        <GlobalOutlined />
        <span>{{ currentLabel }}</span>
      </a-space>
    </a-button>
    <template #overlay>
      <a-menu :selected-keys="[currentLocale]">
        <a-menu-item
          v-for="locale in supportedLocales"
          :key="locale"
          @click="onSelectLocale(locale)"
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

const props = defineProps<{
  currentLocale: string;
  supportedLocales: readonly string[];
  ariaLabel: string;
  getLocaleLabel: (locale: string) => string;
}>();

const emit = defineEmits<{
  select: [locale: string];
}>();

const currentLabel = computed(() => props.getLocaleLabel(props.currentLocale));

function onSelectLocale(locale: string) {
  if (locale === props.currentLocale) return;
  emit("select", locale);
}
</script>

<style scoped>
.locale-switch {
  color: var(--color-text-primary);
}
</style>
