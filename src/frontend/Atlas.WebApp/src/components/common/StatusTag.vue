<template>
  <a-tag :color="color">{{ label }}</a-tag>
</template>

<script setup lang="ts">
import { computed } from "vue";

export interface StatusOption {
  value: unknown;
  label: string;
  color: string;
}

const props = defineProps<{
  /** Current status value */
  value: unknown;
  /** Map of status options: value -> { label, color } */
  options: StatusOption[];
  /** Fallback label when no match is found */
  fallbackLabel?: string;
  /** Fallback color when no match is found */
  fallbackColor?: string;
}>();

const matched = computed(() =>
  props.options.find((opt) => opt.value === props.value)
);

const label = computed(() => matched.value?.label ?? props.fallbackLabel ?? "未知");
const color = computed(() => matched.value?.color ?? props.fallbackColor ?? "default");
</script>
