<template>
  <div class="workflow-databases-page">
    <a-card :bordered="false" class="hero-card">
      <div class="hero-content">
        <div class="hero-copy">
          <div class="hero-badge">{{ t("workflowDatabase.badge") }}</div>
          <h1 class="hero-title">{{ t("workflowDatabase.pageTitle") }}</h1>
          <p class="hero-desc">{{ t("workflowDatabase.pageDesc") }}</p>
        </div>
        <div class="hero-actions">
          <a-button type="primary" size="large" @click="openRoute('app-workflows')">
            {{ t("workflowDatabase.openWorkflows") }}
          </a-button>
          <a-button size="large" @click="openRoute('app-model-configs')">
            {{ t("workflowDatabase.openModelConfigs") }}
          </a-button>
        </div>
      </div>
    </a-card>

    <a-row :gutter="[16, 16]">
      <a-col v-for="card in cards" :key="card.key" :xs="24" :md="12">
        <a-card :bordered="false" class="resource-card" hoverable @click="openRoute(card.routeName)">
          <div class="resource-card__header">
            <div class="resource-card__icon">
              <component :is="card.icon" />
            </div>
            <a-tag v-if="card.highlight" color="blue">{{ t("workflowDatabase.recommended") }}</a-tag>
          </div>
          <h2 class="resource-card__title">{{ card.title }}</h2>
          <p class="resource-card__desc">{{ card.description }}</p>
          <a-button type="link" class="resource-card__action" @click.stop="openRoute(card.routeName)">
            {{ card.actionText }}
          </a-button>
        </a-card>
      </a-col>
    </a-row>

    <a-card :bordered="false" class="guide-card">
      <template #title>{{ t("workflowDatabase.guideTitle") }}</template>
      <div class="guide-list">
        <div v-for="guide in guides" :key="guide.key" class="guide-item">
          <div class="guide-item__index">{{ guide.index }}</div>
          <div class="guide-item__body">
            <div class="guide-item__title">{{ guide.title }}</div>
            <div class="guide-item__desc">{{ guide.description }}</div>
          </div>
        </div>
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { Component } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { ControlOutlined, DatabaseOutlined, PartitionOutlined, RobotOutlined } from "@ant-design/icons-vue";

type RouteName = "app-workflows" | "app-model-configs" | "app-data" | "app-logic-flow";

type ResourceCard = {
  key: string;
  title: string;
  description: string;
  actionText: string;
  routeName: RouteName;
  icon: Component;
  highlight?: boolean;
};

const route = useRoute();
const router = useRouter();
const { t } = useI18n();

const appKey = computed(() => String(route.params.appKey ?? ""));

const cards = computed<ResourceCard[]>(() => [
  {
    key: "models",
    title: t("workflowDatabase.cards.models.title"),
    description: t("workflowDatabase.cards.models.desc"),
    actionText: t("workflowDatabase.cards.models.action"),
    routeName: "app-model-configs",
    icon: RobotOutlined,
    highlight: true
  },
  {
    key: "workflows",
    title: t("workflowDatabase.cards.workflows.title"),
    description: t("workflowDatabase.cards.workflows.desc"),
    actionText: t("workflowDatabase.cards.workflows.action"),
    routeName: "app-workflows",
    icon: PartitionOutlined
  },
  {
    key: "tables",
    title: t("workflowDatabase.cards.tables.title"),
    description: t("workflowDatabase.cards.tables.desc"),
    actionText: t("workflowDatabase.cards.tables.action"),
    routeName: "app-data",
    icon: DatabaseOutlined
  },
  {
    key: "logic",
    title: t("workflowDatabase.cards.logic.title"),
    description: t("workflowDatabase.cards.logic.desc"),
    actionText: t("workflowDatabase.cards.logic.action"),
    routeName: "app-logic-flow",
    icon: ControlOutlined
  }
]);

const guides = computed(() => [
  {
    key: "provider",
    index: "01",
    title: t("workflowDatabase.guides.provider.title"),
    description: t("workflowDatabase.guides.provider.desc")
  },
  {
    key: "draft",
    index: "02",
    title: t("workflowDatabase.guides.draft.title"),
    description: t("workflowDatabase.guides.draft.desc")
  },
  {
    key: "verify",
    index: "03",
    title: t("workflowDatabase.guides.verify.title"),
    description: t("workflowDatabase.guides.verify.desc")
  }
]);

function openRoute(name: RouteName) {
  void router.push({
    name,
    params: { appKey: appKey.value }
  });
}
</script>

<style scoped>
.workflow-databases-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.hero-card,
.resource-card,
.guide-card {
  border-radius: 20px;
  overflow: hidden;
}

.hero-card {
  background:
    radial-gradient(circle at top right, rgba(59, 130, 246, 0.18), transparent 38%),
    linear-gradient(135deg, #f8fbff 0%, #eff6ff 48%, #eef2ff 100%);
}

.hero-content {
  display: flex;
  justify-content: space-between;
  gap: 24px;
  align-items: center;
  flex-wrap: wrap;
}

.hero-copy {
  max-width: 720px;
}

.hero-badge {
  display: inline-flex;
  align-items: center;
  padding: 6px 10px;
  border-radius: 999px;
  background: rgba(37, 99, 235, 0.12);
  color: #1d4ed8;
  font-size: 12px;
  font-weight: 600;
  margin-bottom: 12px;
}

.hero-title {
  margin: 0;
  font-size: 30px;
  line-height: 1.2;
  color: #0f172a;
}

.hero-desc {
  margin: 12px 0 0;
  color: #475569;
  line-height: 1.7;
  font-size: 14px;
}

.hero-actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.resource-card {
  height: 100%;
  border: 1px solid #eef2f7;
  transition: transform 0.18s ease, box-shadow 0.18s ease, border-color 0.18s ease;
}

.resource-card:hover {
  transform: translateY(-2px);
  border-color: #c7d2fe;
  box-shadow: 0 16px 40px rgba(15, 23, 42, 0.08);
}

.resource-card__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.resource-card__icon {
  width: 48px;
  height: 48px;
  border-radius: 14px;
  background: linear-gradient(135deg, #eff6ff, #eef2ff);
  color: #2563eb;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
}

.resource-card__title {
  margin: 0 0 8px;
  font-size: 18px;
  color: #111827;
}

.resource-card__desc {
  margin: 0 0 12px;
  color: #6b7280;
  line-height: 1.7;
  min-height: 48px;
}

.resource-card__action {
  padding-left: 0;
}

.guide-card {
  border: 1px solid #eef2f7;
}

.guide-list {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
}

.guide-item {
  display: flex;
  gap: 12px;
  padding: 16px;
  border-radius: 16px;
  background: #f8fafc;
}

.guide-item__index {
  width: 36px;
  height: 36px;
  border-radius: 12px;
  background: #dbeafe;
  color: #1d4ed8;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  flex-shrink: 0;
}

.guide-item__body {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.guide-item__title {
  color: #111827;
  font-weight: 600;
}

.guide-item__desc {
  color: #6b7280;
  line-height: 1.6;
  font-size: 13px;
}

@media (max-width: 768px) {
  .hero-title {
    font-size: 24px;
  }

  .resource-card__desc {
    min-height: 0;
  }
}
</style>
