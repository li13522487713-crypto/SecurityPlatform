<template>
  <RelationDesigner :app-id="appId" />
</template>

<script setup lang="ts">
import RelationDesigner from "./RelationDesigner.vue";

defineProps<{
  appId: string;
}>();
</script>
