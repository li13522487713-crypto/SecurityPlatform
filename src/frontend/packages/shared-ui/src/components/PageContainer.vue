<template>
  <section class="page-container">
    <header v-if="$slots.title || title" class="page-container__header">
      <slot name="title">
        <h2 class="page-container__title">{{ title }}</h2>
      </slot>
      <slot name="actions"></slot>
    </header>
    <div class="page-container__body">
      <slot></slot>
    </div>
  </section>
</template>

<script setup lang="ts">
defineProps<{
  title?: string;
}>();
</script>

<style scoped>
.page-container {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.page-container__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.page-container__title {
  margin: 0;
  font-size: 18px;
}
</style>
