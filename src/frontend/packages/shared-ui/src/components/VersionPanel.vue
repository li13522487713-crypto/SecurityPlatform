<template>
  <a-list size="small" :data-source="items" bordered>
    <template #renderItem="{ item }">
      <a-list-item>
        <a-space direction="vertical" :size="2">
          <strong>{{ item.versionNo }}</strong>
          <span>{{ item.createdAt }}</span>
        </a-space>
      </a-list-item>
    </template>
  </a-list>
</template>

<script setup lang="ts">
import type { VersionInfo } from "@atlas/shared-kernel";

defineProps<{
  items: VersionInfo[];
}>();
</script>
