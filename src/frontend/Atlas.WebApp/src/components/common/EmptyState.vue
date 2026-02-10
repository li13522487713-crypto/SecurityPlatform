<template>
  <div class="empty-state">
    <a-empty :description="description">
      <template v-if="$slots.default" #default>
        <slot />
      </template>
    </a-empty>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  description?: string;
}>();
</script>

<style scoped>
.empty-state {
  padding: var(--spacing-xxl, 48px) 0;
  display: flex;
  justify-content: center;
  align-items: center;
}
</style>
