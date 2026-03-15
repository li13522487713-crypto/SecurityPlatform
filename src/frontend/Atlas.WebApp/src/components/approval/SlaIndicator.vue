<template>
  <span :class="remainingMinutes != null && remainingMinutes >= 0 ? 'sla-ok' : 'sla-error'">
    {{ formattedText }}
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  remainingMinutes?: number | null;
}>();

const formattedText = computed(() => {
  if (props.remainingMinutes == null) {
    return '';
  }
  const value = props.remainingMinutes;
  const abs = Math.abs(value);
  if (abs >= 60) {
    const hours = Math.floor(abs / 60);
    const minutes = abs % 60;
    return value >= 0 ? `剩 ${hours}h${minutes}m` : `超 ${hours}h${minutes}m`;
  }
  return value >= 0 ? `剩 ${abs}m` : `超 ${abs}m`;
});
</script>

<style scoped>
.sla-ok { color: var(--color-success); }
.sla-error { color: var(--color-error-text); margin-left: 4px; }
</style>
