<template>
  <div class="core-apps-card">
    <div class="core-apps-card__header">
      <h2 class="core-apps-card__title">{{ t("home.coreAppsTitle") }}</h2>
      <a class="core-apps-card__link" @click="$router.push('/console/catalog')">
        {{ t("home.appMarket") }}
        <RightOutlined class="core-apps-card__link-icon" />
      </a>
    </div>
    <div class="core-apps-card__grid">
      <button
        v-for="app in appTiles"
        :key="app.path"
        type="button"
        class="app-tile"
        @click="$router.push(app.path)"
      >
        <div class="app-tile__icon-wrapper" :style="app.iconStyle">
          <component :is="app.icon" class="app-tile__icon" :style="{ color: app.iconColor }" />
        </div>
        <div class="app-tile__text">
          <h4 class="app-tile__label">{{ app.label }}</h4>
          <p class="app-tile__desc">{{ app.desc }}</p>
        </div>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { Component } from "vue";
import { useI18n } from "vue-i18n";
import {
  UserOutlined,
  SafetyCertificateOutlined,
  PieChartOutlined,
  DatabaseOutlined,
  ApartmentOutlined,
  AppstoreOutlined,
  FileSearchOutlined,
  RobotOutlined,
  RightOutlined,
} from "@ant-design/icons-vue";

const { t } = useI18n();

interface AppTileConfig {
  icon: Component;
  labelKey: string;
  descKey: string;
  color: string;
  path: string;
}

const colorMap: Record<string, { bg: string; text: string; ring: string }> = {
  blue: { bg: "#eff6ff", text: "#2563eb", ring: "rgba(59, 130, 246, 0.2)" },
  indigo: { bg: "#eef2ff", text: "#4f46e5", ring: "rgba(99, 102, 241, 0.2)" },
  teal: { bg: "#f0fdfa", text: "#0d9488", ring: "rgba(20, 184, 166, 0.2)" },
  emerald: { bg: "#ecfdf5", text: "#059669", ring: "rgba(16, 185, 129, 0.2)" },
  orange: { bg: "#fff7ed", text: "#ea580c", ring: "rgba(249, 115, 22, 0.2)" },
  purple: { bg: "#faf5ff", text: "#9333ea", ring: "rgba(147, 51, 234, 0.2)" },
  sky: { bg: "#f0f9ff", text: "#0284c7", ring: "rgba(14, 165, 233, 0.2)" },
  red: { bg: "#fef2f2", text: "#dc2626", ring: "rgba(239, 68, 68, 0.2)" },
};

const appTilesConfig: AppTileConfig[] = [
  { icon: UserOutlined, labelKey: "home.appUsersLabel", descKey: "home.appUsersDesc", color: "blue", path: "/settings/org/users" },
  { icon: SafetyCertificateOutlined, labelKey: "home.appRolesLabel", descKey: "home.appRolesDesc", color: "indigo", path: "/settings/auth/roles" },
  { icon: PieChartOutlined, labelKey: "home.appProjectsLabel", descKey: "home.appProjectsDesc", color: "teal", path: "/settings/projects" },
  { icon: DatabaseOutlined, labelKey: "home.appDatasourcesLabel", descKey: "home.appDatasourcesDesc", color: "emerald", path: "/settings/system/datasources" },
  { icon: ApartmentOutlined, labelKey: "home.appWorkflowLabel", descKey: "home.appWorkflowDesc", color: "orange", path: "/approval/flows" },
  { icon: AppstoreOutlined, labelKey: "home.appAssetsLabel", descKey: "home.appAssetsDesc", color: "purple", path: "/assets" },
  { icon: FileSearchOutlined, labelKey: "home.appAuditLabel", descKey: "home.appAuditDesc", color: "sky", path: "/console/audit" },
  { icon: RobotOutlined, labelKey: "home.appAILabel", descKey: "home.appAIDesc", color: "red", path: "/ai/agents" },
];

const appTiles = computed(() =>
  appTilesConfig.map((cfg) => {
    const c = colorMap[cfg.color] ?? colorMap.blue;
    return {
      icon: cfg.icon,
      label: t(cfg.labelKey),
      desc: t(cfg.descKey),
      path: cfg.path,
      iconColor: c.text,
      iconStyle: {
        background: c.bg,
        boxShadow: `0 0 0 1px ${c.ring}`,
      },
    };
  })
);
</script>

<style scoped>
.core-apps-card {
  background: #fff;
  border-radius: 16px;
  padding: 20px;
  border: 1px solid rgba(229, 231, 235, 0.6);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
}

.core-apps-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.core-apps-card__title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #111827;
}

.core-apps-card__link {
  font-size: 14px;
  font-weight: 500;
  color: #4f46e5;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 2px;
  transition: color 0.15s;
}

.core-apps-card__link:hover {
  color: #4338ca;
}

.core-apps-card__link-icon {
  font-size: 12px;
}

.core-apps-card__grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.app-tile {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 12px;
  padding: 16px;
  border-radius: 12px;
  border: 1px solid #f3f4f6;
  background: rgba(249, 250, 251, 0.5);
  cursor: pointer;
  font: inherit;
  text-align: left;
  transition: all 0.2s ease;
  color: inherit;
  margin: 0;
}

.app-tile:hover {
  background: #fff;
  border-color: #e0e7ff;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
}

.app-tile__icon-wrapper {
  padding: 10px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: transform 0.2s ease;
}

.app-tile:hover .app-tile__icon-wrapper {
  transform: scale(1.1);
}

.app-tile__icon {
  font-size: 20px;
}

.app-tile__label {
  margin: 0 0 2px;
  font-size: 14px;
  font-weight: 600;
  color: #111827;
  transition: color 0.15s;
}

.app-tile:hover .app-tile__label {
  color: #4f46e5;
}

.app-tile__desc {
  margin: 0;
  font-size: 12px;
  color: #6b7280;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
}

@media (max-width: 768px) {
  .core-apps-card__grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
