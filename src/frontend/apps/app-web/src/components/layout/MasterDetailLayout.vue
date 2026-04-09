<template>
  <SharedMasterDetailLayout :detail-visible="detailVisible" :master-width="masterWidth">
    <template #master>
      <slot name="master"></slot>
    </template>
    <template #detail>
      <slot name="detail"></slot>
    </template>
  </SharedMasterDetailLayout>
</template>

<script setup lang="ts">
import { MasterDetailLayout as SharedMasterDetailLayout } from "@atlas/shared-ui";

withDefaults(
  defineProps<{
    detailVisible?: boolean;
    masterWidth?: number;
  }>(),
  {
    detailVisible: false,
    masterWidth: 380
  }
);
</script>
