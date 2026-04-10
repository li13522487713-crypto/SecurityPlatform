<template>
  <DatabaseNodeForm :configs="configs" mode="query" @change="$emit('change')" />
</template>

<script setup lang="ts">
import DatabaseNodeForm from "@/components/workflow/forms/DatabaseNodeForm.vue";
defineProps<{ configs: Record<string, unknown> }>();
defineEmits<{ (e: "change"): void }>();
</script>
