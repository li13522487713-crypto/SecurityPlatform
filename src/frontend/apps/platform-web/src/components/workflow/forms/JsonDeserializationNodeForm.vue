<template>
  <JsonNodeForm :configs="configs" direction-default="deserialize" @change="$emit('change')" />
</template>

<script setup lang="ts">
import JsonNodeForm from "@/components/workflow/forms/JsonNodeForm.vue";

defineProps<{ configs: Record<string, unknown> }>();
defineEmits<{ (e: "change"): void }>();
</script>
