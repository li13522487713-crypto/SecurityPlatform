<template>
  <div class="activity-item" :class="{ 'is-last': isLast }">
    <div class="activity-line" />
    <div class="activity-dot" :style="{ background: dotColor }" />
    <div class="activity-body">
      <div class="activity-main">
        <div>
          <p class="activity-title">{{ title }}</p>
          <span class="activity-user">{{ user }}</span>
        </div>
        <span class="activity-time">{{ time }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title: string;
  user: string;
  time: string;
  dotColor: string;
  isLast?: boolean;
}>();
</script>

<style scoped>
.activity-item {
  position: relative;
  padding-left: 32px;
  padding-bottom: 24px;
}

.activity-line {
  position: absolute;
  left: 0;
  top: 4px;
  bottom: 0;
  width: 1px;
  background: #f3f4f6;
}

.activity-item.is-last .activity-line {
  background: transparent;
}

.activity-dot {
  position: absolute;
  left: -6px;
  top: 4px;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  border: 2px solid #fff;
  z-index: 1;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
}

.activity-body {
  margin-top: -6px;
}

.activity-main {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.activity-title {
  font-size: 14px;
  font-weight: 500;
  color: #111827;
  margin: 0;
}

.activity-user {
  display: inline-block;
  margin-top: 4px;
  padding: 2px 6px;
  background: #f3f4f6;
  color: #4b5563;
  border-radius: 4px;
  font-size: 10px;
  font-weight: 500;
  line-height: 1;
}

.activity-time {
  font-size: 12px;
  color: #9ca3af;
  font-weight: 500;
  white-space: nowrap;
}
</style>
