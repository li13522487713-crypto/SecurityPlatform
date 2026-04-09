<template>
  <section class="nav-projection-group">
    <header class="nav-projection-group__title">{{ group.groupTitle }}</header>
    <div class="nav-projection-group__items">
      <NavigationItem
        v-for="item in sortedItems"
        :key="item.key"
        :item="item"
        :active="currentPath === item.path"
        @navigate="$emit('navigate', $event)"
      />
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { NavigationProjectionGroup } from "../types/index";
import NavigationItem from "./NavigationItem.vue";

const props = defineProps<{
  group: NavigationProjectionGroup;
  currentPath?: string;
}>();

defineEmits<{
  navigate: [path: string];
}>();

const sortedItems = computed(() =>
  [...props.group.items].sort((a, b) => a.order - b.order)
);
</script>

<style scoped>
.nav-projection-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.nav-projection-group__title {
  font-size: 12px;
  font-weight: 600;
  opacity: 0.68;
  padding: 0 8px;
  text-transform: uppercase;
}

.nav-projection-group__items {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
</style>
