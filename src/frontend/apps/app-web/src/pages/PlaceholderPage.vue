<template>
  <div class="placeholder-page">
    <div class="placeholder-header">
      <div>
        <h1 class="placeholder-title">{{ title }}</h1>
        <p class="placeholder-subtitle">{{ t('placeholder.subtitle', { title }) }}</p>
      </div>
      <div class="placeholder-actions">
        <button class="placeholder-btn">{{ t('placeholder.newProject') }}</button>
      </div>
    </div>

    <div class="placeholder-body">
      <div class="placeholder-icon-wrap">
        <AppstoreOutlined class="placeholder-icon" />
      </div>
      <h2 class="placeholder-welcome">{{ t('placeholder.welcome', { title }) }}</h2>
      <p class="placeholder-desc">{{ t('placeholder.desc') }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import { AppstoreOutlined } from "@ant-design/icons-vue";

const { t } = useI18n();

defineProps<{
  title: string;
}>();
</script>

<style scoped>
.placeholder-page {
  max-width: 1280px;
  margin: 0 auto;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.placeholder-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24px;
}

.placeholder-title {
  font-size: 24px;
  font-weight: 700;
  color: #111827;
  letter-spacing: -0.025em;
  margin: 0;
}

.placeholder-subtitle {
  font-size: 14px;
  color: #6b7280;
  margin: 4px 0 0 0;
}

.placeholder-btn {
  padding: 8px 16px;
  background: #4f46e5;
  color: #fff;
  border: none;
  border-radius: 12px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s;
  box-shadow: 0 1px 3px rgba(79, 70, 229, 0.3);
}

.placeholder-btn:hover {
  background: #4338ca;
}

.placeholder-body {
  flex: 1;
  background: #fff;
  border-radius: 16px;
  border: 1px solid rgba(243, 244, 246, 0.5);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 48px;
}

.placeholder-icon-wrap {
  width: 80px;
  height: 80px;
  background: #eef2ff;
  border-radius: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 24px;
}

.placeholder-icon {
  font-size: 40px;
  color: #6366f1;
}

.placeholder-welcome {
  font-size: 20px;
  font-weight: 600;
  color: #111827;
  margin: 0 0 8px 0;
}

.placeholder-desc {
  color: #6b7280;
  max-width: 448px;
  margin: 0;
}
</style>
