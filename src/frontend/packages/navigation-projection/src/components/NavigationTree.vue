<template>
  <div class="nav-projection-tree">
    <NavigationGroup
      v-for="group in groups"
      :key="group.groupKey"
      :group="group"
      :current-path="currentPath"
      @navigate="$emit('navigate', $event)"
    />
    <div v-if="groups.length === 0" class="nav-projection-tree__empty">
      {{ emptyText }}
    </div>
  </div>
</template>

<script setup lang="ts">
import NavigationGroup from "./NavigationGroup.vue";
import type { NavigationProjectionGroup } from "../types/index";

withDefaults(defineProps<{
  groups: NavigationProjectionGroup[];
  currentPath?: string;
  emptyText?: string;
}>(), {
  emptyText: "No navigation items."
});

defineEmits<{
  navigate: [path: string];
}>();
</script>

<style scoped>
.nav-projection-tree {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.nav-projection-tree__empty {
  opacity: 0.7;
  font-size: 13px;
  padding: 8px 10px;
}
</style>
