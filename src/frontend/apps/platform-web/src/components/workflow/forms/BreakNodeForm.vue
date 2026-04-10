<template>
  <LoopControlNodeForm :configs="configs" mode="break" @change="$emit('change')" />
</template>

<script setup lang="ts">
import LoopControlNodeForm from "@/components/workflow/forms/LoopControlNodeForm.vue";

defineProps<{
  configs: Record<string, unknown>;
}>();

defineEmits<{
  (e: "change"): void;
}>();
</script>
