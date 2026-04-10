<template>
  <DatabaseNodeForm :configs="configs" mode="customSql" @change="$emit('change')" />
</template>

<script setup lang="ts">
import DatabaseNodeForm from "../forms/DatabaseNodeForm.vue";
defineProps<{ configs: Record<string, unknown> }>();
defineEmits<{ (e: "change"): void }>();
</script>
