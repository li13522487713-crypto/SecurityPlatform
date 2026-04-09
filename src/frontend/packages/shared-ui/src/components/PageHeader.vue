<template>
  <div class="page-header">
    <div>
      <h3 class="page-header__title">{{ title }}</h3>
      <p v-if="description" class="page-header__description">{{ description }}</p>
    </div>
    <div class="page-header__actions">
      <slot name="actions"></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title: string;
  description?: string;
}>();
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
}

.page-header__title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
}

.page-header__description {
  margin: 6px 0 0;
  color: #666;
  font-size: 13px;
}
</style>
