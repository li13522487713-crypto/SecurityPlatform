<template>
  <header class="app-header" data-testid="app-header">
    <div class="header-search-area">
      <div class="search-box">
        <SearchOutlined class="search-icon" />
        <input
          type="text"
          class="search-input"
          data-testid="app-header-search"
          :placeholder="t('header.searchPlaceholder')"
        />
        <kbd class="search-shortcut">⌘K</kbd>
      </div>
    </div>

    <div class="header-actions">
      <div class="tenant-badge" data-testid="app-header-tenant">
        <span class="tenant-dot"></span>
        <span class="tenant-label">{{ t('header.tenant') }}: {{ tenantDisplay }}</span>
      </div>

      <LocaleSwitch />

      <a-badge :count="unreadCount" :offset="[-2, 2]" :number-style="{ display: unreadCount > 0 ? '' : 'none' }">
        <button class="icon-btn" data-testid="app-header-notification" @click="$emit('notificationClick')">
          <BellOutlined />
          <span v-if="unreadCount > 0" class="notification-dot"></span>
        </button>
      </a-badge>

      <div class="header-divider"></div>

      <a-dropdown :trigger="['click']">
        <div class="user-area" data-testid="app-header-user-menu">
          <div class="user-avatar">
            <UserOutlined />
          </div>
          <span class="user-name">{{ displayName }}</span>
        </div>
        <template #overlay>
          <a-menu @click="(info: { key: string }) => $emit('userMenuClick', info)">
            <a-menu-item key="profile" data-testid="app-header-menu-profile">
              <UserOutlined />
              <span style="margin-left: 8px">{{ t('profile.title') }}</span>
            </a-menu-item>
            <a-menu-item key="changePassword" data-testid="app-header-menu-change-password">
              <LockOutlined />
              <span style="margin-left: 8px">{{ t('profile.changePassword') }}</span>
            </a-menu-item>
            <a-menu-divider />
            <a-menu-item key="logout" data-testid="app-header-menu-logout">
              <LogoutOutlined />
              <span style="margin-left: 8px">{{ t('auth.logout') }}</span>
            </a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
    </div>
  </header>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import {
  SearchOutlined,
  BellOutlined,
  UserOutlined,
  LockOutlined,
  LogoutOutlined
} from "@ant-design/icons-vue";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";

const { t } = useI18n();

defineProps<{
  displayName: string;
  unreadCount: number;
  tenantDisplay: string;
}>();

defineEmits<{
  notificationClick: [];
  userMenuClick: [info: { key: string }];
}>();
</script>

<style scoped>
.app-header {
  height: 64px;
  flex-shrink: 0;
  background: #fff;
  border-bottom: 1px solid #f3f4f6;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  z-index: 10;
  position: sticky;
  top: 0;
}

.header-search-area {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 16px;
  max-width: 576px;
}

.search-box {
  position: relative;
  width: 100%;
}

.search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 16px;
  color: #9ca3af;
  pointer-events: none;
}

.search-input {
  width: 100%;
  padding: 8px 48px 8px 40px;
  border: none;
  border-radius: 12px;
  background: #f9fafb;
  font-size: 14px;
  color: #111827;
  outline: none;
  transition: all 0.2s;
}

.search-input::placeholder {
  color: #9ca3af;
}

.search-input:focus {
  background: #fff;
  box-shadow: 0 0 0 2px #4f46e5 inset, 0 1px 3px rgba(0, 0, 0, 0.06);
}

.search-shortcut {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  display: inline-flex;
  align-items: center;
  padding: 1px 6px;
  border: 1px solid #e5e7eb;
  border-radius: 4px;
  font-family: system-ui, sans-serif;
  font-size: 12px;
  color: #9ca3af;
  pointer-events: none;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 24px;
}

.tenant-badge {
  display: flex;
  align-items: center;
  gap: 8px;
  background: #eef2ff;
  padding: 6px 12px;
  border-radius: 9999px;
  border: 1px solid rgba(99, 102, 241, 0.15);
}

.tenant-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #6366f1;
  flex-shrink: 0;
}

.tenant-label {
  font-size: 14px;
  font-weight: 500;
  color: #4338ca;
  white-space: nowrap;
}

.icon-btn {
  position: relative;
  background: none;
  border: none;
  padding: 4px;
  cursor: pointer;
  color: #6b7280;
  font-size: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: color 0.15s;
}

.icon-btn:hover {
  color: #111827;
}

.notification-dot {
  position: absolute;
  top: 0;
  right: 0;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #ef4444;
  border: 2px solid #fff;
}

.header-divider {
  width: 1px;
  height: 24px;
  background: #e5e7eb;
}

.user-area {
  display: flex;
  align-items: center;
  gap: 12px;
  cursor: pointer;
}

.user-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: #f3f4f6;
  border: 1px solid #e5e7eb;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  color: #6b7280;
}

.user-name {
  font-size: 14px;
  font-weight: 500;
  color: #374151;
}
</style>
