<template>
  <a-tag :color="color">{{ text }}</a-tag>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  status: "success" | "processing" | "warning" | "error" | "default";
  text: string;
}>();

const color = computed(() => {
  switch (props.status) {
    case "success":
      return "success";
    case "processing":
      return "processing";
    case "warning":
      return "warning";
    case "error":
      return "error";
    default:
      return "default";
  }
});
</script>
