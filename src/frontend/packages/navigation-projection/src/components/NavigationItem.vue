<template>
  <button
    type="button"
    class="nav-projection-item"
    :class="{ 'nav-projection-item--active': active }"
    @click="$emit('navigate', item.path)"
  >
    <span class="nav-projection-item__title">{{ item.title }}</span>
    <span v-if="item.permissionCode" class="nav-projection-item__permission">
      {{ item.permissionCode }}
    </span>
  </button>
</template>

<script setup lang="ts">
import type { NavigationProjectionItem } from "../types/index";

defineProps<{
  item: NavigationProjectionItem;
  active?: boolean;
}>();

defineEmits<{
  navigate: [path: string];
}>();
</script>

<style scoped>
.nav-projection-item {
  width: 100%;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 10px;
}

.nav-projection-item:hover {
  background: rgba(79, 70, 229, 0.08);
}

.nav-projection-item--active {
  background: rgba(79, 70, 229, 0.14);
  color: #4338ca;
}

.nav-projection-item__title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.nav-projection-item__permission {
  font-size: 11px;
  opacity: 0.7;
  margin-left: 8px;
}
</style>
